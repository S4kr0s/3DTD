using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuCanvasManager : MonoBehaviour
{
    [SerializeField] private GameObject levelSelectWindow;
    [SerializeField] private GameObject towersWindow;
    [SerializeField] private GameObject optionsWindow;
    [SerializeField] private GameObject upgradesWindow;

    public void DeactivateAllWindows(GameObject except)
    {
        if(!except.Equals(levelSelectWindow)) levelSelectWindow.SetActive(false);
        if (!except.Equals(towersWindow)) towersWindow.SetActive(false);
        if (!except.Equals(optionsWindow)) optionsWindow.SetActive(false);
        if (!except.Equals(upgradesWindow)) upgradesWindow.SetActive(false);
    }

    public void SwitchWindow(GameObject window)
    {
        window.SetActive(!window.activeSelf);
    }
}
