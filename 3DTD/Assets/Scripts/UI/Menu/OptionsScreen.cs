using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Options (redesign B4): General, Video, Audio and Controls tabs of 58 px rows. Edits stay pending until
// Apply, which is the screen's CTA and only enabled while there are unsaved changes.
public class OptionsScreen : MonoBehaviour
{
    [SerializeField] private SegmentedControl tabs;
    [SerializeField] private GameObject[] pages = new GameObject[4];
    [SerializeField] private List<OptionRow> rows = new List<OptionRow>();
    [SerializeField] private BevelButton applyButton;
    [SerializeField] private TMP_Text unsavedLabel;
    [SerializeField] private BevelButton resetButton;
    [Tooltip("Shown when the screen is opened on top of the game (pause menu); hidden in the main menu")]
    [SerializeField] private BevelButton backButton;
    [SerializeField] private BevelButton resetProgressButton;
    [SerializeField] private TMP_Text resetProgressLabel;

    public event Action Closed;

    private GameOptions.Values pending;
    private float resetArmedUntil = -1f;

    private void Awake()
    {
        tabs.OnValueChanged += ShowPage;
        foreach (OptionRow row in rows)
            row.Changed += RefreshPending;
        applyButton.Clicked += _ => Apply();
        resetButton.Clicked += _ => ResetToDefaults();
        if (backButton != null)
        {
            backButton.Clicked += _ => Close();
            // Inside the main menu the navigation leaves the screen; Back is for the pause menu
            backButton.gameObject.SetActive(GetComponentInParent<MainMenuScreen>(true) == null);
        }
        if (resetProgressButton != null)
            resetProgressButton.Clicked += _ => ResetProgress();
    }

    private void OnEnable()
    {
        pending = GameOptions.Current.Clone();
        BindRows();
        ShowPage(Mathf.Max(0, tabs.Value));
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    private void BindRows()
    {
        foreach (OptionRow row in rows)
            row.Bind(pending);
        RefreshPending();
    }

    private void ShowPage(int index)
    {
        tabs.SetValue(index, false);
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == index);
        }
    }

    private void RefreshPending()
    {
        int changes = pending.CountDifferences(GameOptions.Current);
        applyButton.SetInteractable(changes > 0);
        unsavedLabel.text = changes == 0 ? "All changes saved" : changes + (changes == 1 ? " unsaved change" : " unsaved changes");
    }

    private void Apply()
    {
        GameOptions.Save(pending);
        pending = GameOptions.Current.Clone();
        BindRows();
    }

    private void ResetToDefaults()
    {
        pending = GameOptions.Defaults();
        BindRows();
    }

    // Needs a second click within a few seconds; medals, Research, meta upgrades and the autosave are lost
    private void ResetProgress()
    {
        if (Time.unscaledTime > resetArmedUntil)
        {
            resetArmedUntil = Time.unscaledTime + 4f;
            resetProgressLabel.text = "Click again to reset";
            return;
        }
        resetArmedUntil = -1f;
        PlayerProgress.ResetAll();
        SaveGame.Delete();
        resetProgressLabel.text = "Progress reset";
    }

    private void Update()
    {
        if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil && resetProgressLabel != null)
        {
            resetArmedUntil = -1f;
            resetProgressLabel.text = "Reset progress";
        }
    }
}
