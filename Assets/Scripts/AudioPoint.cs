using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioPoint : MonoBehaviour
{
    public AudioClip Clip;

    private void Awake()
    {
        GetComponent<AudioSource>().playOnAwake = false;
    }

    public void Start()
    {
        GetComponent<AudioSource>().PlayOneShot(Clip);
        Destroy(gameObject, Clip.length);
    }
}
