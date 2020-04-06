using UnityEngine;
using UnityEngine.UI;

public class KillfeedItem : MonoBehaviour
{
    public string poolItemName = "Killfeed";

    [SerializeField]
    private Text killNotityText = null;

    private float duration = 0f;
    public float durationMax = 5f;

    private void Update()
    {
        duration += Time.deltaTime;
        if (duration > durationMax)
        {
            duration = 0f;
            ObjectPoolingManager.Instance.PushToPool(poolItemName, gameObject, ObjectPoolingManager.Instance.killfeedObj.transform);
        }
    }

    public void SetUp(string player, string source)
    {
        killNotityText.color = Color.white;

        if (source == null)
            killNotityText.text = player + " 이 <Color=\"RED\">자살</Color> 했습니다.";
        else
        {
            killNotityText.text = "<Color=\"RED\">" + source + "</Color> 이(가) <Color=\"BLUE\">" + player + "</Color> 를 죽였습니다.";
        }
    }
}
