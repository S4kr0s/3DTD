using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Michsky.UI.ModernUIPack;
using UnityEngine.UI;

public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private Image parentBackground;
    [SerializeField] private TMP_Text title;
    [SerializeField] private HorizontalSelector targettingSelector;
    [SerializeField] private TMPro.TMP_Text damage;
    [SerializeField] private TMPro.TMP_Text fireRate;
    [SerializeField] private TMPro.TMP_Text pierce;

    private static UpgradeManager instance;
    public static UpgradeManager Instance { get { return instance; } }

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
        damage.text = $"Damage:      {tower.TowerData.BaseDamage}";
        fireRate.text = $"FireRate:      {tower.TowerData.BaseFireRate}";
        pierce.text = $"Pierce:      {tower.TowerData.BasePenetration}";

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
}
