using PolygonArsenal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SocialPlatforms;

[RequireComponent(typeof(UpgradeManager))]
[RequireComponent(typeof(StatsManager))]
public class Tower : Building
{
    public UpgradeManager UpgradeManager { get { return upgradeManager; } }
    public StatsManager StatsManager { get { return statsManager; } }
    public Targetter Targetter { get { return targetter; } }
    public ActionStrategy ActionStrategy { get { return actionStrategy; } }
    public TargetBehaviour TargetBehaviour { get { return targetBehaviour; } }
    public GameObject RotationPoint { get { return rotationPoint; } }
    public ShootingPointReference[] ShootingPoints { get { return shootingPoints; } }
    public bool UseRotationSlider { get { return useRotationSlider; } }
    public GameObject Rotationbase { get { return rotationBase; } }

    public event Action<Tower> OnTowerDestroyed;

    [Header("Stats & Modules")]
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private StatsManager statsManager;
    [SerializeField] private Targetter targetter;
    [SerializeField] private ActionStrategy actionStrategy;

    [Header("Settings & Fields")]
    [SerializeField] private TargetBehaviour targetBehaviour = TargetBehaviour.FIRST;
    [SerializeField] private MeshRenderer rangeRenderer;

    [SerializeField] private GameObject rotationPoint;
    [SerializeField] private ShootingPointReference[] shootingPoints;

    [SerializeField] private bool useRotationSlider = false;
    [SerializeField] private GameObject rotationBase;

    private void Start()
    {
        rangeRenderer.enabled = false;
        actionStrategy.SetupActionStrategy(this);
        this.gameObject.name = DisplayName;
    }

    private void Update()
    {
        targetter.gameObject.transform.localScale = Vector3.one * statsManager.GetStatValue(Stat.StatType.RANGE);
        actionStrategy.ExecuteAction();
    }

    // Move to a better place? idk where tho

    /*
    public void RotateTower(float rotationValueX)
    {
        //this.rotationBase.transform.rotation = Quaternion.Euler(0, rotationValueX, 0);
        this.rotationBase.transform.localRotation = Quaternion.Euler(rotationBase.transform.rotation.x, rotationBase.transform.rotation.y, rotationValueX);
        // this.rotationBase.transform.localEulerAngles.Set(rotationBase.transform.localEulerAngles.x, rotationBase.transform.localEulerAngles.y, rotationValueX);
    }
    */

    // THIS NEEDS TO BE FIXED ASAP. Rotation way too hard. Can't get it to work on all placement directions for some reason..
    public void RotateTower(float rotationValue)
    {
        Vector3 localUp = this.rotationBase.transform.up; 
        localUp.Normalize();
        localUp = GetNearestDirection(localUp);

        Debug.Log(localUp);
        Debug.Log("EULER: " + this.rotationBase.transform.rotation.ToString());

        if (localUp == Vector3.right)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(rotationValue, 0, -90);
        }
        else if (localUp == Vector3.up)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(0, rotationValue, 0);
        }
        else if (localUp == Vector3.forward)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(rotationValue, 90, 90);
        }
        else
        if (localUp == -Vector3.right)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(rotationValue, 0, 90);
        }
        else if (localUp == -Vector3.up)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(180, rotationValue, 0);
        }
        else if (localUp == -Vector3.forward)
        {
            this.rotationBase.transform.rotation = Quaternion.Euler(rotationValue, 90, -90);
        }

        Debug.Log("EULER: " + this.rotationBase.transform.rotation.ToString());
    }

    public Vector3 GetNearestDirection(Vector3 localUp)
    {
        // Define the candidate directions
        Vector3[] directions = {
            Vector3.right, Vector3.up, Vector3.forward,
            -Vector3.right, -Vector3.up, -Vector3.forward
        };

        // Initialize variables to find the nearest direction
        float maxDot = float.MinValue;
        Vector3 nearestDirection = Vector3.zero;

        // Check each direction to find the nearest one
        foreach (Vector3 dir in directions)
        {
            float dot = Vector3.Dot(localUp, dir);
            if (dot > maxDot)
            {
                maxDot = dot;
                nearestDirection = dir;
            }
        }

        return nearestDirection;
    }

    /*
    private void ShootAtTarget()
    {
        foreach (GameObject shootingPoint in shootingPoints)
        {
            if (internalFireRate <= 0 && target != null)
            {
                internalFireRate = GetFireRate();
                GameObject _projectile = Instantiate(projectile, shootingPoint.transform.position, shootingPoint.transform.rotation);
                _projectile.GetComponent<Projectile>().Target = target;
                _projectile.GetComponent<Projectile>().lifetime = 2.5f;
                _projectile.GetComponent<Projectile>().penetration = TowerData.BasePenetration;
                _projectile.transform.position = shootingPoint.transform.position;
                _projectile.transform.rotation = shootingPoint.transform.rotation;
                _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
            }
            else
            {
                internalFireRate -= Time.deltaTime;
            }
        }
    }
    */

    public void ChangeTargettingBehaviour(TargetBehaviour targetBehaviour)
    {
        this.targetBehaviour = targetBehaviour;
    }

    public void MouseEnter()
    {
        rangeRenderer.enabled = true;
    }

    public void MouseExit()
    {
        rangeRenderer.enabled = false;
    }

    public void SetActionStrategy(ActionStrategy actionStrategy)
    {
        Destroy(this.actionStrategy);
        this.actionStrategy = actionStrategy;
        this.actionStrategy.SetupActionStrategy(this);
    }

    public void SetRotationPoint(GameObject target)
    {
        if (target == null)
        {
            rotationPoint = target;
        }
    }
}
