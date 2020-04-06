using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAudios : MonoBehaviour
{
    [Header("Gun Related: ")]
    public AudioClip ShotClip;      // 총소리
    public AudioClip ReloadClip;    // 장전소리

    [Space(10)]
    [Header("Character Related: ")]
    public AudioClip ItemPickupClip;    // 아이템 습득 소리
    public AudioClip[] DeathClip;       // 사망 소리
    public AudioClip[] HitClip;         // 피격 소리
}
