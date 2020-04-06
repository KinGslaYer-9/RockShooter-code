using System.Collections;
using UnityEngine;
using Photon.Pun;

// 총에 관한 처리
public class Gun : MonoBehaviourPun, IPunObservable
{
    // 총의 상태를 표현
    public enum State
    {
        Ready,      // 발사 준비됨
        Empty,      // 탄창이 빔
        Reloading   // 재장전 중
    }

    public State state { get; private set; }

    public Transform fireTransform;                 // 탄알이 발사될 위치
    public ParticleSystem muzzleFlashEffect;        // 총구 화염 효과

    [HideInInspector] public CharacterAudios CA;
    private AudioSource gunAudioPlayer; // 총 소리 생성기

    private float fireDistance;   // 사정거리

    public int ammoRemain = 100;    // 남은 전체 탄알
    public int magCapacity = 25;    // 탄창 용량
    public int magAmmo;             // 현재 탄창에 남아 있는 탄알

    public float timeBetFire = 0.12f;   // 탄알 발사 간격
    public float reloadTime = 1.8f;  // 재장전 소요 시간
    public float lastFireTime;  // 총을 마지막으로 발사한 시점

    private GameObject MyCharacter;

    public string bulletName = "Bullet";

    // 주기적으로 자동 실행되는 동기화 메서드
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 로컬 오브젝트라면 쓰기 부분이 실행됨
        if (stream.IsWriting)
        {
            // 남은 탄알 수를 네트워크를 통해 보내기
            stream.SendNext(ammoRemain);
            // 탄창의 탄알 수를 네트워크를 통해 보내기
            stream.SendNext(magAmmo);
            // 현재 총의 상태를 네트워크를 통해 보내기
            stream.SendNext(state);
        }
        else
        {
            // 리모트 오브젝트라면 읽기 부분이 실행됨
            // 남은 탄알 수를 네트워크를 통해 받기
            ammoRemain = (int)stream.ReceiveNext();
            // 탄창의 탄알 수를 네트워크를 통해 받기
            magAmmo = (int)stream.ReceiveNext();
            // 현재 총의 상태를 네트워크를 통해 받기
            state = (State)stream.ReceiveNext();
        }
    }

    // 남은 탄알을 추가하는 메서드
    [PunRPC]
    public void AddAmmo(int ammo)
    {
        ammoRemain += ammo;
    }

    private IEnumerator GetCom()
    {
        GameObject tmp = gameObject;
        while (true)
        {
            yield return new WaitForSeconds(0.02f);
            if (tmp.CompareTag("PLAYER") || tmp.CompareTag("ENEMY"))
            {
                gunAudioPlayer = tmp.GetComponent<AudioSource>();
                CA = tmp.GetComponent<CharacterAudios>();
                break;
            }
            tmp = tmp.transform.parent.gameObject;
        }
    }

    private void Awake()
    {
        // 최상위 오브젝트의 컴포넌트를 가져오기 위함
        StartCoroutine(GetCom());

        // 케릭터의 최상위 게임오브젝트 가져오기
        GameObject tmp = gameObject;
        while (true)
        {
            if (tmp.CompareTag("PLAYER"))
            {
                MyCharacter = tmp;
                break;
            }

            tmp = tmp.transform.parent.gameObject;
        }

        //MyCharacter = transform.root.gameObject;
    }

    private void OnEnable()
    {
        // 현재 탄창을 가득 채우기
        magAmmo = magCapacity;
        // 총의 현재 상태를 총을 쏠 준비가 된 상태로 변경
        state = State.Ready;
        // 마지막으로 총을 쏜 시점을 초기화
        lastFireTime = 0;
        // 총알 거리를 초기화
        fireDistance = BulletCtrl._bulletMaxDistance;
    }

    public void Fire()
    {
        // 현재 상태가 발사 가능한 상태
        // && 마지막 총 발사 시점에서 timeBetFire 이상의 시간이 지남
        if (state == State.Ready && Time.time >= lastFireTime + timeBetFire)
        {
            // 마지막 총 발사 시점 갱신
            lastFireTime = Time.time;

            Shot();
        }
    }

    private void Shot()
    {
        photonView.RPC("ShotProcessOnServer", RpcTarget.MasterClient);

        // 남은 탄알 수를 -1
        magAmmo--;

        if (magAmmo <= 0)
        {
            // 탄창에 남은 탄알이 없다면 총의 현재 상태를 Empty로 갱신
            state = State.Empty;
        }
    }

    [PunRPC]
    private void ShotProcessOnServer()
    {
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
                photonView.RPC("IncHitPointOnClients", RpcTarget.All);
                
                // 상대방의 OnDamage 함수를 실행시켜 상대방에 대미지 주기
                target.OnDamage(MyCharacter.GetComponent<PlayerStat>().CStats.CurrentDamage, hit.point, hit.normal, photonView.Owner.NickName);
            }
        }

        // 발사 이펙트 재생. 이펙트 재생은 모든 클라이언트에서 실행
        photonView.RPC("ShotEffectProcessOnClients", RpcTarget.All);
    }

    [PunRPC]
    private void IncHitPointOnClients()
    {
        if(photonView.IsMine)
        {
            MyCharacter.GetComponent<PlayerShooter>().IncHitPoint();
        }
    }

    // 물리적인 총알 발사
    [PunRPC]
    private void bulletProcessOnClients()
    {
        // 탄알 오브젝트 풀링
        GameObject _bullet = ObjectPoolingManager.Instance.PopFromPool(bulletName);
        if (_bullet != null)
        {
            _bullet.transform.position = fireTransform.position;
            _bullet.transform.rotation = fireTransform.rotation;
            _bullet.SetActive(true);
        }
    }

    // 이펙트 재생 메서드를 랩핑하는 메서드
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

        gunAudioPlayer.PlayOneShot(CA.ShotClip);
    }

    // 재장전 시도
    public bool Reload()
    {
        if (state == State.Reloading || ammoRemain <= 0 || magAmmo >= magCapacity)
        {
            // 이미 재장전 중이거나 남은 탄알이 없거나
            // 탄창에 탄알이 이미 가득 찬 경우 재장전할 수 없음
            return false;
        }

        // 재장전 처리 시작
        StartCoroutine(ReloadRoutine());
        return true;
    }

    // 실제 재장전 처리를 진행
    private IEnumerator ReloadRoutine()
    {
        // 현재 상태를 재장전 상태로 전환
        state = State.Reloading;
        // 재장전 소리 재생
        gunAudioPlayer.PlayOneShot(CA.ReloadClip);

        // 재장전 소요 시간만큼 처리 쉬기
        yield return new WaitForSeconds(reloadTime);

        // 탄창에 채울 탄알을 계산
        int ammoToFill = magCapacity - magAmmo;

        // 탄창에 채워야 할 탄알이 남은 탄알보다 많다면
        // 채워야 할 탄알 수를 남은 탄알 수에 맞춰 줄임
        if (ammoRemain < ammoToFill)
        {
            ammoToFill = ammoRemain;
        }

        // 탄창을 채움
        magAmmo += ammoToFill;
        // 남은 탄알에서 탄창에 채운만큼 탄알을 뺌
        ammoRemain -= ammoToFill;

        // 총의 현재 상태를 발사 준비된 상태로 변경
        state = State.Ready;
    }
}
