using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

[RequireComponent(typeof(AudioSource))]
public class AudioPoint : MonoBehaviour
{
    public SFX Clip;

    private void Awake()
    {
        GetComponent<AudioSource>().playOnAwake = false;
    }

    public void Start()
    {
        GetComponent<AudioSource>().PlayOneShot(Clip.Clip, Clip.Volume);
        Destroy(gameObject, Clip.Clip.length);
    }
}
