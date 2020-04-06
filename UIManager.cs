using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;  // 씬 관리자 관련 코드
using UnityEngine.UI;   // UI 관련 코드
using UnityEditor;
using System.IO;
using System.Text;
using Photon.Realtime;
using ExitGames.Client.Photon;

// 필요한 UI에 즉시 접근하고 변경할 수 있도록 허용하는 UI 매니저
public class UIManager : MonoBehaviourPunCallbacks
{
    // 싱글턴 접근용 프로퍼티
    public static UIManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindObjectOfType<UIManager>();
            }

            return m_instance;
        }
    }

    private static UIManager m_instance;    // 싱글턴이 할당될 변수

    public Text ammoText_mag;           // 탄알 표시용 텍스트(남은 탄알)
    public Text ammoText_remain;        // 탄알 표시용 텍스트(현재 보유한 전체 탄알)
    public Text enemyCountText;         // 남은 적 수 표시용 텍스트
    public Text killCountText;          // 적 플레이어를 죽인 횟수를 표시할 텍스트
    public Text killCountTextCenter;    // 중앙에 표시될 플레이어 킬 알림
    public Text alertDestroyMapText;    // 맵 파괴 알림을 표시할 텍스트
    public Text announceText;           // 게임이 끝났을 때 출력되는 알림말
    public Text rankText;               // 등 수 표시 텍스트
    public Text totalPlayersText;       // 전체 플레이어 수를 표시할 텍스트
    public Text nickNameText;           // 플레이어 닉네임을 표시할 텍스트

    public Button itemButton;           // 아이템 버튼

    public GameObject gameoverUI;       // 게임오버 시 활성화할 UI

    public Text killPointText;
    public Text hitPointText;
    public Text totalPointText;
    public Text ratingPointText;
    public Text aliveTimePointText;

    private int killPoint, hitPoint, totalPoint, ratingPoint, aliveTimePoint;

    public Slider healthSlider;     // 체력을 표시할 UI 슬라이더
    public Transform inventoryPanel;

    public GameObject[] hideInGameUI;

    public GameObject Player { private get; set; }  // 자신이 진행할 플레이어. PlayerHealth에서 공수해옴

    public int rank = 0;    // 등 수

    public GameObject ExitPopUp;

    [Header("Alert: ")]
    public float SecondOfAlertDestroy = 5f;

    public override void OnEnable()
    {
        base.OnEnable();
        itemButton.onClick.AddListener(UsePossedItem);
    }

    private void Awake()
    {
        // 씬에 싱글턴 오브젝트가 된 다른 UIManager 오브젝트가 있다면
        if (Instance != this)
        {
            // 자신을 파괴
            Destroy(gameObject);
        }
    }

    public void Update()
    {
        // 게임 종료버튼
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitPopUp.SetActive(true);
        }
    }

    public void UpdateInventory(GameObject pickItem)
    {
        IItem item = pickItem.GetComponent<IItem>();

        foreach (Transform slot in inventoryPanel)
        {
            Image image = slot.GetChild(0).GetComponent<Image>();

            if (!image.enabled)
            {
                image.enabled = true;
                image.sprite = item.itemIcon;

                break;
            }
            else
            {
                image.sprite = item.itemIcon;

                break;
            }
        }
    }

    // 소지 아이템 사용
    public void UsePossedItem()
    {
        // 현재 클라이언트의 플레이어가 소유한 아이템 이름
        string pickItemName = Player.GetComponent<PlayerStat>().PickItem.name;
        object[] content = new object[] { pickItemName };

        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };
        PhotonNetwork.RaiseEvent(NetEventCode.UsePossedItemEvent, content, raiseEventOptions, sendOptions);
    }

    // 체력바 갱신
    public void UpdateHealthSlider(float health)
    {
        healthSlider.value = health;
    }

    // 탄알 텍스트 갱신
    public void UpdateAmmoText(int magAmmo, int remainAmmo)
    {
        ammoText_mag.text = magAmmo.ToString();
        ammoText_remain.text = "/ " + remainAmmo.ToString();
    }

    // 남은 적 수 텍스트 갱신
    [PunRPC]
    public void UpdateEnemyCountText(int count)
    {
        GameManager.Instance.currentPlayers = count;
        enemyCountText.text = count.ToString();
    }

    // 킬 수 텍스트 갱신
    public void UpdateKillCountText(int count)
    {
        killCountText.text = count.ToString();
        StartCoroutine(ShowKillCountTextCenter(count));
    }

    // 중앙에 표시될 킬 카운트 텍스트 출력
    private IEnumerator ShowKillCountTextCenter(int count)
    {
        killCountTextCenter.color = new Color(1f, 0f, 0f, 1f);
        killCountTextCenter.text = count.ToString() + " 킬";
        yield return new WaitForSeconds(10.0f);
        killCountTextCenter.color = new Color(1f, 0f, 0f, 0f);
    }

    // 게임오버 UI 활성화
    public void SetActiveGameoverUI(bool active)
    {
        gameoverUI.SetActive(active);

        PlayerHealth ph = Player.GetComponent<PlayerHealth>();
        PlayerShooter ps = Player.GetComponent<PlayerShooter>();
        MoveController mc = Player.GetComponent<MoveController>();

        mc.moveJoystick.gameObject.SetActive(false);     // 움직임 컨트롤러
        ps.attackJoystick.gameObject.SetActive(false);    // 공격버튼 컨트롤러
        ps.reloadJoystick.gameObject.SetActive(false);    // 재장전버튼 컨트롤러

        for(int i = 0; i < hideInGameUI.Length; i++)
        {
            hideInGameUI[i].SetActive(false);
        }

        nickNameText.text = Social.localUser.userName;

        rank = GameManager.Instance.currentPlayers;

        if (rank <= 0)
        {
            rank = 1;
        }

        rankText.text = "#" + rank.ToString();
        totalPlayersText.text = PhotonNetwork.CurrentRoom.MaxPlayers.ToString();

        if (rank <= 40 && rank > 10)
        {
            announceText.text = "그럴 수 있어. 이런 날도 있지 뭐.";
        }
        else if (rank <= 10 && rank >= 2)
        {
            announceText.text = "TOP 10 달성!!";
        }
        else if (rank <= 1)
        {
            announceText.text = "이겼닭! 오늘 저녁은 치킨이닭!";
        }

        killPoint = ph.GetKillCount();
        hitPoint = ps.GetHitPoint();
        totalPoint = hitPoint + killPoint;
        ratingPoint = rank;
        aliveTimePoint = TimeManager.Instance.gamingSecondTime;

        killPointText.text = killPoint.ToString();
        hitPointText.text = hitPoint.ToString();
        totalPointText.text = totalPoint.ToString();
        ratingPointText.text = ratingPoint.ToString();
        aliveTimePointText.text = aliveTimePoint.ToString();
    }

    // 룸을 나갈때 누르는 메소드
    public void LeaveRoom()
    {
        // 게임 나갈시 종료
        GameManager.State = GameManager.GameState.End;

        PhotonNetwork.LeaveRoom();
    }

    // 룸을 나갈 때 자동 실행되는 메서드(LeaveRoom 실행뒤에 자동적으로 실행됨)
    public override void OnLeftRoom()
    {
        // 룸을 나가면 로비 씬으로 돌아감
        SceneManager.LoadScene("NetworkLobby");
    }
}
