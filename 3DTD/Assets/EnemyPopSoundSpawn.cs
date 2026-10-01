using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPopSoundSpawn : MonoBehaviour
{
    [SerializeField] private GameObject audioClip;
    [SerializeField] private AudioSource audioSource;

    public static EnemyPopSoundSpawn Instance;

    private void Start()
    {
        Instance = this;
        audioSource = Instantiate(audioClip, this.transform).GetComponent<AudioSource>();
    }

    public void PlayPopSound()
    {
        audioSource.Play();
    }
}
