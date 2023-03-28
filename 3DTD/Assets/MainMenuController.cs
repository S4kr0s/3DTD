using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;

    void Start()
    {
        mixer.SetFloat("towersVolume", -80f);
    }
}
