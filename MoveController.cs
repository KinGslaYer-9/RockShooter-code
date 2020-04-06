using UnityEngine;
using Photon.Pun;
using CoolJoystick;

// 플레이어의 이동에 관한 처리
public class MoveController : MonoBehaviourPun
{
    [HideInInspector] public PlayerStat PS;
    private float gravity = 300.0f;

    private Animator animator;
    [HideInInspector] public Joystick moveJoystick,attackJoystick;
    [HideInInspector] public CharacterController controller;
    private Vector3 moveDirection = Vector3.zero;    // 캐릭터의 움직이는 방향
    private Vector3 lookDirection = Vector3.zero;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        moveJoystick = GameObject.FindGameObjectWithTag("MOVEJOYSTICK").GetComponent<Joystick>();
        attackJoystick = GameObject.FindGameObjectWithTag("ATTACKJOYSTICK").GetComponent<Joystick>();
        PS = gameObject.GetComponent<PlayerStat>();
    }

    private void Update()
    {
        // 로컬 플레이어만 직접 위치와 회전 변경 가능
        if (!photonView.IsMine)
        {
            return;
        }

        // 게임시작해서 살아있을동안만 조작가능
        if (GameManager.State != GameManager.GameState.Start)
        {
            return;
        }

        moveDirection = new Vector3(moveJoystick.Horizontal * PS.CStats.CurrentSpeed, 0, moveJoystick.Vertical * PS.CStats.CurrentSpeed);
        lookDirection = new Vector3(attackJoystick.Horizontal, 0, attackJoystick.Vertical);

        if (moveJoystick.Horizontal != 0f || moveJoystick.Vertical != 0f)
        {
            animator.SetBool("IsMove", true);
            transform.rotation = Quaternion.LookRotation(moveDirection);
            if(attackJoystick.Horizontal != 0f || attackJoystick.Vertical != 0f)
            {
                animator.SetBool("IsMove", true);
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
        else
        {
            animator.SetBool("IsMove", false);
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }
}
