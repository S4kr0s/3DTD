using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPopSoundSpawn : MonoBehaviour
{
    [SerializeField] private GameObject audioClip;
    [SerializeField] private AudioSource audioSource;

    public static EnemyPopSoundSpawn Instance;

    // Late waves pop hundreds of layers per second; restarting the clip that often only costs CPU
    [SerializeField] private float minSecondsBetweenPops = 0.04f;
    private float lastPopTime = -1f;

    private void Start()
    {
        Instance = this;
        audioSource = Instantiate(audioClip, this.transform).GetComponent<AudioSource>();
    }

    public void PlayPopSound()
    {
        if (audioSource == null || Time.unscaledTime - lastPopTime < minSecondsBetweenPops)
            return;

        lastPopTime = Time.unscaledTime;
        audioSource.Play();
    }
}
