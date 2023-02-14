using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Michsky.UI.ModernUIPack.SliderManager m_sliderManager;

    [SerializeField] private AudioMixer masterAudioMixer;

    private void Start()
    {
        masterAudioMixer.SetFloat("masterVolume", m_sliderManager.mainSlider.value - 80);
    }

    public void OnMasterVolumeSliderValueChanged()
    {
        masterAudioMixer.SetFloat("masterVolume", m_sliderManager.mainSlider.value - 80);
    }

    public void StartGame()
    {
        OnMasterVolumeSliderValueChanged();
        // Temporary
        SceneManager.LoadScene(1);
        GameManager.Instance.ChangeGameSpeed(1);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
