using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuObjectMain;
    [SerializeField] private TMPro.TMP_Text gamePausedText;
    [SerializeField] private GameObject continueButton;

    private bool canSwitchMenu = true;

    private static PauseMenu instance;
    public static PauseMenu Instance { get { return instance; } }

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
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (!canSwitchMenu)
            return;

        menuObjectMain.SetActive(!menuObjectMain.activeSelf);
        PauseGame(menuObjectMain.activeSelf);
    }

    // Temporary Solution
    public void GameOver()
    {
        gamePausedText.text = "GAME OVER!";
        continueButton.SetActive(false);

        ToggleMenu();

        canSwitchMenu = false;
    }

    public void GameWon()
    {
        gamePausedText.text = "GAME WON!";

        ToggleMenu();
        canSwitchMenu = true;
    }

    void PauseGame(bool pause)
    {
        if (pause)
        {
            GameManager.Instance.ChangeGameSpeed(0f);
        }
        else
        {
            GameManager.Instance.ChangeGameSpeed(1f);
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        GameManager.Instance.ChangeGameSpeed(1f);
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(0);
        GameManager.Instance.ChangeGameSpeed(1f);
    }
}
