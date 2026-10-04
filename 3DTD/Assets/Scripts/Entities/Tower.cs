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
    public float DamageCount = 0f;
    // Enemies this tower finished off (the final layer)
    public int Kills { get; private set; }

    // 1 for a fresh tower, +1 for every tier reached on its furthest upgrade path (tier pips in the panel)
    public int Tier
    {
        get
        {
            int highest = 0;
            foreach (UpgradePath path in upgradeManager.GetUpgradePaths())
            {
                if (path != null)
                    highest = Mathf.Max(highest, path.activeUpgrades);
            }
            return 1 + highest;
        }
    }

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
        if (GameManager.Instance != null && !GameManager.Instance.IsMainMenu)
            MetaUpgrades.ApplyTo(statsManager);
        actionStrategy.SetupActionStrategy(this);
        this.gameObject.name = DisplayName;
    }

    private float appliedRange = -1f;

    private void Update()
    {
        // Rescaling the trigger every frame forces a physics shape update, so only do it when RANGE changes
        float range = statsManager.GetStatValue(Stat.StatType.RANGE);
        if (range != appliedRange)
        {
            appliedRange = range;
            targetter.gameObject.transform.localScale = Vector3.one * range;
        }
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
        rangeRenderer.enabled = GameOptions.RangeOnHover;
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
        if (target != null)
        {
            rotationPoint = target;
        }
    }

    public void HandleDamageDealt(float damage)
    {
        DamageCount += damage;
    }

    public void HandleKill()
    {
        Kills++;
    }

    // World-space radius of the targetting sphere for a RANGE value: the targetter is scaled by RANGE,
    // so the radius is the collider's own radius times RANGE times the tower's scale. Works on prefabs too.
    public float GetWorldRangeRadius(float range)
    {
        float localRadius = 1f;
        Collider rangeCollider = targetter != null ? targetter.Collider : null;
        if (rangeCollider is SphereCollider sphere)
            localRadius = sphere.radius;
        else if (rangeCollider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            localRadius = Mathf.Max(meshCollider.sharedMesh.bounds.extents.x, meshCollider.sharedMesh.bounds.extents.z);

        float parentScale = targetter != null && targetter.transform.parent != null ? targetter.transform.parent.lossyScale.x : transform.lossyScale.x;
        return localRadius * range * parentScale;
    }

    // Radius where the targetting sphere meets the tower's base plane. Equal to the world radius for
    // half-sphere targetters; smaller for the floating full spheres of the Hangar and the Mine Factory.
    public float GetGroundRangeRadius(float range)
    {
        float radius = GetWorldRangeRadius(range);
        if (targetter == null)
            return radius;
        // Buildings are spawned with their anchor's rotation, so forward is the face normal
        float height = Vector3.Dot(targetter.transform.position - transform.position, transform.forward);
        return Mathf.Sqrt(Mathf.Max(0f, radius * radius - height * height));
    }
}
