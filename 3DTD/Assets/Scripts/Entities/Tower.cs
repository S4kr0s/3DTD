using PolygonArsenal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(UpgradeManager))]
[RequireComponent(typeof(StatsManager))]
public class Tower : MonoBehaviour
{
    public string DisplayName { get { return displayName; } }
    public string Description { get { return description; } }
    public UpgradeManager UpgradeManager { get { return upgradeManager; } }
    public StatsManager StatsManager { get { return statsManager; } }
    public TargetBehaviour TargetBehaviour { get { return targetBehaviour; } }

    public event Action<Tower> OnTowerDestroyed;

    [Header("Information")]
    [SerializeField] private string displayName;
    [SerializeField] private string description;

    [Header("Stats & Modules")]
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private StatsManager statsManager;
    [SerializeField] private Targetter targetter;

    [Header("Settings & Fields")]
    [SerializeField] private TargetBehaviour targetBehaviour = TargetBehaviour.FIRST;
    [SerializeField] private new MeshRenderer renderer;

    [SerializeField] private GameObject rotationPoint;
    [SerializeField] private bool alternateRotation = false;
    [SerializeField] private bool doNotRotate = false;
    [SerializeField] private GameObject[] shootingPoints;
    [SerializeField] private bool shootAtSameTime = false;
    [SerializeField] private GameObject target;

    [SerializeField] private GameObject projectile;
    [SerializeField] private float internalFireRate;

    private ProjectilePoolManager projectilePoolManager;

    private void Start()
    {
        renderer.enabled = false;
        internalFireRate = statsManager.GetStatValue(Stat.StatType.FIRERATE);

        // Object Pool
        projectilePoolManager = gameObject.AddComponent<ProjectilePoolManager>();
        projectilePoolManager.Setup(projectile, Mathf.RoundToInt(10 / internalFireRate * shootingPoints.Length));
    }

    private void Update()
    {
        Enemy enemy = targetter.GetEnemy(targetBehaviour);

        if (enemy != null)
        {
            target = enemy.gameObject;

            if (!doNotRotate)
            {
                if (alternateRotation)
                    rotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);
                else
                    rotationPoint.transform.LookAt(enemy.transform.position);
            }
        }
        else
            target = null;

        ShootAtTarget();
    }

    public bool CanShoot(GameObject enemy)
    {
        if (!doNotRotate)
        {
            if (alternateRotation)
                rotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);
            else
                rotationPoint.transform.LookAt(enemy.transform.position);
        }

        foreach (GameObject shootingPoint in shootingPoints)
        {
            if (shootingPoint.activeSelf)
            {
                Vector3 raycastDirection = enemy.transform.position - shootingPoint.transform.position;
                if (Physics.Linecast(shootingPoint.transform.position, enemy.transform.position, out RaycastHit hit))
                {
                    if (hit.collider.gameObject == enemy)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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

    private void ShootAtTarget()
    {
        if (shootAtSameTime)
        {
            if (internalFireRate <= 0 && target != null)
            {
                foreach (GameObject shootingPoint in shootingPoints)
                {
                    internalFireRate = statsManager.GetStatValue(Stat.StatType.FIRERATE);

                    GameObject _projectile = projectilePoolManager.GetPooledProjectile();

                    if (_projectile == null)
                    {
                        continue;
                    }

                    _projectile.SetActive(false);
                    _projectile.transform.position = shootingPoint.transform.position;
                    _projectile.transform.rotation = shootingPoint.transform.rotation;

                    Projectile projectileComponent = _projectile.GetComponent<Projectile>();
                    projectileComponent.Target = target;
                    projectileComponent.lifetime = statsManager.GetStatValue(Stat.StatType.DAMAGE);
                    projectileComponent.damage = statsManager.GetStatValue(Stat.StatType.DAMAGE);
                    projectileComponent.penetration = ((int)statsManager.GetStatValue(Stat.StatType.PIERCING));
                    projectileComponent.OnProjectileDeath += ReturnToPool;
                    _projectile.SetActive(true);

                    _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
                }
            }
            else
            {
                internalFireRate -= Time.deltaTime;
            }
        }
        else
        {
            foreach (GameObject shootingPoint in shootingPoints)
            {
                if (internalFireRate <= 0 && target != null)
                {
                    internalFireRate = statsManager.GetStatValue(Stat.StatType.FIRERATE);

                    GameObject _projectile = projectilePoolManager.GetPooledProjectile();

                    if (_projectile == null)
                    {
                        continue;
                    }

                    _projectile.SetActive(false);
                    _projectile.transform.position = shootingPoint.transform.position;
                    _projectile.transform.rotation = shootingPoint.transform.rotation;

                    Projectile projectileComponent = _projectile.GetComponent<Projectile>();
                    projectileComponent.Target = target;
                    projectileComponent.lifetime = statsManager.GetStatValue(Stat.StatType.FIRERATE);
                    projectileComponent.damage = statsManager.GetStatValue(Stat.StatType.DAMAGE);
                    projectileComponent.penetration = ((int)statsManager.GetStatValue(Stat.StatType.PIERCING));
                    projectileComponent.OnProjectileDeath += ReturnToPool;
                    _projectile.SetActive(true);

                    _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
                }
                else
                {
                    internalFireRate -= Time.deltaTime;
                }
            }
        }
    }

    public void ChangeTargettingBehaviour(TargetBehaviour targetBehaviour)
    {
        this.targetBehaviour = targetBehaviour;
    }

    public void MouseEnter()
    {
        renderer.enabled = true;
    }

    public void MouseExit()
    {
        renderer.enabled = false;
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.GetComponent<Projectile>().OnProjectileDeath -= ReturnToPool;
        projectilePoolManager.ReturnToPool(obj);
    }
}
