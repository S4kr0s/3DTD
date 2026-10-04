using EPOOutline;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorPoint : MonoBehaviour
{
    [SerializeField] private GameObject BuildingBlock;
    [SerializeField] private GameObject DefaultTower;
    [SerializeField] private GameObject BombTurret;
    [SerializeField] private GameObject LaserTurret;
    [SerializeField] private GameObject MinigunTurret;
    [SerializeField] private GameObject SniperTurret;
    [SerializeField] private GameObject RoundTower;
    [SerializeField] private GameObject GlobalTower;
    [SerializeField] private GameObject HindranceTower;
    [SerializeField] private GameObject PulseTower;
    [SerializeField] private Transform anchorPointPosition;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Outlinable outlinable;

    private GameObject selectedObject
    {
        get { return BuildingManager.Instance != null ? BuildingManager.Instance.GetSelectedBuilding() : null; }
    }

    public Transform AnchorPointPosition => anchorPointPosition;
    public LayerMask LayerMask => layerMask;

    private new Renderer renderer;

    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        renderer.enabled = false;
        OutlineSwitch.SetOutline(outlinable, false);
    }

    private void OnMouseDown()
    {
        // OnMouseDown also fires through the HUD; only clicks that reach the world may build
        if (IsPointerOverUI())
            return;

        if(CanBuildHere() && selectedObject != null)
            BuildHere(selectedObject);
    }

    private void OnMouseOver()
    {
        bool hover = selectedObject != null && !IsPointerOverUI();
        if (hover)
            ChangeMaterialSelected();
        else
            RevertMaterialSelected();

        if (PlacementPreview.Instance != null)
        {
            if (hover)
                PlacementPreview.Instance.Hover(this, IsFree(), CanAfford());
            else
                PlacementPreview.Instance.Unhover(this);
        }
    }

    private void OnMouseExit()
    {
        RevertMaterialSelected();
        if (PlacementPreview.Instance != null)
            PlacementPreview.Instance.Unhover(this);
    }

    private static bool IsPointerOverUI()
    {
        return UIPointer.IsOverUI();
    }

    private void ChangeMaterialSelected()
    {
        OutlineSwitch.SetOutline(outlinable, true);
    }

    private void RevertMaterialSelected()
    {
        OutlineSwitch.SetOutline(outlinable, false);
    }

    private bool CanBuildHere()
    {
        if (selectedObject == null)
            return false;

        return IsFree() && CanAfford();
    }

    // Nothing is attached to this face yet
    public bool IsFree()
    {
        Ray ray = new Ray(transform.parent.gameObject.transform.position, transform.forward);
        return !Physics.Raycast(ray, out RaycastHit hit, 1f, layerMask);
    }

    private bool CanAfford()
    {
        return selectedObject != null && GameManager.Instance.Money >= GetBuildCost(selectedObject);
    }

    private void BuildHere(GameObject objectToBuild)
    {
        int buildCost = GetBuildCost(objectToBuild);

        if (GameManager.Instance.Money >= buildCost)
        {
            GameManager.Instance.Money -= buildCost;
            GameObject built = Instantiate(objectToBuild, anchorPointPosition.position, this.transform.rotation);
            if (built.TryGetComponent<Building>(out Building building))
            {
                building.AddInvestment(buildCost);
                building.MarkPlacedByPlayer(BuildingManager.Instance.SelectedIndex);
            }
            if (PlacementPreview.Instance != null)
                PlacementPreview.Instance.Unhover(this);
        }
    }

    private int GetBuildCost(GameObject objectToBuild)
    {
        if (objectToBuild != null && objectToBuild.TryGetComponent<Building>(out Building building))
            return GameManager.Instance.Price(building.Cost);

        return int.MaxValue;
    }

    private void OnDrawGizmos()
    {
        Ray ray = new Ray(transform.parent.gameObject.transform.position, transform.forward + transform.forward);
        Gizmos.DrawRay(ray);
    }
}
