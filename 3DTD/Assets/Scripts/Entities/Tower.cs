using PolygonArsenal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

public class Tower : MonoBehaviour
{
    public TargetBehaviour TargetBehaviour { get { return targetBehaviour; } }

    public UpgradeData[] Upgrades { get { return upgrades; } }
    public UpgradeData[] ActiveUpgrades { get { return activeUpgrades; } }

    [SerializeField] private UpgradeData[] upgrades;
    [SerializeField] private UpgradeData[] activeUpgrades;

    private float _realDamage;
    public float UpdateAndGetDamage 
    { 
        get 
        {
            _realDamage = TowerData.BaseDamage;
            foreach (UpgradeData upgrade in ActiveUpgrades)
                _realDamage += upgrade.DamageModifier;
            return _realDamage;
        } 
    }
    public float GetDamage { get { return _realDamage; } }

    private float _realFireRate;
    public float UpdateAndGetFireRate
    {
        get
        {
            _realFireRate = TowerData.BaseFireRate;
            foreach (UpgradeData upgrade in ActiveUpgrades)
                _realFireRate += upgrade.FireRateModifier;
            return _realFireRate;
        }
    }
    public float GetFireRate { get { return _realFireRate; } }

    private int _realPenetration;
    public int UpdateAndGetPenetration
    {
        get
        {
            _realPenetration = TowerData.BasePenetration;
            foreach (UpgradeData upgrade in ActiveUpgrades)
                _realPenetration += upgrade.PenetrationModifier;
            return _realPenetration;
        }
    }
    public int GetPenetration { get { return _realPenetration; } }

    public float GetLifetime { get { return TowerData.Lifetime; } }

    [SerializeField] private Targetter targetter;
    [SerializeField] private TargetBehaviour targetBehaviour = TargetBehaviour.FIRST;
    [SerializeField] private new MeshRenderer renderer;

    [SerializeField] private TowerData data;
    [SerializeField] private GameObject rotationPoint;
    [SerializeField] private bool alternateRotation = false;
    [SerializeField] private bool doNotRotate = false;
    [SerializeField] private GameObject[] shootingPoints;
    [SerializeField] private bool shootAtSameTime = false;
    [SerializeField] private GameObject target;

    [SerializeField] private GameObject projectile;
    [SerializeField] private float internalFireRate;

    public event Action<Tower> OnTowerDestroyed;

    [Space]
    [SerializeField] private int level = 0;
    public TowerData TowerData { get { return data; } }
    public int Level { get { return level; } }

    private ProjectilePoolManager projectilePoolManager;

    private void Start()
    {
        renderer.enabled = false;
        internalFireRate = UpdateAndGetFireRate;
        _realDamage = UpdateAndGetDamage;
        _realPenetration = UpdateAndGetPenetration;

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
                    internalFireRate = GetFireRate;

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
                    projectileComponent.lifetime = GetLifetime;
                    projectileComponent.damage = GetDamage;
                    projectileComponent.penetration = GetPenetration;
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
                    internalFireRate = GetFireRate;

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
                    projectileComponent.lifetime = GetLifetime;
                    projectileComponent.damage = GetDamage;
                    projectileComponent.penetration = GetPenetration;
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
