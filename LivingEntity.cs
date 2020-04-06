using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 생명체로 동작할 게임 오브젝트들을 위한 뼈대 제공
// 체력, 대미지 받아들이기, 사망 기능 이벤트를 제공
public class LivingEntity : MonoBehaviourPun, IDamageable
{
    [HideInInspector] public Stat BaseStat;     // 스탯관련
    public bool dead { get; protected set; }    // 사망 상태
    public event Action onDeath;                // 사망 시 발동할 이벤트

    // 아이템부분(보호율이 데미지쪽에 관련된 식이라 LivingEntity에 삽입)
    [HideInInspector] public float protectRate { get; set; }   // 현재 보호율
    [HideInInspector] public string theKiller = null;

    #region 유니티 이벤트 함수
    protected virtual void Awake()
    {
        if (gameObject.CompareTag("PLAYER"))
        {
            BaseStat = gameObject.GetComponent<PlayerStat>();
        }
        else
        {
            BaseStat = gameObject.GetComponent<EnemyStat>();
        }
    }

    // 생명체가 활성화될 때 상태를 리셋
    protected virtual void OnEnable()
    {
        // 사망하지 않은 상태로 시작
        dead = false;
    }

    #endregion

    // 호스트 -> 모든 클라이언트 방향으로 체력과 사망 상태를 동기화하는 메서드
    [PunRPC]
    public void ApplyUpdatedHealth(float newHealth, bool newDead)
    {
        BaseStat.CStats.CurrentHealth = newHealth;
        dead = newDead;
    }

    // 대미지 처리
    // 호스트에서 먼저 단독 실행되고, 호스트를 통해 다른 클라이언트에서 일괄 실행됨
    [PunRPC]
    public virtual void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, string killerName)
    {
        theKiller = killerName;

        if (PhotonNetwork.IsMasterClient)
        {
            // 대미지만큼 체력 감소
            BaseStat.CStats.CurrentHealth -= damage * (1 - BaseStat.CStats.CurrentProtectRate);

            // 호스트에서 클라이언트로 동기화
            photonView.RPC("ApplyUpdatedHealth", RpcTarget.Others, BaseStat.CStats.CurrentHealth, dead);

            // 다른 클라이언트도 OnDamage를 실행하도록 함
            photonView.RPC("OnDamage", RpcTarget.Others, damage, hitPoint, hitNormal, theKiller);
        }

        // 체력이 0 이하 && 아직 죽지 않았다면 사망 처리 실행
        if (BaseStat.CStats.CurrentHealth <= 0 && !dead)
        {
            Die();
        }
    }

    // 체력을 회복하는 기능
    [PunRPC]
    public virtual void RestoreHealth(float newHealth)
    {
        if (dead)
        {
            // 이미 사망한 경우 체력을 회복할 수 없음
            return;
        }

        // 클라이언트만 가능
        if (photonView.IsMine)
        {
            // 체력 추가
            BaseStat.CStats.CurrentHealth += newHealth;
            if (BaseStat.CStats.CurrentHealth > BaseStat.CStats.InitialHealth)
                BaseStat.CStats.CurrentHealth = BaseStat.CStats.InitialHealth;

            // 서버에서 클라이언트로 동기화
            photonView.RPC("ApplyUpdatedHealth", RpcTarget.Others, BaseStat.CStats.CurrentHealth, dead);
        }
    }

    // 체력을 감소시키는 기능
    // 체력을 회복하는 기능
    [PunRPC]
    public virtual void ReduceHealth(float newHealth)
    {
        if (dead)
        {
            // 이미 사망한 경우 체력을 감소시킬 수 없음
            return;
        }

        // 클라이언트만 가능
        if (photonView.IsMine)
        {
            // 체력 감소(최저치 10. 10 이하는 아무리 FakeItem을 먹어도 체력감소가 안됨)
            BaseStat.CStats.CurrentHealth += newHealth;
            if (BaseStat.CStats.CurrentHealth < 10)
                BaseStat.CStats.CurrentHealth = 10;

            UIManager.Instance.UpdateHealthSlider(BaseStat.CStats.CurrentHealth);

            // 서버에서 클라이언트로 동기화
            photonView.RPC("ApplyUpdatedHealth", RpcTarget.Others, BaseStat.CStats.CurrentHealth, dead);
        }
    }

    // 사망 처리
    public virtual void Die()
    {
        // onDeath 이벤트에 등록된 메서드가 있다면 실행
        if (onDeath != null)
        {
            onDeath();
        }

        // 사망 상태를 참으로 변경
        dead = true;
    }
}