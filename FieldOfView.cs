using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public float viewRadius;    // 시야 범위
    [Range(0, 360)]
    public float viewAngle;     // 시야 각도

    public LayerMask targetMask;    // 플레이어 검출용 레이어
    public LayerMask obstacleMask;  // 장애물 검출용 레이어

    public List<Transform> visibleTargets = new List<Transform>();

    private EnemyAI enemyAI;
    private Transform tmpTagetTr = null;

    private void Start()
    {
        enemyAI = GetComponent<EnemyAI>();

        StartCoroutine("FindTargetsWithDelay", 0.2f);
    }

    private IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisibleTargets();
        }
    }

    private void FindVisibleTargets()
    {
        visibleTargets.Clear();
        enemyAI.targetTr = null;

        // ViewRadius 유닛 만큼의 반지름을 가진 가상의 구를 그렸을 때, 구와 겹치는 모든 콜라이더를 가져옴
        // 단, targetMask 레이어를 가진 콜라이더만 가져오도록 필터링
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        // 모든 콜라이더를 순회하면서, 해당 타겟을 찾는다
        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;

            // 찾아낸 타겟의 방향을 구함
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                // 찾아낸 타겟의 거리를 구한다
                float dstToTarget = Vector3.Distance(transform.position, target.position);

                // 장애물이 검출되지 않았을 경우 visibleTargets 리스트에 추가한다.
                if (!Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), dirToTarget, dstToTarget, obstacleMask))
                {
                    float height = transform.position.y - target.position.y;
                    if(height <= 0.5 && height >= -0.5 && transform.GetHashCode() != target.GetHashCode())
                    {
                        visibleTargets.Add(target);

                        if(visibleTargets.Count >= 1)
                        {
                            // 가장 가까운 타겟을 할당
                            float minDistance = (visibleTargets[0].position - transform.position).sqrMagnitude;
                            tmpTagetTr = visibleTargets[0];
                            for (int j = 1; j < visibleTargets.Count; j++)
                            {
                                float tmpDistance = (visibleTargets[j].position - transform.position).sqrMagnitude;

                                if (minDistance * minDistance > tmpDistance * tmpDistance)
                                {
                                    minDistance = tmpDistance;
                                    tmpTagetTr = visibleTargets[j];
                                }
                            }
                            enemyAI.targetTr = tmpTagetTr;
                        }
                        else
                        {
                            enemyAI.targetTr = visibleTargets[0];
                        }
                    }
                }
            }
        }
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}