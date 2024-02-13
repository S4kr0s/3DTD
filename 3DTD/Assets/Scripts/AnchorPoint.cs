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

    private GameObject selectedObject
    {
        get { return BuildingManager.Instance.SelectedBuilding; }
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
        if (!renderer.enabled)
            ChangeMaterialSelected();
    }

    private void OnMouseExit()
    {
        RevertMaterialSelected();
    }

    private void ChangeMaterialSelected()
    {
        renderer.enabled = true;
    }

    private void RevertMaterialSelected()
    {
        renderer.enabled = false;
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

        if (BuildingManager.Instance.SelectedBuilding == GameManager.Instance.Buildings[0])
        {
            // Get rid of magic number
            if (GameManager.Instance.Money >= 50)
            {
                return true;
            }
            else return false;
        }
        else
        {
            // Only reflects Base Cost.
            if (GameManager.Instance.Money >= selectedObject.GetComponent<Tower>().Cost)
            {
                return true;
            }
        }
        return true;
    }

    private void BuildHere(GameObject objectToBuild)
    {
        // Only reflects Base Cost. Handle Money elsewhere.
        if(objectToBuild.TryGetComponent<Tower>(out Tower tower))
        {
            if (GameManager.Instance.Money >= (selectedObject.GetComponent<Tower>().Cost))
            {
                GameManager.Instance.Money -= (selectedObject.GetComponent<Tower>().Cost);
                Instantiate(objectToBuild, anchorPointPosition.position, this.transform.rotation);
            }
        }
        else
        {
            if (BuildingManager.Instance.SelectedBuilding == GameManager.Instance.Buildings[0])
            {
                // Get rid of magic number
                if (GameManager.Instance.Money >= 50)
                {
                    GameManager.Instance.Money -= 50;
                    Instantiate(objectToBuild, anchorPointPosition.position, this.transform.rotation);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Ray ray = new Ray(transform.parent.gameObject.transform.position, transform.forward + transform.forward);
        Gizmos.DrawRay(ray);
    }
}
