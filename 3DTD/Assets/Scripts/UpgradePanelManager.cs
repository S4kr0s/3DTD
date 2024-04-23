using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Michsky.UI.ModernUIPack;
using UnityEngine.UI;
using System;

public class UpgradePanelManager : MonoBehaviour
{
    [SerializeField] private Image parentBackground;
    [SerializeField] private TMP_Text title;
    [SerializeField] private HorizontalSelector targettingSelector;
    [SerializeField] private Slider rotationSlider;
    [SerializeField] private TMPro.TMP_Text damage;
    [SerializeField] private TMPro.TMP_Text fireRate;
    [SerializeField] private TMPro.TMP_Text pierce;
    [SerializeField] private TMPro.TMP_Text range;
    [SerializeField] private TMPro.TMP_Text criticalChance;
    [SerializeField] private TMPro.TMP_Text sellValue;
    [SerializeField] private TMPro.TMP_Text ammo;
    [SerializeField] private TMPro.TMP_Text reloadSpeed;
    [SerializeField] private TMPro.TMP_Text accuracy;
    [SerializeField] private TMPro.TMP_Text damageDealt;

    [SerializeField] private UpgradeUIButton upgradeUI;
    [SerializeField] private GameObject perkUI;
    [SerializeField] private GameObject perkList;
    [SerializeField] private GameObject sellUI;

    private List<GameObject> upgrades = new List<GameObject>();
    private List<GameObject> perkObjects = new List<GameObject>();

    private static UpgradePanelManager instance;
    public static UpgradePanelManager Instance { get { return instance; } }

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
    }

    private void Start()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChanged;

        CheckReferences();
    }

    private void OnDestroy()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChanged;
    }

    public void HandleSelectionChanged(Selectable oldSelection, Selectable newSelection)
    {
        if (newSelection != null && newSelection.gameObject.TryGetComponent<Tower>(out Tower tower))
        {
            ClearUI();
            SetNewUI(tower);
        }
        else if (newSelection != null && newSelection.gameObject.TryGetComponent<BuildingBlock>(out BuildingBlock buildingBlock))
        {
            ClearUI();
            SetNewUI(buildingBlock);
        }
        else
        {
            ClearUI();
        }
    }

    public void ClearSelection()
    {
        SelectionManager.CurrentlySelected = null;
    }

    private void SetNewUI(Tower tower)
    {
        ActivateUI();

        title.text = tower.name;
        targettingSelector.index = (int)tower.TargetBehaviour;
        targettingSelector.itemList[(int)tower.TargetBehaviour].onValueChanged.Invoke();
        targettingSelector.selectorEvent.Invoke((int)tower.TargetBehaviour);
        targettingSelector.UpdateUI();

        if (tower.UseRotationSlider)
        {
            rotationSlider.gameObject.SetActive(true);
            rotationSlider.value = tower.Rotationbase.transform.eulerAngles.y;
            rotationSlider.onValueChanged.AddListener(tower.RotateTower);
        }

        // Only respects base stats!
        damage.text = $"{ Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE),2) }";
        fireRate.text = $"{ Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE), 2) }";
        pierce.text = $"{ Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.PIERCING),2) }";
        range.text = $"{ Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.RANGE), 2) }";
        criticalChance.text = $"0%";
        sellValue.text = $"{ tower.Cost }";
        ammo.text = $"{Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.AMMO), 2)}";
        if (ammo.text == "0") ammo.text = "1";
        reloadSpeed.text = $"{Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.RELOAD_SPEED), 2)}";
        if (reloadSpeed.text == "0") reloadSpeed.text = $"{Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE), 2)}";
        accuracy.text = $"{Math.Round(tower.StatsManager.GetStatValue(Stat.StatType.ACCURACY), 2) * 100}%";
        damageDealt.text = $"{tower.DamageCount}";

        // Upgrade(s) now here
        tower.UpgradeManager.CheckPathBlocking();
        foreach (UpgradePath upgradePath in tower.UpgradeManager.GetUpgradePaths())
        {
            UpgradeUIButton _setup = Instantiate(upgradeUI.gameObject, this.gameObject.transform).GetComponent<UpgradeUIButton>();
            upgrades.Add(_setup.gameObject);
            _setup.SetColor(3);
            int count = 0;
            foreach (UpgradeModule upgradeModule in upgradePath.UpgradeModules)
            {
                if (!upgradeModule.IsActive) 
                {
                    _setup.Setup(upgradeModule, count);
                    if (!upgradeModule.isAvailable)
                    {
                        _setup.gameObject.GetComponent<Button>().interactable = false;
                    }
                    break;
                }
                else
                {
                    count++;
                    TooltipContent instance = Instantiate(perkUI, perkList.transform).GetComponent<TooltipContent>();
                    instance.description = upgradeModule.Description;
                    perkObjects.Add(instance.gameObject);
                }
            }
        }

        if (perkObjects.Count > 0)
            perkList.SetActive(true);
        else
            perkList.SetActive(false);

        // Sell function as last
        //sellUI.transform.SetAsLastSibling();
        perkList.transform.SetAsLastSibling();
    }

    private void SetNewUI(BuildingBlock buildingBlock)
    {
        ClearUI();
        parentBackground.enabled = true;
        title.enabled = true;
        title.text = "Building Block";
    }

    public void ClearUI()
    {
        parentBackground.enabled = false;
        title.enabled = false;
        targettingSelector.gameObject.SetActive(false);
        rotationSlider.onValueChanged.RemoveAllListeners();
        rotationSlider.gameObject.SetActive(false);
        damage.enabled = false;
        fireRate.enabled = false;
        pierce.enabled = false;

        if (upgrades.Count > 0)
        {
            foreach (GameObject upgrade in upgrades)
            {
                Destroy(upgrade.gameObject);
            }
            upgrades.Clear();
        }

        if (perkObjects.Count > 0)
        {
            foreach (GameObject perk in perkObjects)
            {
                Destroy(perk.gameObject);
            }
            perkObjects.Clear();
        }
    }

    private void ActivateUI()
    {
        parentBackground.enabled = true;
        title.enabled = true;
        targettingSelector.gameObject.SetActive(true);
        damage.enabled = true;
        fireRate.enabled = true;
        pierce.enabled = true;
        perkList.SetActive(true);
    }

    public void DeactivateUI()
    {
        title.enabled = false;
        targettingSelector.gameObject.SetActive(false);
        damage.enabled = false;
        fireRate.enabled = false;
        pierce.enabled = false;
        parentBackground.enabled = false;
        perkList.SetActive(false);
    }

    private void CheckReferences()
    {
        if (parentBackground == null)
            parentBackground = this.gameObject.GetComponent<Image>();

        if (title == null)
            title = GameObject.Find("Title").GetComponent<TMP_Text>();

        if (targettingSelector == null)
            targettingSelector = GameObject.Find("Horizontal Selector").GetComponent<HorizontalSelector>();

        if (damage == null)
            damage = GameObject.Find("DamageText").GetComponent<TMP_Text>();

        if (pierce == null)
            pierce = GameObject.Find("PierceText").GetComponent<TMP_Text>();

        if (fireRate == null)
            fireRate = GameObject.Find("FireRateText").GetComponent<TMP_Text>();
    }
}
