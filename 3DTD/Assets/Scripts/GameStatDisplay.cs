using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameStatDisplay : MonoBehaviour
{
    [SerializeField] private GameObject nextWaveButton;
    [SerializeField] private Toggle autoWaveButton;
    [SerializeField] private TMP_Text moneyDisplay;
    [SerializeField] private TMP_Text livesDisplay;
    [SerializeField] private TMP_Text roundDisplay;

    private void Start()
    {
        nextWaveButton.SetActive(true);
        HandleMoneyUpdated(GameManager.Instance.Money);
        HandleLivesUpdated(GameManager.Instance.Lives);
        HandleRoundUpdated(GameManager.Instance.Round);
        Spawner.Instance.OnWaveStarted += HandleWaveStarted;
        Spawner.Instance.OnWaveEnded += HandleWaveEnded;
        GameManager.Instance.OnMoneyChanged += HandleMoneyUpdated;
        GameManager.Instance.OnLivesChanged += HandleLivesUpdated;
        GameManager.Instance.OnRoundChanged += HandleRoundUpdated;

    }

    private void HandleWaveStarted(int value)
    {
        nextWaveButton.SetActive(false);
    }

    private void HandleWaveEnded(int value)
    {
        if (autoWaveButton.isOn)
            ButtonStartNewWave();
        else
            nextWaveButton.SetActive(true);
    }

    private void HandleMoneyUpdated(int value)
    {
        moneyDisplay.text = value.ToString();
    }

    private void HandleLivesUpdated(int value)
    {
        livesDisplay.text = value.ToString();
    }

    private void HandleRoundUpdated(int value)
    {
        roundDisplay.text = value++.ToString();
    }

    public void ButtonStartNewWave()
    {
        Spawner.Instance.StartNextWave();

        if (MapHighlighter.Instance != null)
            MapHighlighter.Instance.highlight = false;
    }
}
