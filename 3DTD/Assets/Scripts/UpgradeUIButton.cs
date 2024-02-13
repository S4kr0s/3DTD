using Michsky.UI.ModernUIPack;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUIButton : MonoBehaviour
{
    public TMP_Text UpgradeName;
    public TooltipContent UpgradeDescription;
    public TMP_Text UpgradePrice;
    public ButtonManagerBasic UpgradeButton;
    public Image[] upgradeCountImage;
    public Color upgradeActivated;
    public Color upgradeDisabled;

    public void Setup(UpgradeModule upgradeModule, int count)
    {
        UpgradeButton.buttonText = upgradeModule.Name;
        UpgradeName.text = upgradeModule.Name;
        UpgradeDescription.description = upgradeModule.Description;
        UpgradePrice.text = upgradeModule.Price.ToString();
        UpgradeButton.clickEvent.AddListener(delegate { Upgrade(upgradeModule); });
        SetColor(count);
    }

    public void SetColor(int count)
    {
        foreach (var item in upgradeCountImage)
        {
            item.color = Color.white;
        }

        for (int i = 0; i < count; i++)
        {
            upgradeCountImage[i].color = upgradeActivated;
        }
    }
    
    // not working, just an idea
    public void SetColorDeactivated(int count)
    {
        foreach (var item in upgradeCountImage)
        {
            item.color = Color.white;
        }

        for (int i = 0; i < upgradeCountImage.Length; i++)
        {
            if (i <= count)
                upgradeCountImage[i].color = upgradeActivated;
            else
                upgradeCountImage[i].color = upgradeDisabled;
        }
    }

    void Upgrade(UpgradeModule upgradeModule)
    {
        SelectionManager.Instance.UpgradeCurrentTower(upgradeModule);
        UpgradePanelManager.Instance.HandleSelectionChanged(SelectionManager.CurrentlySelected, SelectionManager.CurrentlySelected);
    }
}