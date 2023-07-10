using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    [SerializeField] private Building selectedBuilding;
    [SerializeField] private List<BuildingUIText> buildingUIButtons;
    [SerializeField] private GameObject buildingButtonList;
    [SerializeField] private GameObject buildingUITextPrefab;

    public Building SelectedBuilding => selectedBuilding;

    private static BuildingManager instance;
    public static BuildingManager Instance { get { return instance; } }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        selectedBuilding = Building.NONE;

        foreach (Building building in Enum.GetValues(typeof(Building)))
        {
            if (building == Building.NONE)
                continue;

            GameObject buildingUITextGO = Instantiate(buildingUITextPrefab, buildingButtonList.transform);
            BuildingUIText buildingUIText = buildingUITextGO.GetComponent<BuildingUIText>();
            buildingUIText.Building = building;
            buildingUIText.Name = GetBuildingName(building) + " - " + GetBuildingBaseCost(building).ToString();
            buildingUIText.Description = GetBuildingDescription(building);
            buildingUIButtons.Add(buildingUIText);
        }
    }

    public void ChangeSelectedBuilding(BuildingUIText buildingUIText)
    {
        foreach (BuildingUIText _buildingUIText in buildingUIButtons)
        {
            _buildingUIText.ChangeText(false);
        }

        buildingUIText.ChangeText(true);
        selectedBuilding = buildingUIText.Building;
    }

    [SerializeField] private TowerData[] towerDatas;
    public static string GetBuildingName(Building building)
    {
        switch (building)
        {
            case Building.NONE:             return string.Empty;
            case Building.BUILDING_BLOCK:   return "Building Block";
            case Building.DEFAULT_TOWER:    return Instance.towerDatas[0].Name;
            case Building.BOMB_TOWER:       return Instance.towerDatas[1].Name;
            case Building.LASER_TOWER:      return Instance.towerDatas[2].Name;
            case Building.MINIGUN_TOWER:    return Instance.towerDatas[3].Name;
            case Building.ROUND_TOWER:      return Instance.towerDatas[4].Name;
            case Building.GLOBAL_TOWER:     return Instance.towerDatas[5].Name;
            case Building.HINDRANCE_TOWER:  return Instance.towerDatas[6].Name;
            case Building.PULSE_TOWER:      return Instance.towerDatas[7].Name;
            //case Building.SNIPER_TOWER:     return Instance.towerDatas[4].Name;
            default: return string.Empty;
        }
    }

    public static string GetBuildingDescription(Building building)
    {
        switch (building)
        {
            case Building.NONE:             return string.Empty;
            case Building.BUILDING_BLOCK:   return "A block with 6 sides to build on.";
            case Building.DEFAULT_TOWER:    return Instance.towerDatas[0].Description;
            case Building.BOMB_TOWER:       return Instance.towerDatas[1].Description;
            case Building.LASER_TOWER:      return Instance.towerDatas[2].Description;
            case Building.MINIGUN_TOWER:    return Instance.towerDatas[3].Description;
            case Building.ROUND_TOWER:      return Instance.towerDatas[4].Description;
            case Building.GLOBAL_TOWER:     return Instance.towerDatas[5].Description;
            case Building.HINDRANCE_TOWER:  return Instance.towerDatas[6].Description;
            case Building.PULSE_TOWER:      return Instance.towerDatas[7].Description;
            //case Building.SNIPER_TOWER:     return Instance.towerDatas[4].Description;
            default: return string.Empty;
        }
    }

    public static int GetBuildingBaseCost(Building building)
    {
        switch (building)
        {
            case Building.NONE:             return 0;
            case Building.BUILDING_BLOCK:   return 50;
            case Building.DEFAULT_TOWER:    return Instance.towerDatas[0].BaseCost;
            case Building.BOMB_TOWER:       return Instance.towerDatas[1].BaseCost;
            case Building.LASER_TOWER:      return Instance.towerDatas[2].BaseCost;
            case Building.MINIGUN_TOWER:    return Instance.towerDatas[3].BaseCost;
            case Building.ROUND_TOWER:      return Instance.towerDatas[4].BaseCost;
            case Building.GLOBAL_TOWER:     return Instance.towerDatas[5].BaseCost;
            case Building.HINDRANCE_TOWER:  return Instance.towerDatas[6].BaseCost;
            case Building.PULSE_TOWER:      return Instance.towerDatas[7].BaseCost;
            //case Building.SNIPER_TOWER:     return Instance.towerDatas[4].BaseCost;
            default: return 0;
        }
    }
}

public enum Building
{
    NONE,
    BUILDING_BLOCK,
    DEFAULT_TOWER,
    BOMB_TOWER,
    LASER_TOWER,
    MINIGUN_TOWER,
    ROUND_TOWER,
    GLOBAL_TOWER,
    HINDRANCE_TOWER,
    PULSE_TOWER
    //SNIPER_TOWER
}