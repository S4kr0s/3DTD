﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private bool isSelected = false;

    private void Start()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChange;
    }

    private void Update()
    {
        if (isSelected)
        {
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                SellThisTower();
            }
        }
    }

    public void SelectThis()
    {
        SelectionManager.CurrentlySelected = this;
    }

    public void Select()
    {
        Debug.Log(gameObject.name + " selected.");
    }

    public void Deselect()
    {
        Debug.Log(gameObject.name + " deselected.");
    }

    private void HandleSelectionChange(Selectable oldSelection, Selectable newSelection)
    {
        if (oldSelection == this)
        {
            isSelected = false;
        }

        if (newSelection == this)
        {
            isSelected = true;
        }
    }

    public void SellThisTower()
    {
        if (this.gameObject.TryGetComponent<Tower>(out Tower tower))
        {
            GameManager.Instance.Money += (tower.Cost);
            Destroy(this.gameObject);
            UpgradePanelManager.Instance.ClearUI();
        }

        if (this.gameObject.TryGetComponent<BuildingBlock>(out BuildingBlock buildingBlock))
        {
            bool hitSomething = false;

            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }

            ray = new Ray(transform.position, -transform.forward);
            if (Physics.Raycast(ray, out hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }


            ray = new Ray(transform.position, transform.up);
            if (Physics.Raycast(ray, out hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }

            ray = new Ray(transform.position, -transform.up);
            if (Physics.Raycast(ray, out hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }

            ray = new Ray(transform.position, transform.right);
            if (Physics.Raycast(ray, out hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }

            ray = new Ray(transform.position, -transform.right);
            if (Physics.Raycast(ray, out hit, 1f, layerMask))
            {
                if (hit.collider.gameObject.TryGetComponent<Tower>(out Tower hitTower))
                {
                    hitSomething = true;
                }
            }

            if (!hitSomething)
            {
                // HARD CODED! GET RID OF MAGIC NUMBER!
                GameManager.Instance.Money += 50;
                Destroy(this.gameObject);
                UpgradePanelManager.Instance.ClearUI();
            }
        }
    }

    public void UpgradeThisTower(UpgradeModule upgradeModule)
    {
        if (upgradeModule == null)
            return;

        this.gameObject.GetComponent<UpgradeManager>().ActivateUpgradeModule(upgradeModule);
    }
}
