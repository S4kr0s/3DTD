using System.Collections.Generic;
using UnityEngine;

public class ProjectilePoolManager : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private int _poolSize = 10;

    private Queue<GameObject> pooledProjectiles;

    private void OnDestroy()
    {
        foreach (var p in pooledProjectiles)
            Destroy(p);
    }

    public void Setup(GameObject projectilePrefab, int poolSize)
    {
        _projectilePrefab = projectilePrefab;
        _poolSize = poolSize;

        pooledProjectiles = new Queue<GameObject>(_poolSize);

        for (int i = 0; i < _poolSize; i++)
        {
            GameObject projectile = Instantiate(_projectilePrefab);
            projectile.SetActive(false);
            pooledProjectiles.Enqueue(projectile);
        }
    }

    public GameObject GetPooledProjectile()
    {
        if (pooledProjectiles.Count == 0)
        {
            return null;
        }

        GameObject projectile = pooledProjectiles.Dequeue();
        projectile.SetActive(true);
        return projectile;
    }

    public void ReturnToPool(GameObject projectile)
    {
        projectile.SetActive(false);
        pooledProjectiles.Enqueue(projectile);
    }
}