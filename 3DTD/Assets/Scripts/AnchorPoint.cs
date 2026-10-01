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
        get { return BuildingManager.Instance.GetSelectedBuilding(); }
    }

    public Transform AnchorPointPosition => anchorPointPosition;
    public LayerMask LayerMask => layerMask;

    private new Renderer renderer;

    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        renderer.enabled = false;
    }

    private void OnMouseDown()
    {
        if(CanBuildHere() && selectedObject != null)
            BuildHere(selectedObject);
    }

    private void OnMouseOver()
    {
        ChangeMaterialSelected();
    }

    private void OnMouseExit()
    {
        RevertMaterialSelected();
    }

    private void ChangeMaterialSelected()
    {
        outlinable.OutlineParameters.Enabled = true;
    }

    private void RevertMaterialSelected()
    {
        outlinable.OutlineParameters.Enabled = false;
    }

    private bool CanBuildHere()
    {
        if (selectedObject == null)
            return false;

        Ray ray = new Ray(transform.parent.gameObject.transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 1f, layerMask))
        {
            return false;
        }

        return GameManager.Instance.Money >= GetBuildCost(selectedObject);
    }

    private void BuildHere(GameObject objectToBuild)
    {
        int buildCost = GetBuildCost(objectToBuild);

        if (GameManager.Instance.Money >= buildCost)
        {
            GameManager.Instance.Money -= buildCost;
            Instantiate(objectToBuild, anchorPointPosition.position, this.transform.rotation);
        }
    }

    private int GetBuildCost(GameObject objectToBuild)
    {
        if (objectToBuild != null && objectToBuild.TryGetComponent<Building>(out Building building))
            return building.Cost;

        return int.MaxValue;
    }

    private void OnDrawGizmos()
    {
        Ray ray = new Ray(transform.parent.gameObject.transform.position, transform.forward + transform.forward);
        Gizmos.DrawRay(ray);
    }
}
