using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

// 플레이어 캐릭터의 생명체로서의 동작 담당
public class PlayerHealth : LivingEntity, IPunInstantiateMagicCallback
{
    [HideInInspector] public CharacterAudios CA;

    private AudioSource playerAudioPlayer;      // 플레이어 소리 재생기
    private Animator playerAnimator;
    private MoveController playerMovement;      // 플레이어 움직임 컴포넌트
    private PlayerShooter playerShooter;        // 플레이어 슈터 컴포넌트
    [HideInInspector] public PlayerStat PS;

    private bool isHit = false;                 // 실제 총알을 맞게 될 경우를 판별하기 위한 변수
    private float duration = 0f;                // 물이나 용암에 있을 때 시간을 측정하기 위한 누적 변수
    public float durationMax = 4f;              // 물이나 용암에서 버틸 수 있는 최대 시간
    public float charDisappearTime = 5f;        // 사망 애니메이션 직후 오브젝트를 사라지게할 시간

    private Image bloodScreen;                  // BloodScreen 텍스처를 저장하기 위한 변수

    private bool isOnLiquid = false;
    private const string liquidTag = "LIQUID";  // 물이나 용암에 닿았을 경우 상태를 체크하기 위한 태그
    private const string bulletTag = "BULLET";

    public ParticleSystem hitEffect;            // 피격 시 재생할 파티클 효과

    public Transform hudpos;
    public string hitTextName = "HitText";
    public string killfeedItemName = "KillfeedItem";
    public string bulletName = "Bullet";

    private int killCount = 0;

    protected override void Awake()
    {
        base.Awake();
        PS = (PlayerStat)BaseStat;

        CA = GetComponent<CharacterAudios>();
        playerAudioPlayer = GetComponent<AudioSource>();
        playerAnimator = GetComponent<Animator>();

        playerMovement = GetComponent<MoveController>();
        playerShooter = GetComponent<PlayerShooter>();

        bloodScreen = GameObject.Find("BloodScreen").GetComponent<Image>();
    }

    private void Update()
    {
        // 플레이어가 물 또는 용암 위에 있는지 확인
        if (isOnLiquid)
        {
            duration += Time.deltaTime;
            if (duration > durationMax)
            {
                duration = 0f;
                // 물이나 용암에서의 데미지 처리
                OnDamage(4f, Vector3.zero, Vector3.zero, null);
            }
        }

        // 맵의 최소 높이가 -2 정도 되기 때문에 -2 이하로 내려가면 자동으로 사망 처리
        if (transform.position.y <= -2)
            Die();
    }

    protected override void OnEnable()
    {
        // LivingEntity의 OnEnable() 실행(상태 초기화)
        base.OnEnable();

        // 로컬일 때만 체력관련 적용
        if (photonView.IsMine)
        {
            // 체력바의 최댓값을 기본 체력값으로 변경
            UIManager.Instance.healthSlider.maxValue = PS.CStats.InitialHealth;
            // 체력 슬라이더의 값을 현재 체력값으로 변경
            UIManager.Instance.UpdateHealthSlider(PS.CStats.CurrentHealth);
        }

        // 플레이어의 조작을 받는 컴포넌트 활성화
        playerMovement.enabled = true;
        playerShooter.enabled = true;
    }

    // 체력 회복
    [PunRPC]
    public override void RestoreHealth(float newHealth)
    {
        // LivingEntity의 RestoreHealth() 실행(체력 증가)
        base.RestoreHealth(newHealth);

        // 로컬일 때만 갱신
        if (photonView.IsMine)
        {
            // 갱신된 체력으로 체력 슬라이더에 갱신
            UIManager.Instance.UpdateHealthSlider(PS.CStats.CurrentHealth);
        }
    }

    // 대미지 처리
    [PunRPC]
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, string killerName)
    {
        if (!dead)
        {
            if (isHit)
            {
                // LivingEntity의 OnDamage() 실행(대미지 적용)
                base.OnDamage(damage, hitPoint, hitNormal, killerName);

                // 공격받은 지점으로 파티클 효과 재생
                hitEffect.transform.position = hitPoint;
                hitEffect.Play();

                ShowHitText();

                isHit = false;
            }

            if (isOnLiquid)
            {
                // LivingEntity의 OnDamage() 실행(대미지 적용)
                base.OnDamage(damage, hitPoint, hitNormal, killerName);

                isOnLiquid = false;
            }

            // 사망하지 않은 경우에만 효과음 재생
            playerAudioPlayer.PlayOneShot(CA.HitClip[Random.Range(0, CA.HitClip.Length)]);

            if (photonView.IsMine)
            {
                // 갱신된 체력을 체력 슬라이더에 반영
                UIManager.Instance.UpdateHealthSlider(PS.CStats.CurrentHealth);

                StartCoroutine(ShowBloodScreen());
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

    // 화면 혈흔 효과, 지정한 시간만큼 알파값을 높여서 화면에 표시됨
    private IEnumerator ShowBloodScreen()
    {
        bloodScreen.color = new Color(1f, 0f, 0f, 0.5f);
        yield return new WaitForSeconds(0.2f);
        // BloodScreen 텍스처의 색상을 모두 0으로 변경
        bloodScreen.color = Color.clear;
    }

    // 사망 처리
    public override void Die()
    {
        // LivingEntity의 Die() 실행(사망 적용)
        base.Die();

        if (photonView.IsMine)
        {
            // 자살한 경우
            if (transform.position.y <= -2 || isOnLiquid)
            {
                photonView.RPC("ShowKillfeedProcessOnClients", RpcTarget.All, photonView.Owner.NickName, null);
            }
            else
            {
                // 자신의 이름과 죽인 유저의 이름을 통해서 킬 로그를 출력
                photonView.RPC("ShowKillfeedProcessOnClients", RpcTarget.All, photonView.Owner.NickName, theKiller);
            }

            for (int i = 0; i < PhotonNetwork.PlayerListOthers.Length; i++)
            {
                if(PhotonNetwork.PlayerListOthers[i].NickName.Equals(theKiller))
                {
                    photonView.RPC("IncKillCountProcessOnClients", PhotonNetwork.PlayerListOthers[i], theKiller);
                }
            }
        }

        bloodScreen.color = Color.clear;

        // die라는 이름을 가진 클립의 여부를 판단하는 변수
        bool isNone = true;

        ItemManager.Instance.DropItemAfterDie(gameObject);

        // 사망음 재생
        playerAudioPlayer.PlayOneShot(CA.DeathClip[Random.Range(0, CA.DeathClip.Length)]);

        // AI 캐릭터의 탐지나 다른 플레이어의 탄에 맞지 않도록 콜라이더를 비활성화한다.
        Collider[] playerColliders = GetComponents<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            playerColliders[i].enabled = false;
        }

        // 현재까지 Animator에 있는 모든 Animation Clip 들을 가져온다.
        AnimationClip[] clips = playerAnimator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip c in clips)
        {
            // die라는 클립(Motion)이 있다면
            if (c.name.Equals("die"))
            {
                playerAnimator.SetTrigger("Die");
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
            // 죽었을시 상태변경
            GameManager.State = GameManager.GameState.Die;

            // 플레이어 조작을 받는 컴포넌트 비활성화
            playerMovement.enabled = false;
            playerShooter.enabled = false;

            playerMovement.moveJoystick.gameObject.SetActive(false);
            playerShooter.attackJoystick.gameObject.SetActive(false);
            playerShooter.reloadJoystick.gameObject.SetActive(false);

            if (isNone || (transform.position.y <= -2))
                PhotonNetwork.Destroy(gameObject);
            else
                StartCoroutine(DestroyAfter(gameObject, charDisappearTime));
        }
    }

    private void IncKillCount()
    {
        killCount++;
        UIManager.Instance.UpdateKillCountText(killCount);
    }

    [PunRPC]
    private void IncKillCountProcessOnClients(string killerName)
    {
        IncKillCount();
    }

    [PunRPC]
    private void ShowKillfeedProcessOnClients(string player, string source)
    {
        GameObject _killfeed = ObjectPoolingManager.Instance.PopFromPool(killfeedItemName);
        if (_killfeed != null)
        {
            _killfeed.SetActive(true);
            _killfeed.GetComponent<KillfeedItem>().SetUp(player, source);
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

    // 총알에 대한 감지
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(bulletTag))
        {
            isHit = true;
            ObjectPoolingManager.Instance.PushToPool(bulletName, other.gameObject, ObjectPoolingManager.Instance.bulletObj.transform);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(liquidTag))
        {
            isOnLiquid = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(liquidTag))
        {
            isOnLiquid = false;
        }
    }

    // 케릭터 생성시 콜백함수
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // 플레이어 이름 저장
        PS.PlayerName = info.Sender.NickName;
        // Debug.Log("케릭이름: " + PS.PlayerName);

        // 오브젝트 이름변경
        gameObject.name = string.Format("{0}. {1} / {2}", info.Sender.ActorNumber.ToString("00"),
            gameObject.name.Substring(0, gameObject.name.IndexOf("(")).ToString(), info.Sender.NickName);

        PhotonNetwork.NickName = Social.localUser.userName;

        // 케릭터 AI관리용 오브젝트로 부모설정
        gameObject.transform.SetParent(GameManager.Instance.playersAndEnemys.transform);
    }

    public int GetKillCount()
    {
        return killCount;
    }
}