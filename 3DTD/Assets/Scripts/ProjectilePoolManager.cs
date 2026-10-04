using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePoolManager : MonoBehaviour
{
    // Hard cap so a runaway fire rate can't flood the scene with projectiles
    public const int MaxPoolSize = 256;

    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private int _poolSize = 10;

    public GameObject ProjectilePrefab => _projectilePrefab;

    private Queue<GameObject> pooledProjectiles = new Queue<GameObject>();
    private readonly List<GameObject> allProjectiles = new List<GameObject>();

    private void OnDestroy()
    {
        foreach (GameObject projectile in allProjectiles)
        {
            if (projectile != null)
                Destroy(projectile);
        }
    }

    // Reuses the tower's pool for the same projectile and removes pools of other projectiles,
    // so swapping the action strategy through an upgrade doesn't leave an orphaned pool behind
    public static ProjectilePoolManager GetOrCreate(GameObject owner, GameObject projectilePrefab, int poolSize)
    {
        ProjectilePoolManager match = null;
        foreach (ProjectilePoolManager existing in owner.GetComponents<ProjectilePoolManager>())
        {
            if (match == null && existing.ProjectilePrefab == projectilePrefab)
                match = existing;
            else
                Destroy(existing);
        }

        if (match != null)
        {
            match.Prewarm(poolSize);
            return match;
        }

        ProjectilePoolManager pool = owner.AddComponent<ProjectilePoolManager>();
        pool.Setup(projectilePrefab, poolSize);
        return pool;
    }

    public void Setup(GameObject projectilePrefab, int poolSize)
    {
        _projectilePrefab = projectilePrefab;
        _poolSize = 0;
        Prewarm(poolSize);
    }

    // Makes sure at least poolSize projectiles exist
    public void Prewarm(int poolSize)
    {
        poolSize = Mathf.Clamp(poolSize, 1, MaxPoolSize);
        while (allProjectiles.Count < poolSize)
            pooledProjectiles.Enqueue(CreateProjectile());
        _poolSize = allProjectiles.Count;
    }

    private GameObject CreateProjectile()
    {
        GameObject projectile = Instantiate(_projectilePrefab);
        projectile.SetActive(false);
        allProjectiles.Add(projectile);
        return projectile;
    }

    // Grows on demand: pools used to be sized from the fire rate before upgrades and starved fast towers
    public GameObject GetPooledProjectile()
    {
        PerfCounters.ProjectilesRequested++;
        GameObject projectile = null;
        while (projectile == null && pooledProjectiles.Count > 0)
            projectile = pooledProjectiles.Dequeue();

        if (projectile == null)
        {
            if (allProjectiles.Count >= MaxPoolSize)
                return null;

            projectile = CreateProjectile();
            _poolSize = allProjectiles.Count;
        }

        projectile.SetActive(true);
        PerfCounters.ProjectilesSpawned++;
        return projectile;
    }

    public void ReturnToPool(GameObject projectile)
    {
        // Wait because Projectile.cs has fancy animation now
        StartCoroutine(WaitAndEnqueue(0.5f, projectile));
    }

    IEnumerator WaitAndEnqueue(float delay, GameObject projectile)
    {
        yield return new WaitForSeconds(delay);
        pooledProjectiles.Enqueue(projectile);
    }
}
