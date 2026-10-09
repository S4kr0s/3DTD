using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// Pause, game-over and victory overlay. Esc first cancels a placement, then closes the tower panel,
// then toggles this menu. Resuming restores the game speed that was active before the pause.
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuObjectMain;
    [SerializeField] private GameObject menuPlate;
    [SerializeField] private TMPro.TMP_Text gamePausedText;
    [SerializeField] private TMPro.TMP_Text subtitleText;
    [SerializeField] private BevelButton primaryButton;
    [SerializeField] private TMPro.TMP_Text primaryLabel;
    [SerializeField] private BevelButton restartButton;
    [SerializeField] private BevelButton optionsButton;
    [SerializeField] private BevelButton mainMenuButton;

    [Header("Victory")]
    [SerializeField] private GameObject rewardRow;
    [SerializeField] private MedalRow medalRow;
    [SerializeField] private TMPro.TMP_Text rewardText;

    [Header("Options")]
    [SerializeField] private OptionsScreen optionsScreen;
    [SerializeField] private AudioMixer audioMixer;

    private enum Mode
    {
        Paused,
        GameOver,
        Victory,
    }

    private bool canSwitchMenu = true;
    private Mode mode = Mode.Paused;
    private float speedBeforePause = 1f;

    private static PauseMenu instance;
    public static PauseMenu Instance { get { return instance; } }

    public bool IsOpen => menuObjectMain.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        GameOptions.RegisterMixer(audioMixer);
        menuObjectMain.SetActive(false);
        if (optionsScreen != null)
        {
            optionsScreen.gameObject.SetActive(false);
            optionsScreen.Closed += CloseOptions;
        }
    }

    private void Start()
    {
        primaryButton.Clicked += _ => HandlePrimary();
        restartButton.Clicked += _ => RestartGame();
        optionsButton.Clicked += _ => OpenOptions();
        mainMenuButton.Clicked += _ => LoadMainMenu();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (optionsScreen != null && optionsScreen.gameObject.activeSelf)
        {
            optionsScreen.Close();
            return;
        }
        if (!IsOpen)
        {
            if (BuildingManager.Instance != null && BuildingManager.Instance.CancelPlacement())
                return;
            if (UpgradePanelManager.Instance != null && UpgradePanelManager.Instance.IsOpen)
            {
                UpgradePanelManager.Instance.ClearSelection();
                return;
            }
        }
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (!canSwitchMenu)
            return;

        bool open = !menuObjectMain.activeSelf;
        if (open)
            Refresh();
        else if (mode == Mode.Victory)
            mode = Mode.Paused;   // closed with Esc or the pause button: later pauses are plain pauses
        menuObjectMain.SetActive(open);
        PauseGame(open);
    }

    public void GameOver()
    {
        mode = Mode.GameOver;
        if (!IsOpen)
            ToggleMenu();
        else
            Refresh();

        canSwitchMenu = false;
    }

    public void GameWon()
    {
        mode = Mode.Victory;
        if (!IsOpen)
            ToggleMenu();
        else
            Refresh();
        canSwitchMenu = true;
    }

    private void Refresh()
    {
        if (menuPlate != null)
            menuPlate.SetActive(true);

        GameManager game = GameManager.Instance;
        string level = LevelCatalog.Instance != null ? LevelCatalog.Instance.DisplayName(SceneManager.GetActiveScene().name) : SceneManager.GetActiveScene().name;
        subtitleText.text = level + " · " + game.Difficulty + " · Wave " + game.Round;

        switch (mode)
        {
            case Mode.GameOver:
                gamePausedText.text = "Hull breached";
                primaryLabel.text = "Try again";
                restartButton.gameObject.SetActive(false);
                rewardRow.SetActive(false);
                break;
            case Mode.Victory:
                gamePausedText.text = "Victory";
                primaryLabel.text = "Keep playing";
                restartButton.gameObject.SetActive(true);
                rewardRow.SetActive(true);
                medalRow.Set(PlayerProgress.GetMedals(SceneManager.GetActiveScene().name));
                rewardText.text = game.LastWinReward > 0
                    ? game.Difficulty + " medal · +" + game.LastWinReward + " Research"
                    : game.Difficulty + " medal already earned";
                break;
            default:
                gamePausedText.text = "Paused";
                primaryLabel.text = "Resume";
                restartButton.gameObject.SetActive(true);
                rewardRow.SetActive(false);
                break;
        }
    }

    private void HandlePrimary()
    {
        if (mode == Mode.GameOver)
        {
            RestartGame();
            return;
        }
        // After the victory screen the game continues as freeplay; later pauses are plain pauses
        mode = Mode.Paused;
        ToggleMenu();
    }

    void PauseGame(bool pause)
    {
        if (pause)
        {
            if (GameManager.Instance.GameSpeed > 0f)
                speedBeforePause = GameManager.Instance.GameSpeed;
            GameManager.Instance.ChangeGameSpeed(0f);
        }
        else
        {
            GameManager.Instance.ChangeGameSpeed(speedBeforePause > 0f ? speedBeforePause : 1f);
        }
    }

    private void OpenOptions()
    {
        if (optionsScreen == null)
            return;
        menuPlate.SetActive(false);
        optionsScreen.Open();
    }

    private void CloseOptions()
    {
        if (menuPlate != null && IsOpen)
            menuPlate.SetActive(true);
    }

    public void RestartGame()
    {
        // A restart is a new game: the autosave of this run no longer applies
        SaveGame.Delete();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        GameManager.Instance.ChangeGameSpeed(1f);
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(0);
        GameManager.Instance.ChangeGameSpeed(1f);
    }
}
