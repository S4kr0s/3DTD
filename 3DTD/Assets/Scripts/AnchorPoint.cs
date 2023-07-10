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
        get
        {
            switch (BuildingManager.Instance.SelectedBuilding)
            {
                case Building.NONE:
                    return null;
                case Building.BUILDING_BLOCK:
                    return BuildingBlock;
                case Building.DEFAULT_TOWER:
                    return DefaultTower;
                case Building.BOMB_TOWER:
                    return BombTurret;
                case Building.LASER_TOWER:
                    return LaserTurret;
                case Building.MINIGUN_TOWER:
                    return MinigunTurret;
                case Building.ROUND_TOWER:
                    return RoundTower;
                case Building.GLOBAL_TOWER:
                    return GlobalTower;
                case Building.HINDRANCE_TOWER:
                    return HindranceTower;
                case Building.PULSE_TOWER:
                    return PulseTower;
                //case Building.SNIPER_TOWER:
                //    return SniperTurret;
                default:
                    return null;
            }
        }
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

        if (BuildingManager.Instance.SelectedBuilding == Building.BUILDING_BLOCK)
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
            if (GameManager.Instance.Money >= selectedObject.GetComponent<Tower>().TowerData.BaseCost)
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
            if (GameManager.Instance.Money >= tower.TowerData.BaseCost)
            {
                GameManager.Instance.Money -= tower.TowerData.BaseCost;
                Instantiate(objectToBuild, anchorPointPosition.position, this.transform.rotation);
            }
        }
        else
        {
            if (BuildingManager.Instance.SelectedBuilding == Building.BUILDING_BLOCK)
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
