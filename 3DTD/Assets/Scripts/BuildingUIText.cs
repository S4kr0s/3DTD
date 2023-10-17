using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine.UI;

public class BuildingUIText : MonoBehaviour
{
    private ButtonManager buttonManager;
    private GameObject buildingPrefab;
    private Building buildingInfo;

    public TextMeshProUGUI nameNormal;
    public TextMeshProUGUI nameHighlighted;
    public TextMeshProUGUI costNormal;
    public TextMeshProUGUI costHighlighted;
    public Image imageNormal;
    public Image imageHighlighted;

    public void SetBuilding(GameObject buildingPrefab)
    {
        this.buildingPrefab = buildingPrefab;

        buildingInfo = buildingPrefab.GetComponent<Building>();
    }

    private void Awake()
    {
        buttonManager = this.gameObject.GetComponent<ButtonManager>();
    }

    public void Refresh()
    {
        if (buttonManager == null)
            return;

        buttonManager.buttonText = buildingInfo.DisplayName;
        nameNormal.text = buildingInfo.DisplayName; nameHighlighted.text = buildingInfo.DisplayName;
        Debug.Log(buildingInfo.Cost);
        costNormal.text = buildingInfo.Cost.ToString(); costHighlighted.text = buildingInfo.Cost.ToString();
        imageNormal.sprite = buildingInfo.UISprite; imageHighlighted.sprite = buildingInfo.UISprite;
    }

    public void SelectBuilding()
    {
        BuildingManager.Instance.SetSelectedBuilding(buildingPrefab);
    }
}
