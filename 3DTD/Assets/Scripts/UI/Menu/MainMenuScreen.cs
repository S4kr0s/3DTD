using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Main menu frame (redesign B3-B6): brand and navigation on the left, one large content plate on the right.
// The background stays the live demo match of the menu scene, independent of the selected screen.
public class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private BevelButton continueButton;
    [SerializeField] private TMP_Text continueSummary;
    [Tooltip("Play a game, Towers, Upgrades, Options")]
    [SerializeField] private SegmentedControl navigation;
    [SerializeField] private GameObject[] screens = new GameObject[4];
    [SerializeField] private BevelButton exitButton;
    [SerializeField] private AudioMixer audioMixer;

    private SaveGame save;

    private void Awake()
    {
        GameOptions.RegisterMixer(audioMixer);
    }

    private void Start()
    {
        navigation.OnValueChanged += Show;
        continueButton.Clicked += _ => Continue();
        exitButton.Clicked += _ => Application.Quit();

        // Continue is hidden when there is no savegame
        save = SaveGame.Load();
        continueButton.gameObject.SetActive(save != null);
        if (save != null)
            continueSummary.text = save.Summary();

        Show(0);
    }

    public void Show(int index)
    {
        navigation.SetValue(index, false);
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] != null)
                screens[i].SetActive(i == index);
        }
    }

    private void Continue()
    {
        if (save != null)
            SaveGame.BeginRestore(save);
    }
}
