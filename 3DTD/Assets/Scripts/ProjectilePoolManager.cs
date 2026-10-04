using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePoolManager : MonoBehaviour
{
    // Safety net against a runaway configuration. Real fire rates stay far below it; shots are never
    // dropped below it (that used to happen at 256 for fast towers).
    public const int MaxPoolSize = 4096;

    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private int _poolSize = 10;

    public GameObject ProjectilePrefab => _projectilePrefab;

    private readonly Queue<GameObject> pooledProjectiles = new Queue<GameObject>();
    private readonly List<GameObject> allProjectiles = new List<GameObject>();
    private bool warnedAboutCap;

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
        if (projectile.TryGetComponent(out Projectile component))
            component.Pool = this;
        allProjectiles.Add(projectile);
        return projectile;
    }

    // Returns an inactive projectile; the caller positions and configures it, then activates it once.
    // Grows on demand: pools used to be sized from the fire rate before upgrades and starved fast towers.
    public GameObject GetPooledProjectile()
    {
        PerfCounters.ProjectilesRequested++;
        GameObject projectile = null;
        while (projectile == null && pooledProjectiles.Count > 0)
            projectile = pooledProjectiles.Dequeue();

        if (projectile == null)
        {
            if (allProjectiles.Count >= MaxPoolSize)
            {
                if (!warnedAboutCap)
                {
                    warnedAboutCap = true;
                    Debug.LogWarning(name + ": projectile pool reached " + MaxPoolSize + " projectiles; further shots are dropped");
                }
                return null;
            }

            projectile = CreateProjectile();
            _poolSize = allProjectiles.Count;
        }

        PerfCounters.ProjectilesSpawned++;
        return projectile;
    }

    // Called by the projectile once its death fade is over
    public void Release(GameObject projectile)
    {
        pooledProjectiles.Enqueue(projectile);
    }
}
