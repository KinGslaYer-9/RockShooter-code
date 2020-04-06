using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(NavMeshAgent))]
public class MoveAgent : MonoBehaviourPun
{
    private float patrolSpeed = 1.5f;
    private float traceSpeed = 4.0f;
    private float damping = 1.0f;                   // 회전할 때의 속도를 조절하는 계수

    [HideInInspector] public bool isPathInvalidDie = false;

    [HideInInspector] public NavMeshAgent agent;    // 경로 계산 AI 에이전트

    private EnemyAI enemyAI;

    // 순찰 여부를 판단하는 변수
    private bool _patrolling;
    public bool patrolling
    {
        get { return _patrolling; }
        set
        {
            _patrolling = value;
            if (_patrolling)
            {
                agent.speed = patrolSpeed;
                // 순찰 상태의 회전계수
                damping = 1.0f;
            }
        }
    }


    // 추적 대상의 위치를 저장하는 변수
    public Vector3 _traceTarget;
    public Vector3 traceTarget
    {
        get { return _traceTarget; }
        set
        {
            _traceTarget = value;
            agent.speed = traceSpeed;
            // 추적 상태의 회전계수
            damping = 7.0f;
            TraceTarget(_traceTarget);
        }
    }

    private void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
        // 목적지에 가까워질수록 속도를 줄이는 옵션을 비활성화
        agent.autoBraking = false;
        // 자동으로 회전하는 기능을 비활성화
        agent.updateRotation = false;
        agent.speed = patrolSpeed;

        patrolling = true;
    }

    // 다음 목적지까지 이동 명령을 내리는 함수
    private Vector3 MoveWayPoint()
    {
        agent.isStopped = false;
        return Utility.GetRandomPointOnNavMesh(transform.position, 20f);
    }


    // 플레이어를 추적할 때 이동시키는 함수
    private void TraceTarget(Vector3 pos)
    {
        if (agent.isPathStale) return;

        agent.destination = pos;
        agent.isStopped = false;
    }

    // 순찰 및 추적을 정지시키는 함수
    public void Stop()
    {
        agent.isStopped = true;
        // 바로 정지하기 위해 속도를 0으로 설정
        agent.velocity = Vector3.zero;
        _patrolling = false;
    }

    private void Update()
    {
        // 호스트일 경우만 이동 경로 계산
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // 살아있는 경우에만 실행됨
        if (!enemyAI.dead)
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                isPathInvalidDie = true;
                photonView.RPC("ApplyIsPathInvalidDie", RpcTarget.Others, isPathInvalidDie);
                enemyAI.Die();
                return;
            }

            // 적 캐릭터가 이동 중일 때만 회전
            if (agent.isStopped == false)
            {
                if (agent.desiredVelocity != Vector3.zero)
                {
                    // NavMeshAgent가 가야 할 방향 벡터를 쿼터니언 타입의 각도로 변환
                    Quaternion rot = Quaternion.LookRotation(agent.desiredVelocity);
                    // 보간 함수를 사용해 점진적으로 회전시킴
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * damping);
                }
            }

            // 순찰 모드가 아닐 경우 이후 로직을 수행하지 않음
            if (!_patrolling) return;

            if (agent.remainingDistance <= 1f)
                agent.SetDestination(MoveWayPoint());
        }
    }

    [PunRPC]
    public void ApplyIsPathInvalidDie(bool newisPathInvalidDie)
    {
        isPathInvalidDie = newisPathInvalidDie;
    }
}
