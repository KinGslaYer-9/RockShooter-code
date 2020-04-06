using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolingManager : MonoBehaviour
{
    // 싱글턴 접근용 프로퍼티
    public static ObjectPoolingManager Instance
    {
        get
        {
            // 만약 싱글턴 변수에 아직 오브젝트가 할당되지 않았다면
            if (m_instance == null)
                // 씬에서 ObjectPoolingManager 오브젝트를 찾아서 할당
                m_instance = FindObjectOfType<ObjectPoolingManager>();

            // 싱글턴 오브젝트 반환
            return m_instance;
        }
    }

    private static ObjectPoolingManager m_instance; // 싱글턴이 할당될 static 변수

    public List<PooledObject> pooledObjects = new List<PooledObject>();

    public GameObject hitTextObj;
    public GameObject bulletObj;
    public GameObject killfeedObj;
    public string hitTextName = "HitText";
    public string killfeedItemName = "Killfeed";
    public string bulletName = "Bullet";

    private void Awake()
    {
        // poolCount 수 만큼 리스트에 객체가 생성되어 추가 됨
        for (int i = 0; i < pooledObjects.Count; i++)
        {
            if (pooledObjects[i].poolItemName.Equals(killfeedItemName))
                pooledObjects[i].Initialize(killfeedObj.transform);
            else if (pooledObjects[i].poolItemName.Equals(bulletName))
                pooledObjects[i].Initialize(bulletObj.transform);
            else if (pooledObjects[i].poolItemName.Equals(hitTextName))
                pooledObjects[i].Initialize(hitTextObj.transform);
            else
                pooledObjects[i].Initialize(transform);
        }
    }

    // 사용한 객체를 반환할 때 사용할 메소드
    public bool PushToPool(string itemName, GameObject item, Transform parent = null)
    {
        PooledObject pool = GetPoolItem(itemName);
        // 검색에 실패
        if (pool == null)
            return false;

        pool.PushToPool(item, parent == null ? transform : parent);
        return true;
    }

    // 필요한 객체를 오브젝트 풀에 요청할 때 사용할 메소드
    public GameObject PopFromPool(string itemName, Transform parent = null)
    {
        PooledObject pool = GetPoolItem(itemName);
        // 검색에 실패
        if (pool == null)
            return null;

        return pool.PopFromPool(parent);
    }

    // itemName 파라미터와 같은 이름을 가진 오브젝트 풀을 검색하고, 검색에 성공하면 결과를 리턴
    private PooledObject GetPoolItem(string itemName)
    {
        for(int i = 0; i < pooledObjects.Count; i++)
            if (pooledObjects[i].poolItemName.Equals(itemName))
                return pooledObjects[i];

        return null;
    }
}
