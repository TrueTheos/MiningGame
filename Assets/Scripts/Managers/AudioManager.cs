using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Serializable]
    public struct SFX
    {
        public AudioClip Clip;
        public float Volume;
    }

    public static AudioManager Instance;

    [SerializeField] private List<SFX> _mineClips;
    [SerializeField] private SFX _pickupClip;
    [SerializeField] private SFX _placeClip;
    [SerializeField] private SFX _spearThrow;
    [SerializeField] private SFX _fallDamage;
    [SerializeField] private SFX _monsterDamage;

    private AudioSource _source;

    private void Awake()
    {
        Instance = this;
        _source = GetComponent<AudioSource>();
    }

    #region Utility
    public void Play(SFX sfx)
    {
        _source.PlayOneShot(sfx.Clip, sfx.Volume);
    }
    public void PlayAtPos(AudioPoint audio, Vector3 pos)
    {
        Instantiate(audio.gameObject, pos, Quaternion.identity);
    }
    #endregion

    public void PlayMine() => Play(_mineClips.Random());
    public void PlayPickup() => Play(_pickupClip);
    public void PlayPlace() => Play(_placeClip);
    public void PlaySpearThrow() => Play(_spearThrow);
    public void PlayFallDamage() => Play(_fallDamage);
    public void PlayMonsterDamage() => Play(_monsterDamage);

    public void Play(AudioClip clip)
    {
        _source.PlayOneShot(clip);
    }
}
