using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using PHashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class GameManager : MonoBehaviourPunCallbacks
{
    // 싱글턴 접근용 프로퍼티
    private static GameManager m_instance;  // 싱글턴이 할당될 static 변수
    public static GameManager Instance
    {
        get
        {
            // 만약 싱글턴 변수에 아직 오브젝트가 할당되지 않았다면
            if (m_instance == null)
            {
                // 씬에서 GameManager 오브젝트를 찾아서 할당
                m_instance = FindObjectOfType<GameManager>();
            }
            // 싱글턴 오브젝트 반환
            return m_instance;
        }
    }

    // 게임 상태 관련
    public static GameState State = GameState.Wait;
    public enum GameState
    {
        Wait,       // 유저들 로딩 대기
        Standby,    // 게임 시작 전
        Start,      // 게임 시작
        Die,        // 본인 클라이언트가 죽을 때
        End         // 게임 끝
    }

    public GameObject[] characters;
    public GameObject[] enemies;
    [HideInInspector] public List<Transform> spawnPos;
    [HideInInspector] public bool[] spawnPosCheck = null;
    private int spawnIndex;
    public static float time = 0;
    public int currentPlayers = 0;
    public int totalPlayers = 0;

    // AI 관련
    public int aiCharaterCount = 0;
    [HideInInspector] public int numberOfAICreation = 0;

    private int selectedCharacterNum;

    [HideInInspector] public GameObject playersAndEnemys;

    // 클라이언트 관련
    [HideInInspector] public int PlayerViewID;      // 클라이언트 포톤 View ID
    [HideInInspector] public GameObject Player;      // 클라이언트

    // 게임로딩 체크용
    private bool imReady = false;
    [HideInInspector] public bool allReady = false;
    [HideInInspector] public bool[] readys = null;

    [Header("Destroy Related: ")]
    public bool isEnable2DestroyField = true;

    private void Awake()
    {
        // 씬에 싱글턴 오브젝트가 된 다른 GameManager 오브젝트가 있다면
        if (Instance != this)
        {
            // 자신을 파괴
            Destroy(gameObject);
        }

        // 최대인원수만큼 false로 초기화
        readys = Enumerable.Repeat<bool>(false, (int)PhotonNetwork.CurrentRoom.MaxPlayers).ToArray<bool>();
        spawnPos = transform.Find("SpawnTransform").GetComponentsInChildren<Transform>().ToList();
        // 부모 객체가 포함되지 않게 하기 위해서 첫 번째 요소를 제거
        spawnPos.RemoveAt(0);
        // 스폰 위치의 갯수만큼 false로 초기화
        spawnPosCheck = Enumerable.Repeat(false, spawnPos.Count).ToArray();

        // 케릭터 AI관리용 오브젝트 생성
        CreateCharacterAndAIManager();

        // 로딩완료됬단 것을 다른사람들에게 알림
        PHashtable setValue = new PHashtable();
        imReady = true;
        setValue.Add("ImReady", imReady);
        PhotonNetwork.LocalPlayer.SetCustomProperties(setValue);
    }

    // 게임 시작과 동시에 플레이어가 될 게임 오브젝트 생성
    private void Start()
    {
        StartCoroutine(ReadyCheck());       // 유저 게임씬 진입 체크용

        selectedCharacterNum = TurnOnTheStage.charactorNum;

        for (int i = 0; i < characters.Length; i++)
        {
            if (selectedCharacterNum == i)
            {
                // 플레이어의 생성 위치를 중복되지 않게 설정
                while (true)
                {
                    spawnIndex = Random.Range(0, spawnPos.Count);

                    if (spawnPosCheck[spawnIndex] == false)
                    {
                        spawnPosCheck[spawnIndex] = true;
                        photonView.RPC("SpawnIndexProcessOnClient", RpcTarget.All, spawnIndex);
                        GameObject go = PhotonNetwork.Instantiate(characters[i].name, spawnPos[spawnIndex].position, Quaternion.identity);

                        PlayerHealth player = go.GetComponent<PlayerHealth>();

                        // 게임오버 UI 활성화
                        player.onDeath += () => UIManager.Instance.SetActiveGameoverUI(true);
                        player.onDeath += () => DecreasePlayer();

                        break;
                    }
                }
            }
        }

        // 플레이어들을 제외한 나머지 적 AI 캐릭터 생성
        StartCoroutine(CreateEnemy());
    }

    private void Update()
    {
        #region 조건에 따른 게임상태변화
        if (GameManager.State == GameManager.GameState.Wait)
        {
            // 유저들이 imReady 상태값을 다 true 반환하면 Standby로 넘어감
            if (GameManager.Instance.allReady)
            {
                GameManager.State = GameManager.GameState.Standby;
            }
        }
        else if (GameManager.State == GameManager.GameState.Standby)
        {
            //// 대기시간이 0이면 게임시작함
            //if (TimeManager.Instance.StandbySecondTime == 0)
            //{
            //    GameManager.State = GameManager.GameState.Start;
            //}
            GameManager.State = GameManager.GameState.Start;
        }
        #endregion

        // 혼자남은 상황이라면
        if (GameManager.State == GameManager.GameState.Start && currentPlayers == 1 && !Player.GetComponent<PlayerHealth>().dead)
        {
            GameManager.State = GameManager.GameState.End;
            // 게임이 종료되었는데도 애니메이션 클립이 재생중일때
            if (Player.GetComponent<PlayerShooter>().playerAnimator.GetBool("IsMove"))
                Player.GetComponent<PlayerShooter>().playerAnimator.SetBool("IsMove", false);

            if (Player.GetComponent<PlayerShooter>().playerAnimator.GetBool("IsAttack"))
                Player.GetComponent<PlayerShooter>().playerAnimator.SetBool("IsAttack", false);

            UIManager.Instance.SetActiveGameoverUI(true);
        }
    }

    IEnumerator CreateEnemy()
    {
        // 게임상태가 Standby가 될때까지 대기
        while (State == GameState.Wait)
        {
            yield return new WaitForSeconds(0.02f);
        }

        // 플레이어들이 입장 한 후 남은 빈 자리를 AI를 채우기 위함
        aiCharaterCount = PhotonNetwork.CurrentRoom.MaxPlayers - PhotonNetwork.CurrentRoom.PlayerCount;

        if (PhotonNetwork.IsMasterClient)
        {
            // 남은 빈 자리만큼 AI 캐릭터 생성
            for (int i = 0; i < aiCharaterCount; i++)
            {
                // AI 캐릭터의 생성위치를 지정(플레이어와 중복된 위치로 생성되지 않음)
                while (true)
                {
                    spawnIndex = Random.Range(0, spawnPos.Count);

                    if (spawnPosCheck[spawnIndex] == false)
                    {
                        spawnPosCheck[spawnIndex] = true;
                        photonView.RPC("SpawnIndexProcessOnClient", RpcTarget.Others, spawnIndex);
                        GameObject go = PhotonNetwork.InstantiateSceneObject(enemies[Random.Range(0, enemies.Length)].name, spawnPos[spawnIndex].position, Quaternion.identity);

                        EnemyAI enemy = go.GetComponent<EnemyAI>();

                        enemy.onDeath += () => DecreasePlayer();
                        break;
                    }
                }
            }
        }

        // currentPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
        currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount + aiCharaterCount;

        // 처음 생성될 때는 전체 인원으로 초기화
        UIManager.Instance.UpdateEnemyCountText(currentPlayers);
    }

    [PunRPC]
    private void SpawnIndexProcessOnClient(int index)
    {
        spawnPosCheck[index] = true;
    }

    public void DecreasePlayer()
    {
        currentPlayers--;
        UIManager.Instance.UpdateEnemyCountText(currentPlayers);

        // 다른 플레이어들에게도 적용
        photonView.RPC("UpdateCurrentUserProcessOnClients", RpcTarget.Others, currentPlayers);
    }

    [PunRPC]
    private void UpdateCurrentUserProcessOnClients(int currentPlayers)
    {
        UIManager.Instance.UpdateEnemyCountText(currentPlayers);
    }

    // 케릭터 AI관리용 오브젝트 생성
    private void CreateCharacterAndAIManager()
    {
        // 케릭터 AI관리용 오브젝트
        playersAndEnemys = new GameObject("Players_And_Enemys");
    }

    // 인원 체크용
    IEnumerator ReadyCheck()
    {
        int checkCount;
        while (true)
        {
            checkCount = 0;
            for (int i = 0; i < PhotonNetwork.CurrentRoom.MaxPlayers; i++)
            {
                if (readys[i])
                {
                    checkCount++;
                    //Debug.Log("체크: " + checkCount);
                }
            }

            if (checkCount == PhotonNetwork.CurrentRoom.PlayerCount)
            {
                // Master만 표시
                if (PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new PHashtable { { "AllUserJoinTime", PhotonNetwork.Time } });
                }

                break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        while (!NetworkManagerInGame.Instance.IsAllUserJoinTime)
        {
            yield return null;
        }
        yield return new WaitForSeconds(3f);

        // Debug.Log("유저들 게임씬에 다 들어옴");
        allReady = true;
    }

    /*
    private void OnApplicationFocus(bool focus)
    {
        if(!focus)
        {
            Debug.Log("OnApplicationFocus의 if문 focus:" + focus);
        }
        else
        {
            Debug.Log("OnApplicationFocus의 else문 focus:" + focus);
        }
    }

    private void OnApplicationQuit()
    {
        DecreasePlayer();

        Debug.Log("OnApplicationQuit가 작동했습니다.");

        StartCoroutine(DisconnectingProcess());
    }

    private IEnumerator DisconnectingProcess()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("마스터가 나갔습니다.");
            int idx = Random.Range(0, PhotonNetwork.PlayerListOthers.Length);
            PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[idx]);
            Debug.Log("마이그레이션 된 마스터:" + PhotonNetwork.PlayerListOthers[idx].NickName + "입니다.");
        }

        yield return new WaitForSeconds(2f);

        PhotonNetwork.Disconnect();
    }

    private void OnApplicationPause(bool pause)
    {
        if(pause)
        {
            Debug.Log("OnApplicationPause의 pause:" + pause);
        }
        else
        {
            Debug.Log("OnApplicationPause의 pause:" + pause);
        }
    }
    */
}