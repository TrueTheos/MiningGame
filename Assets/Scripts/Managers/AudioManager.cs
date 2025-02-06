using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private List<AudioClip> _mineClips;

    private AudioSource _source;

    private void Awake()
    {
        Instance = this;
        _source = GetComponent<AudioSource>();
    }

    public void PlayMine()
    {
        _source.PlayOneShot(_mineClips.Random());
    }

    public void Play(AudioClip clip)
    {
        _source.PlayOneShot(clip);
    }
}
