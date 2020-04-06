using UnityEngine;

// 오브젝트에 총알이 맞았을경우 사라지게 하는 코드
public class RemoveBullet : MonoBehaviour
{
    private const string bulletTag = "BULLET";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(bulletTag))
            other.gameObject.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(bulletTag))
            collision.collider.gameObject.SetActive(false);
    }
}
