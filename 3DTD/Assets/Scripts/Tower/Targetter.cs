using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Targetter : MonoBehaviour
{
    [SerializeField] private Tower tower;

    [SerializeField] private new Collider collider;
    public Collider Collider => collider;

    [SerializeField] private List<Enemy> enemiesInsideCollider = new List<Enemy>();

    public static GameObject GetFirstEnemyInGame(GameObject enemy)
    {
        if (Spawner.Instance == null || Spawner.Instance.EnemiesAlive.Count <= 1)
            return null;

        List<GameObject> alive = Spawner.Instance.EnemiesAlive;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            GameObject candidate = alive[UnityEngine.Random.Range(0, alive.Count)];
            if (candidate != null && candidate != enemy && candidate.activeInHierarchy)
                return candidate;
        }
        return null;
    }

    // Reused by GetAllEnemiesInRadius. Only valid until the next call on this Targetter.
    private readonly List<Enemy> resultBuffer = new List<Enemy>();

    private static bool IsValid(Enemy enemy)
    {
        return enemy != null && enemy.IsAlive;
    }

    public List<Enemy> GetAllEnemiesInRadius()
    {
        resultBuffer.Clear();
        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            if (IsValid(enemiesInsideCollider[i]))
                resultBuffer.Add(enemiesInsideCollider[i]);
        }
        return resultBuffer;
    }

    public Enemy GetEnemy(TargetBehaviour targetBehaviour)
    {
        switch (targetBehaviour)
        {
            case TargetBehaviour.FIRST:
                return GetFirstEnemyInRadius();
            case TargetBehaviour.LAST:
                return GetLastEnemyInRadius();
            case TargetBehaviour.STRONGEST:
                return GetStrongestEnemyInRadius();
            case TargetBehaviour.NEAREST:
                return GetNearestEnemyInRadius();
            case TargetBehaviour.FARTHEST:
                return GetFarthestEnemyInRadius();
        }
        return GetFirstEnemyInRadius();
    }

    public Enemy GetFirstEnemyInRadius()
    {
        Enemy best = null;
        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            Enemy enemy = enemiesInsideCollider[i];
            if (IsValid(enemy) && (best == null || enemy.DistanceTraveled > best.DistanceTraveled))
                best = enemy;
        }
        return best;
    }

    // Highest Id; ties go to the enemy furthest along the path
    public Enemy GetStrongestEnemyInRadius()
    {
        Enemy best = null;
        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            Enemy enemy = enemiesInsideCollider[i];
            if (!IsValid(enemy))
                continue;

            if (best == null || enemy.Id > best.Id || (enemy.Id == best.Id && enemy.DistanceTraveled > best.DistanceTraveled))
                best = enemy;
        }
        return best;
    }

    public Enemy GetLastEnemyInRadius()
    {
        Enemy best = null;
        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            Enemy enemy = enemiesInsideCollider[i];
            if (IsValid(enemy) && (best == null || enemy.DistanceTraveled < best.DistanceTraveled))
                best = enemy;
        }
        return best;
    }

    public Enemy GetNearestEnemyInRadius()
    {
        float nearestDistance = Mathf.Infinity;
        Enemy nearestEnemy = null;
        Vector3 center = collider.transform.position;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            Enemy enemy = enemiesInsideCollider[i];
            if (!IsValid(enemy))
                continue;

            float sqrDistance = (center - enemy.transform.position).sqrMagnitude;
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    public Enemy GetFarthestEnemyInRadius()
    {
        float farthestDistance = -Mathf.Infinity;
        Enemy farthestEnemy = null;
        Vector3 center = collider.transform.position;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            Enemy enemy = enemiesInsideCollider[i];
            if (!IsValid(enemy))
                continue;

            float sqrDistance = (center - enemy.transform.position).sqrMagnitude;
            if (sqrDistance > farthestDistance)
            {
                farthestDistance = sqrDistance;
                farthestEnemy = enemy;
            }
        }

        return farthestEnemy;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent<Enemy>(out Enemy enemy) && enemy.IsAlive)
        {
            if (!enemiesInsideCollider.Contains(enemy))
            {
                enemiesInsideCollider.Add(enemy);
                enemy.OnDeath += HandleEnemyDeath;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            enemy.OnDeath -= HandleEnemyDeath;
            enemiesInsideCollider.Remove(enemy);
        }
    }

    private void HandleEnemyDeath(GameObject gameObject)
    {
        Enemy enemy = gameObject.GetComponent<Enemy>();
        enemy.OnDeath -= HandleEnemyDeath;
        enemiesInsideCollider.Remove(enemy);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            if (enemiesInsideCollider[i] != null)
                enemiesInsideCollider[i].OnDeath -= HandleEnemyDeath;
        }
    }
}

public enum TargetBehaviour
{
    FIRST = 0,
    LAST = 1,
    STRONGEST = 2,
    NEAREST = 3,
    FARTHEST = 4
}
