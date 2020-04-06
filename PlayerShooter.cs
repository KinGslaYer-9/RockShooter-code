using UnityEngine;
using Photon.Pun;
using CoolJoystick;

// 플레이어의 발사에 관한 처리
public class PlayerShooter : MonoBehaviourPun
{
    public Gun gun; // 사용할 총

    public GameObject aimSprite;       // 총알이 날아갈 방향을 표시

    private Vector3 lookDirection = Vector3.zero;
    private float damping = 5.0f;
    [HideInInspector] public Animator playerAnimator;

    [HideInInspector] public Joystick attackJoystick;
    [HideInInspector] public Joystick reloadJoystick;

    private int hitPoint = 0;

    void Start()
    {
        playerAnimator = GetComponent<Animator>();
        attackJoystick = GameObject.FindGameObjectWithTag("ATTACKJOYSTICK").GetComponent<Joystick>();
        reloadJoystick = GameObject.FindGameObjectWithTag("RELOADJOYSTICK").GetComponent<Joystick>();
    }

    private void Update()
    {
        // 로컬 플레이어만 총을 직접 사격. 탄알 UI 갱신 가능
        if(!photonView.IsMine)
        {
            aimSprite.SetActive(false);
            return;
        }

        // 게임시작해서 살아있을동안만 조작가능
        if (GameManager.State != GameManager.GameState.Start)
        {
            return;
        }

        lookDirection = new Vector3(attackJoystick.Horizontal, 0, attackJoystick.Vertical);

        // 입력을 감지하고 총을 발사
        if (attackJoystick.Pressed)
        {
            if (attackJoystick.Horizontal != 0f || attackJoystick.Vertical != 0f)
            {
                Quaternion rot = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * damping);
            }

            if (gun.state == Gun.State.Ready && Time.time >= gun.lastFireTime + gun.timeBetFire)
            {
                gun.Fire();
                playerAnimator.SetBool("IsAttack", true);
            }
        }
        else
        {
            playerAnimator.SetBool("IsAttack", false);
        }

        if(reloadJoystick.Pressed)
        {
            gun.Reload();
        }

        // 남은 탄알 UI 갱신
        UpdateUI();
    }
    
    // 탄알 UI 갱신
    private void UpdateUI()
    {
        if(gun != null && UIManager.Instance != null)
        {
            // UI 매니저의 탄알 텍스트에 탄창의 탄알과 남은 전체 탄알 표시
            UIManager.Instance.UpdateAmmoText(gun.magAmmo, gun.ammoRemain);
        }
    }

    public int GetHitPoint()
    {
        return hitPoint;
    }

    public void IncHitPoint()
    {
        hitPoint++;
    }
}
