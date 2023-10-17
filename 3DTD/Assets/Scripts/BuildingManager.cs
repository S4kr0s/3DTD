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
    [Header("Bad Code, add pictures IN ORDER of enum")]
    [SerializeField] private List<Sprite> spriteList;

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
            buildingUIText.Name = GetBuildingName(building);
            buildingUIText.Cost = GetBuildingBaseCost(building).ToString() + "$";
            buildingUIText.Description = GetBuildingDescription(building);
            buildingUIText.sprite = spriteList[(int)building];
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

    [SerializeField] private StatsScriptableObject[] statsConfigs;
    public static string GetBuildingName(Building building)
    {
        switch (building)
        {
            case Building.NONE:             return string.Empty;
            case Building.BUILDING_BLOCK:   return "Building Block";
            case Building.DEFAULT_TOWER:    return "Undefined";
            case Building.BOMB_TOWER:       return "Undefined";
            case Building.LASER_TOWER:      return "Undefined";
            case Building.MINIGUN_TOWER:    return "Undefined";
            case Building.ROUND_TOWER:      return "Undefined";
            case Building.GLOBAL_TOWER:     return "Undefined";
            case Building.HINDRANCE_TOWER:  return "Undefined";
            case Building.PULSE_TOWER:      return "Undefined";
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
            case Building.DEFAULT_TOWER:    return "Undefined";
            case Building.BOMB_TOWER:       return "Undefined";
            case Building.LASER_TOWER:      return "Undefined";
            case Building.MINIGUN_TOWER:    return "Undefined";
            case Building.ROUND_TOWER:      return "Undefined";
            case Building.GLOBAL_TOWER:     return "Undefined";
            case Building.HINDRANCE_TOWER:  return "Undefined";
            case Building.PULSE_TOWER:      return "Undefined";
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
            case Building.DEFAULT_TOWER:    return 0;
            case Building.BOMB_TOWER:       return 0;
            case Building.LASER_TOWER:      return 0;
            case Building.MINIGUN_TOWER:    return 0;
            case Building.ROUND_TOWER:      return 0;
            case Building.GLOBAL_TOWER:     return 0;
            case Building.HINDRANCE_TOWER:  return 0;
            case Building.PULSE_TOWER:      return 0;
            //case Building.SNIPER_TOWER:     return Instance.towerDatas[4].BaseCost;
            default: return 0;
        }
    }
}

public enum Building
{
    NONE = 0,
    BUILDING_BLOCK = 1,
    DEFAULT_TOWER = 2,
    BOMB_TOWER = 3,
    LASER_TOWER = 4,
    MINIGUN_TOWER = 5,
    ROUND_TOWER = 6,
    GLOBAL_TOWER = 7,
    HINDRANCE_TOWER = 8,
    PULSE_TOWER = 9
    //SNIPER_TOWER
}