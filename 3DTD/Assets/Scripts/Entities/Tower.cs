using PolygonArsenal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

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
    public GameObject[] ShootingPoints { get { return shootingPoints; } }

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
    [SerializeField] private GameObject[] shootingPoints;

    private void Start()
    {
        rangeRenderer.enabled = false;
        actionStrategy.SetupActionStrategy(this);
    }

    private void Update()
    {
        actionStrategy.ExecuteAction();
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
        this.actionStrategy = actionStrategy;
    }
}
