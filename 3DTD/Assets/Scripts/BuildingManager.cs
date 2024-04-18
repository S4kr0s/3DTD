using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BuildingManager : MonoBehaviour
{
    //public GameObject SelectedBuilding => selectedBuilding;

    [SerializeField] private GameObject selectedBuilding;
    [SerializeField] private Transform parentTransform;
    [SerializeField] private GameObject buildingUIPrefab;
    [SerializeField] private List<BuildingUIText> buildingUIButtons;
    [SerializeField] private ToggleGroup toggleGroup;

    private static BuildingManager instance;
    public static BuildingManager Instance { get { return instance; } }

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        foreach (GameObject building in GameManager.Instance.Buildings) 
        {
            BuildingUIText buildingUIText = Instantiate(buildingUIPrefab, parentTransform).GetComponent<BuildingUIText>();
            buildingUIText.SetBuilding(building);
            buildingUIText.Refresh();
            buildingUIButtons.Add(buildingUIText);
            buildingUIText.toggle.group = toggleGroup;
        }
    }

    public void SetSelectedBuilding(GameObject building)
    {
        selectedBuilding = building;
    }

    public GameObject GetSelectedBuilding()
    {
        Toggle toggle = toggleGroup.ActiveToggles().First();
        return toggle.gameObject.GetComponent<BuildingUIText>().buildingPrefab;
    }
}