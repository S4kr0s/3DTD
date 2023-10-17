using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Michsky.UI.ModernUIPack;
using UnityEngine.UI;

public class UpgradePanelManager : MonoBehaviour
{
    [SerializeField] private Image parentBackground;
    [SerializeField] private TMP_Text title;
    [SerializeField] private HorizontalSelector targettingSelector;
    [SerializeField] private TMPro.TMP_Text damage;
    [SerializeField] private TMPro.TMP_Text fireRate;
    [SerializeField] private TMPro.TMP_Text pierce;

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

    private void HandleSelectionChanged(Selectable oldSelection, Selectable newSelection)
    {
        if (newSelection.gameObject.TryGetComponent<Tower>(out Tower tower))
        {
            ClearUI();
            SetNewUI(tower);
        }
        else if (newSelection.gameObject.TryGetComponent<BuildingBlock>(out BuildingBlock buildingBlock))
        {
            ClearUI();
            SetNewUI(buildingBlock);
        }
        else
        {
            ClearUI();
        }
    }

    private void SetNewUI(Tower tower)
    {
        ActivateUI();

        title.text = tower.name;
        targettingSelector.index = (int)tower.TargetBehaviour;
        targettingSelector.itemList[(int)tower.TargetBehaviour].onValueChanged.Invoke();
        targettingSelector.selectorEvent.Invoke((int)tower.TargetBehaviour);

        // Only respects base stats!
        damage.text = $"{tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE)}";
        fireRate.text = $"{tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE)}";
        pierce.text = $"{tower.StatsManager.GetStatValue(Stat.StatType.PIERCING)}";

        // Upgrade(s) now here


        // Sell function as last

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
        damage.enabled = false;
        fireRate.enabled = false;
        pierce.enabled = false;
    }

    private void ActivateUI()
    {
        parentBackground.enabled = true;
        title.enabled = true;
        targettingSelector.gameObject.SetActive(true);
        damage.enabled = true;
        fireRate.enabled = true;
        pierce.enabled = true;
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
