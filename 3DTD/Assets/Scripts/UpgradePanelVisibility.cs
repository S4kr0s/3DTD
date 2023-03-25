using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradePanelVisibility : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    void Update()
    {
        if (SelectionManager.CurrentlySelected == null)
        {
            panel.SetActive(false);
        }
        else
        {
            panel.SetActive(true);
        }
    }
}
