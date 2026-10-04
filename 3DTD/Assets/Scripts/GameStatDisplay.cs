using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// In-game HUD (redesign B1): Scrap / Hull / Wave plate, pause, game speed, Next wave CTA and the auto-wave switch
public class GameStatDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyDisplay;
    [SerializeField] private TMP_Text livesDisplay;
    [SerializeField] private TMP_Text roundDisplay;
    [SerializeField] private BevelButton pauseButton;

    [Header("Game speed")]
    [SerializeField] private SegmentedControl speedControl;
    [SerializeField] private float[] speeds = { 1f, 3f, 5f, 10f };

    [Header("Waves")]
    [SerializeField] private BevelButton nextWaveButton;
    [SerializeField] private TMP_Text nextWaveSubLabel;
    [SerializeField] private ToggleSwitch autoWaveSwitch;
    [SerializeField] private TMP_Text autoWaveState;

    private Spawner spawner;
    // Scrap changes every frame in late waves; the counter is rebuilt without allocating
    private readonly System.Text.StringBuilder moneyText = new System.Text.StringBuilder(64);
    private int shownMoney = int.MinValue;
    private readonly System.Text.StringBuilder livesText = new System.Text.StringBuilder(64);
    private int shownLives = int.MinValue;
    // The top bar fits its content: a counter's width only follows its digit count, so a new value doesn't
    // resize the bar (TMP's preferred width also follows the last glyph, and every resize during a layout
    // rebuild makes UGUI start a coroutine)
    private LayoutElement moneyWidth;
    private LayoutElement livesWidth;
    private int moneyDigits = -1;
    private int livesDigits = -1;

    private void Start()
    {
        // Spawner.Instance is assigned in Spawner.Start, which may run after this one
        spawner = Spawner.Instance != null ? Spawner.Instance : GameObject.FindGameObjectWithTag("Spawner").GetComponent<Spawner>();

        HandleMoneyUpdated(GameManager.Instance.Money);
        HandleLivesUpdated(GameManager.Instance.Lives);
        HandleRoundUpdated(GameManager.Instance.Round);
        spawner.OnWaveStarted += HandleWaveStarted;
        spawner.OnWaveEnded += HandleWaveEnded;
        GameManager.Instance.OnMoneyChanged += HandleMoneyUpdated;
        GameManager.Instance.OnLivesChanged += HandleLivesUpdated;
        GameManager.Instance.OnRoundChanged += HandleRoundUpdated;
        GameManager.Instance.OnGameSpeedChanged += HandleGameSpeedChanged;

        if (pauseButton != null)
            pauseButton.Clicked += _ => PauseMenu.Instance.ToggleMenu();
        if (nextWaveButton != null)
            nextWaveButton.Clicked += _ => ButtonStartNewWave();
        if (speedControl != null)
        {
            speedControl.OnValueChanged += HandleSpeedSelected;
            HandleGameSpeedChanged(GameManager.Instance.GameSpeed);
        }
        if (autoWaveSwitch != null)
        {
            autoWaveSwitch.SetIsOn(GameOptions.AutoWaveOnStart, false);
            autoWaveSwitch.OnValueChanged += HandleAutoWaveChanged;
            HandleAutoWaveChanged(autoWaveSwitch.IsOn);
        }
        RefreshNextWave();
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnWaveStarted -= HandleWaveStarted;
            spawner.OnWaveEnded -= HandleWaveEnded;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMoneyChanged -= HandleMoneyUpdated;
            GameManager.Instance.OnLivesChanged -= HandleLivesUpdated;
            GameManager.Instance.OnRoundChanged -= HandleRoundUpdated;
            GameManager.Instance.OnGameSpeedChanged -= HandleGameSpeedChanged;
        }
    }

    private void HandleWaveStarted(int value)
    {
        RefreshNextWave();
    }

    private void HandleWaveEnded(int value)
    {
        if (autoWaveSwitch != null && autoWaveSwitch.IsOn && !GameManager.Instance.IsGameOver)
            ButtonStartNewWave();
        RefreshNextWave();
    }

    private void HandleMoneyUpdated(int value)
    {
        if (value == shownMoney)
            return;
        shownMoney = value;
        FitDigits(moneyDisplay, ref moneyWidth, ref moneyDigits, value);
        UIFormat.SetTabular(moneyDisplay, value, moneyText);
    }

    private void HandleLivesUpdated(int value)
    {
        value = Mathf.Max(0, value);
        if (value == shownLives)
            return;
        shownLives = value;
        FitDigits(livesDisplay, ref livesWidth, ref livesDigits, value);
        UIFormat.SetTabular(livesDisplay, value, livesText);
    }

    private static void FitDigits(TMP_Text text, ref LayoutElement width, ref int shownDigits, int value)
    {
        int digits = value < 0 ? 1 : 0;
        for (long remaining = System.Math.Abs((long)value); ; remaining /= 10)
        {
            digits++;
            if (remaining < 10)
                break;
        }
        if (digits == shownDigits)
            return;
        shownDigits = digits;
        if (width == null && !text.TryGetComponent(out width))
            width = text.gameObject.AddComponent<LayoutElement>();
        width.preferredWidth = text.GetPreferredValues(UIFormat.Tabular(new string('8', digits))).x;
    }

    private void HandleRoundUpdated(int value)
    {
        roundDisplay.text = UIFormat.Tabular(value);
        RefreshNextWave();
    }

    private void HandleSpeedSelected(int index)
    {
        if (index >= 0 && index < speeds.Length)
            GameManager.Instance.ChangeGameSpeed(speeds[index]);
    }

    // Keeps the segmented control in sync; speed 0 (paused) leaves the last choice highlighted
    private void HandleGameSpeedChanged(float speed)
    {
        if (speedControl == null || speed <= 0f)
            return;
        for (int i = 0; i < speeds.Length; i++)
        {
            if (Mathf.Approximately(speeds[i], speed))
            {
                speedControl.SetValue(i, false);
                return;
            }
        }
    }

    private void HandleAutoWaveChanged(bool isOn)
    {
        if (autoWaveState != null)
            autoWaveState.text = isOn ? "On" : "Off";
    }

    // The CTA always names the upcoming wave; it is disabled while a wave runs
    private void RefreshNextWave()
    {
        if (nextWaveButton == null)
            return;
        bool waveActive = spawner != null && spawner.IsWaveActive;
        nextWaveButton.SetInteractable(!waveActive);
        if (nextWaveSubLabel != null)
            nextWaveSubLabel.text = "Wave " + (GameManager.Instance.Round + 1);
    }

    public void ButtonStartNewWave()
    {
        spawner.StartNextWave();

        if (MapHighlighter.Instance != null)
            MapHighlighter.Instance.highlight = false;
    }
}
