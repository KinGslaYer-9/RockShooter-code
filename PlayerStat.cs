using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStat : Stat
{
    #region 스텟관련 컴포넌트
    PlayerHealth playerHealth;
    MoveController moveController;
    Gun gun;
    #endregion

    #region 필드

    [Header("Information: ")]
    public string PlayerName;
    public GameObject PickItem;

    #endregion


    void Awake()
    {
        // 컴포넌트 연결
        playerHealth = GetComponent<PlayerHealth>();
        moveController = GetComponent<MoveController>();
        gun = GetComponent<PlayerShooter>().gun;

        // 초기 현재값설정
        CStats.CurrentHealth = CStats.InitialHealth;
        CStats.CurrentProtectRate = CStats.InitialProtectRate;
        CStats.CurrentDamage = CStats.InitialDamage;
        CStats.CurrentSpeed = CStats.InitialSpeed;

        // 조종하는 케릭터값 전달
        if (photonView.IsMine)
        {
            GameManager.Instance.Player = gameObject;
            GameManager.Instance.PlayerViewID = gameObject.GetPhotonView().ViewID;

            ItemManager.Instance.Player = gameObject;
            ItemManager.Instance.PlayerPV = gameObject.GetPhotonView();

            UIManager.Instance.Player = gameObject;

            NetworkManagerInGame.Instance.clientPlayer = gameObject;
        }
    }
}