using PolygonArsenal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class Tower : MonoBehaviour
{
    public TargetBehaviour TargetBehaviour { get { return targetBehaviour; } }

    [SerializeField] private Targetter targetter;
    [SerializeField] private TargetBehaviour targetBehaviour = TargetBehaviour.FIRST;
    [SerializeField] private new MeshRenderer renderer;

    [SerializeField] private TowerData data;
    [SerializeField] private GameObject rotationPoint;
    [SerializeField] private GameObject[] shootingPoints;
    [SerializeField] private GameObject target;

    [SerializeField] private GameObject projectile;
    private float internalFireRate;

    public event Action<Tower> OnTowerDestroyed;

    private ObjectPool<GameObject> projectilePool;

    [Space]
    [SerializeField] private int level = 0;
    public TowerData TowerData { get { return data; } }
    public int Level { get { return level; } }


    private void Start()
    {
        renderer.enabled = false;
        internalFireRate = GetFireRate();

        //ObjectPool
        /*
        projectilePool = new ObjectPool<GameObject>(() =>
        {
            GameObject gameObject = Instantiate(projectile);
            gameObject.GetComponent<Projectile>().OnProjectileDeath += ProjectilePool_OnProjectileDeath;
            return gameObject;
        }, 
        _projectile =>
        {
            _projectile.transform.localScale = Vector3.one;
            _projectile.SetActive(true);
        }, 
        _projectile =>
        {
            _projectile.SetActive(false);
        }, 
        _projectile =>
        {
            Destroy(_projectile);
        }, false, (int)(GetFireRate() * shootingPoints.Length * 20), (int)(GetFireRate() * shootingPoints.Length * 30));
        */
    }

    private void ProjectilePool_OnProjectileDeath(GameObject obj)
    {
        //projectilePool.Release(obj);
        Destroy(obj);
    }

    private void Update()
    {
        Enemy enemy = targetter.GetEnemy(targetBehaviour);

        if (enemy != null)
        {
            target = enemy.gameObject;
            rotationPoint.transform.LookAt(enemy.transform.position);
        }
        else
            target = null;

        ShootAtTarget();
    }

    public bool CanShoot(GameObject enemy)
    {
        rotationPoint.transform.LookAt(enemy.transform.position);

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

    private void ShootAtTarget()
    {
        foreach (GameObject shootingPoint in shootingPoints)
        {
            if (internalFireRate <= 0 && target != null)
            {
                internalFireRate = GetFireRate();
                //GameObject _projectile = projectilePool.Get();
                GameObject _projectile = Instantiate(projectile, shootingPoint.transform.position, shootingPoint.transform.rotation);
                _projectile.GetComponent<Projectile>().Target = target;
                _projectile.GetComponent<Projectile>().lifetime = 2.5f;
                _projectile.GetComponent<Projectile>().penetration = TowerData.BasePenetration;
                _projectile.transform.position = shootingPoint.transform.position;
                _projectile.transform.rotation = shootingPoint.transform.rotation;
                _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();

                /*
                if (shootingPoint.activeSelf)
                    Instantiate(projectile, shootingPoint.transform.position, shootingPoint.transform.rotation);
                */
            }
            else
            {
                internalFireRate -= Time.deltaTime;
            }
        }
    }

    public void ChangeTargettingBehaviour(TargetBehaviour targetBehaviour)
    {
        this.targetBehaviour = targetBehaviour;
    }

    private float GetFireRate()
    {
        return data.BaseFireRate;
    }

    public void MouseEnter()
    {
        renderer.enabled = true;
    }

    public void MouseExit()
    {
        renderer.enabled = false;
    }
}
