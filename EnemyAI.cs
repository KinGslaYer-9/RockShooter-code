using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(MoveAgent))]
public class EnemyAI : LivingEntity, IPunInstantiateMagicCallback, IPunObservable
{
    // 적 캐릭터의 상태를 표현
    public enum State
    {
        Patrol,
        Trace,
        Attack
    }

    // 총의 상태를 표현
    public enum GunState
    {
        Ready,      // 발사 준비됨
        Reloading   // 재장전 중
    }

    public State state { get; private set; } = State.Patrol;
    public GunState gunState { get; private set; } = GunState.Ready;

    private Animator animator;

    [HideInInspector] public CharacterAudios CA;
    [HideInInspector] public AudioSource audioPlayer;
    [HideInInspector] public EnemyStat ES;

    private MoveAgent moveAgent;                        // AI 플레이어의 이동 처리 스크립트
    private FieldOfView fieldOfView;                    // AI 플레이어의 시야 처리 스크립트

    [HideInInspector] public Transform targetTr;        // fieldOfView에서 반환된 타겟의 위치

    public int maxShotCount = 30;                       // 최대 발사 할 수 있는 횟수
    private int shotCount = 0;                          // 현재 발사 횟수
    public float reloadTime = 5f;                       // 재장전 하는데 걸리는 시간
    public float timeBetFire = 0.5f;                    // 공격 간격
    private float lastFireTime = 0;                     // 마지막 공격 시점
    public float damping = 10.0f;                       // 플레이어를 향해 회전할 속도 계수

    private bool isHit = false;                         // 실제 총알을 맞게 될 경우를 판별하기 위한 변수

    private float fireDistance;                         // 공격 사정거리
    private float traceDistance;                        // 추적 사정거리
    [HideInInspector] public float damage = 20f;        // 공격력

    public float charDisappearTime = 5f;                // 사망시 캐릭터 오브젝트가 사라질 시간

    public Transform fireTransform;                     // 탄알이 발사될 위치
    public ParticleSystem muzzleFlashEffect;            // 총구 화염 파티클

    private const string bulletTag = "BULLET";          // 총알 태그(총알 감지용)

    public Transform hudpos;                            // hitText가 표시될 위치

    // 오브젝트 풀링을 위한 오브젝트 이름
    public string hitTextName = "HitText";
    public string killfeedItemName = "Killfeed";
    public string bulletName = "Bullet";

    // 주기적으로 자동 실행되는 동기화 메서드
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 로컬 오브젝트라면 쓰기 부분이 실행됨
        if (stream.IsWriting)
        {
            // 현재 발사한 수를 네트워크를 통해 보내기
            stream.SendNext(shotCount);
            // 현재 총의 상태를 네트워크를 통해 보내기
            stream.SendNext(gunState);
        }
        else
        {
            // 리모트 오브젝트라면 읽기 부분이 실행됨
            // 현재 발사한 수를 네트워크를 통해 받기
            shotCount = (int)stream.ReceiveNext();
            // 현재 총의 상태를 네트워크를 통해 받기
            gunState = (GunState)stream.ReceiveNext();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        ES = (EnemyStat)BaseStat;

        fieldOfView = GetComponent<FieldOfView>();
        CA = GetComponent<CharacterAudios>();
        moveAgent = GetComponent<MoveAgent>();
        animator = GetComponent<Animator>();
        audioPlayer = GetComponent<AudioSource>();

        fireDistance = BulletCtrl._bulletMaxDistance;
        traceDistance = fieldOfView.viewRadius;         // 추적 거리를 시야범위로 초기화
    }

    private void Start()
    {
        // 호스트만 AI 플레이어의 행동 처리를 갱신
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        StartCoroutine(CheckState());
        StartCoroutine(Action());
    }

    private void FixedUpdate()
    {
        // 호스트만 AI 플레이어의 공격 처리를 갱신
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (!dead && (state == State.Attack))
        {
            // 타겟이 죽었을 때 잠시 null이 되는데 이를 방지하기 위함
            if (targetTr != null)
            {
                // 주인공이 있는 위치까지의 회전 각도 계산
                Quaternion rot = Quaternion.LookRotation(targetTr.position - transform.position);
                // 보간 함수를 사용해 점진적으로 회전시킴
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * damping);

                Fire();
            }
        }
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로 갱신
    private IEnumerator CheckState()
    {
        yield return new WaitForSeconds(0.05f);

        // 적 캐릭터가 사망하기 전까지 실행되는 무한 루프
        while (!dead)
        {
            if (targetTr != null)
            {
                float distance;

                distance = (targetTr.position - transform.position).sqrMagnitude;

                if (distance <= fireDistance * fireDistance)
                    state = State.Attack;
                else if (distance <= traceDistance * traceDistance)
                    state = State.Trace;
                else
                    state = State.Patrol;
            }
            else
                state = State.Patrol;

            yield return new WaitForSeconds(0.05f);
        }
    }

    // 상태에 따라 적 캐릭터의 행동을 처리하는 코루틴 함수
    IEnumerator Action()
    {
        // 적 캐릭터가 사망할 때까지 무한루프
        while (!dead)
        {
            yield return new WaitForSeconds(0.05f);
            // 상태에 따라 분기 처리
            switch (state)
            {
                case State.Patrol:
                    // 순찰 모드를 활성화
                    moveAgent.patrolling = true;
                    animator.SetBool("IsMove", true);
                    break;
                case State.Trace:
                    // 추적할 대상을 할당
                    moveAgent.traceTarget = targetTr.position;
                    animator.SetBool("IsMove", true);
                    break;
                case State.Attack:
                    // 순찰 및 추적을 정지
                    moveAgent.Stop();
                    animator.SetBool("IsMove", false);
                    break;
            }
        }
    }

    // 대미지를 입었을 때 실행할 처리
    [PunRPC]
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, string killerName)
    {
        // 아직 사망하지 않은 경우에만 피격 효과 재생
        if (!dead)
        {
            if (isHit)
            {
                // LivingEntity의 OnDamage() 실행(대미지 적용)
                base.OnDamage(damage, hitPoint, hitNormal, killerName);

                // 오브젝트 풀링을 통한 HitText 출력
                ShowHitText();

                // 피격 효과음 재생
                audioPlayer.PlayOneShot(CA.HitClip[Random.Range(0, CA.HitClip.Length)]);

                isHit = false;
            }
        }
    }

    private void ShowHitText()
    {
        GameObject _hitText = ObjectPoolingManager.Instance.PopFromPool(hitTextName);
        if (_hitText != null)
        {
            _hitText.transform.position = hudpos.position;
            _hitText.SetActive(true);
        }
    }

    // 사망 처리
    public override void Die()
    {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        base.Die();

        // die라는 이름을 가진 클립의 여부를 판단하는 변수
        bool isNone = true;

        ItemManager.Instance.DropItemAfterDie(gameObject);

        // 사망 효과음 재생
        audioPlayer.PlayOneShot(CA.DeathClip[Random.Range(0, CA.DeathClip.Length)]);

        // AI 캐릭터의 탐지나 다른 플레이어의 탄에 맞지 않도록 콜라이더를 비활성화한다.
        Collider[] aiColliders = GetComponents<Collider>();
        for (int i = 0; i < aiColliders.Length; i++)
        {
            aiColliders[i].enabled = false;
        }

        // 현재까지 Animator에 있는 모든 Animation Clip 들을 가져온다.
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip c in clips)
        {
            // die라는 클립(Motion)이 있다면
            if (c.name.Equals("die"))
            {
                animator.SetTrigger("Die");
                // 클립이 존재하므로 기존 false 유지
                isNone = false;
                break;
            }
            else
            {
                // 클립이 없으므로 트리거를 통해 실행하지 않음(오브젝트를 바로 삭제)
                isNone = true;
            }
        }

        if (photonView.IsMine)
        {
            if (moveAgent.isPathInvalidDie)
            {
                // 만일 맵이 사라져서 죽을 경우, 공격하는 사람이 없음
                photonView.RPC("KillfeedProcessOnClients", RpcTarget.All, gameObject.name, null);
            }
            else
            {
                // 다른 플레이어나 AI가 AI를 죽였을 경우
                photonView.RPC("KillfeedProcessOnClients", RpcTarget.All, gameObject.name, theKiller);
            }

            if (isNone)
                PhotonNetwork.Destroy(gameObject);
            else
                StartCoroutine(DestroyAfter(gameObject, charDisappearTime));
        }
    }

    private IEnumerator DestroyAfter(GameObject target, float delay)
    {
        // delay만큼 대기
        yield return new WaitForSeconds(delay);

        // target이 파괴되지 않았으면 파괴 실행
        if (target != null)
        {
            PhotonNetwork.Destroy(target);
        }
    }

    [PunRPC]
    private void KillfeedProcessOnClients(string player, string source)
    {
        GameObject _killfeed = ObjectPoolingManager.Instance.PopFromPool(killfeedItemName, ObjectPoolingManager.Instance.killfeedObj.transform);
        if (_killfeed != null)
        {
            _killfeed.SetActive(true);
            _killfeed.GetComponent<KillfeedItem>().SetUp(player, source);
        }
    }

    public void Fire()
    {
        // 현재 상태가 발사 가능한 상태
        // && 마지막 총 발사 시점에서 timeBetFire 이상의 시간이 지남
        if (gunState == GunState.Ready && Time.time >= lastFireTime + timeBetFire)
        {
            // 마지막 총 발사 시점 갱신
            lastFireTime = Time.time;
            // 실제 발사 처리 실행
            StartCoroutine(Shot());
        }
    }

    // 실제 발사 처리
    private IEnumerator Shot()
    {
        shotCount++;

        if (shotCount >= maxShotCount && !(gunState == GunState.Reloading))
        {
            gunState = GunState.Reloading;

            yield return new WaitForSeconds(reloadTime);

            shotCount = 0;

            // 총의 현재 상태를 발사 준비된 상태로 변경
            gunState = GunState.Ready;
        }
        else
        {
            animator.SetTrigger("Fire");

            // 실제 발사 처리는 호스트에 대리
            photonView.RPC("ShotProcessOnServer", RpcTarget.MasterClient);
        }
    }

    [PunRPC]
    private void ShotProcessOnServer()
    {
        // 레이캐스트에 의한 충돌 정보를 저장하는 컨테이너
        RaycastHit hit;

        photonView.RPC("bulletProcessOnClients", RpcTarget.All);

        // 레이캐스트(시작 지점, 방향, 충돌 정보 컨테이너, 사정거리)
        if (Physics.Raycast(fireTransform.position, fireTransform.forward, out hit, fireDistance))
        {
            // 레이가 어떤 물체와 충돌한 경우
            // 충돌한 상대방으로부터 IDamageable 오브젝트 가져오기 시도
            IDamageable target = hit.collider.GetComponent<IDamageable>();

            // 상대방으로부 IDamageable 오브젝트를 가져오는 데 성공했다면
            if (target != null)
            {
                // 상대방의 OnDamage 함수를 실행시켜 상대방에 대미지 주기
                target.OnDamage(damage, hit.point, hit.normal, gameObject.name);
                // target.OnDamage(damage, hit.point, hit.normal);
            }
        }

        // 발사 이펙트 재생. 이펙트 재생은 모든 클라이언트에서 실행
        photonView.RPC("ShotEffectProcessOnClients", RpcTarget.All);
    }

    // 실제 총알 발사
    [PunRPC]
    private void bulletProcessOnClients()
    {
        GameObject _bullet = ObjectPoolingManager.Instance.PopFromPool(bulletName);
        if (_bullet != null)
        {
            _bullet.transform.position = fireTransform.position;
            _bullet.transform.rotation = fireTransform.rotation;
            _bullet.SetActive(true);
        }
    }

    // 이펙트 재생 코루틴을 랩핑하는 메서드
    [PunRPC]
    private void ShotEffectProcessOnClients()
    {
        // 발사 이펙트 재생 시작
        ShotEffect();
    }

    // 발사 이펙트와 소리를 재생
    private void ShotEffect()
    {
        // 총구 화염 효과 재생
        muzzleFlashEffect.Play();

        // 총격 소리 재생
        audioPlayer.PlayOneShot(CA.ShotClip);
    }

    // 총알에 대한 감지
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(bulletTag))
        {
            isHit = true;
            ObjectPoolingManager.Instance.PushToPool(bulletName, other.gameObject, ObjectPoolingManager.Instance.bulletObj.transform);
        }
    }

    // AI 생성시 콜백함수
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        GameManager.Instance.numberOfAICreation++;
        // 이름변경
        gameObject.name = string.Format("{0}. {1}", GameManager.Instance.numberOfAICreation.ToString("00"),
            gameObject.name.Substring(0, gameObject.name.IndexOf("(")).ToString());
        ES.EnemyName = gameObject.name;

        // 케릭터 AI관리용 오브젝트로 부모설정
        gameObject.transform.SetParent(GameManager.Instance.playersAndEnemys.transform);
    }
}
