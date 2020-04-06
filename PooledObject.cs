using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PooledObject
{
    public string poolItemName = string.Empty;                      // 객체 검색 때 사용할 이름
    public GameObject prefab = null;                                // 오브젝트 풀에 저장할 프리팹
    public int poolCount = 0;                                       // 초기화할 때 생성할 객체의 수
    [SerializeField]
    private List<GameObject> poolList = new List<GameObject>();     // 생성한 객체들을 저장할 리스트

    // 필요한 객체를 미리 생성해서 리스트에 저장
    // parent 파라미터는 생성된 객체들을 정리하는 용도로 사용됨
    // parent가 null 이라면 ObjectPoolingManager 게임 오브젝트의 자식으로 지정됨
    public void Initialize(Transform parent = null)
    {
        for (int i = 0; i < poolCount; i++)
        {
            poolList.Add(CreateItem(parent));
        }
    }

    // 사용한 객체를 다시 오브젝트 풀에 반환할 때 사용할 메소드
    public void PushToPool(GameObject item, Transform parent = null)
    {
        item.transform.SetParent(parent);
        item.SetActive(false);
        poolList.Add(item);
    }

    // 객체가 필요할 때 오브젝트 풀에 요청하는 용도로 사용할 메소드
    public GameObject PopFromPool(Transform parent = null)
    {
        // 저장해둔 오브젝트가 남아있는 지 확인하고, 없으면 새로 생성해서 추가
        if (poolList.Count == 0)
            poolList.Add(CreateItem(parent));
        // 저장해 둔 리스트에서 하나를 꺼내서 이 객체를 반환
        GameObject item = poolList[0];
        poolList.RemoveAt(0);
        return item;
    }

    // prefab 변수에 지정된 게임 오브젝트를 생성하는 메소드
    private GameObject CreateItem(Transform parent = null)
    {
        GameObject item = Object.Instantiate(prefab, parent) as GameObject;
        item.name = poolItemName;
        item.SetActive(false);
        return item;
    }
}