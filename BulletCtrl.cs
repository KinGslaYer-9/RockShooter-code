using UnityEngine;

/*
 * 총알 제어에 관한 스크립트
 * 일정 거리만큼 총알이 이동하면 사라지는 기능을 함
 */
public class BulletCtrl : MonoBehaviour
{
    [HideInInspector] public string poolItemName = "Bullet";

    private float speed = 1000.0f;      // 총알 발사 속도
    public float bulletMaxDistance;
    public static float _bulletMaxDistance = 5f;     // 사정거리, 수정 시 이 값만 수정하면 다른 곳에서 적용 됨
    private Vector3 _originalPosition;  // 발사 시 처음 위치를 저장할 변수

    // 초기화 시 동작할 컴포넌트
    private Transform _tr;
    private Rigidbody _rb;
    private TrailRenderer _trail;

    private void Awake()
    {
        bulletMaxDistance = _bulletMaxDistance;
        _tr = GetComponent<Transform>();
        _rb = GetComponent<Rigidbody>();
        _trail = GetComponent<TrailRenderer>();
    }

    private void OnEnable()
    {
        _originalPosition = transform.position;     // 처음 위치 저장
        _rb.AddForce(transform.forward * speed);
    }

    private void OnDisable()
    {
        // 재활용된 총알의 여러 효과값을 초기화
        _trail.Clear();
        _tr.position = Vector3.zero;
        _tr.rotation = Quaternion.identity;
        _rb.Sleep();
    }
    
    private void Update()
    {
        // 거리 계산을 위한 변수
        float currentDistance = Vector3.Distance(_originalPosition, transform.position);

        // 처음 위치에서 현재 위치까지의 거리가 사정거리보다 크면 총알을 제거
        if (currentDistance >= bulletMaxDistance)
            ObjectPoolingManager.Instance.PushToPool(poolItemName, gameObject, ObjectPoolingManager.Instance.bulletObj.transform);
    }
}
