using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        List<GameObject> possibleEnemies = Spawner.Instance.EnemiesAlive
            .Where(possibleEnemy => possibleEnemy != null && possibleEnemy != enemy)
            .ToList();

        if (possibleEnemies.Count == 0)
            return null;

        return possibleEnemies[UnityEngine.Random.Range(0, possibleEnemies.Count)];
    }

    public List<Enemy> GetAllEnemiesInRadius()
    {
        return enemiesInsideCollider.Where(enemy => enemy != null).ToList();
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
        return enemiesInsideCollider
            .Where(enemy => enemy != null)
            .OrderByDescending(enemy => enemy.DistanceTraveled)
            .FirstOrDefault();
    }

    public Enemy GetStrongestEnemyInRadius()
    {
        Enemy[] possibleEnemies = enemiesInsideCollider
            .Where(enemy => enemy != null)
            .OrderByDescending(enemy => enemy.DistanceTraveled)
            .ToArray();

        if (possibleEnemies.Length == 0)
            return null;

        int powerScore = int.MinValue;
        int index = 0;

        for (int i = 0; i < possibleEnemies.Length; i++)
        {
            //tower.ActionStrategy.CanShoot(possibleEnemies[i].gameObject)
            int compareScore = (int)possibleEnemies[i].CurrentShape * 10 + (int)possibleEnemies[i].CurrentColor;
            if (compareScore > powerScore)
            {
                powerScore = compareScore;
                index = i;
            }
        }

        return possibleEnemies[index];
    }

    public Enemy GetLastEnemyInRadius()
    {
        return enemiesInsideCollider
            .Where(enemy => enemy != null)
            .OrderBy(enemy => enemy.DistanceTraveled)
            .FirstOrDefault();
    }

    public Enemy GetNearestEnemyInRadius()
    {
        float nearestDistance = Mathf.Infinity;
        Enemy nearestEnemy = null;

        if (enemiesInsideCollider.Count == 0)
            return null;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            if (enemiesInsideCollider[i] == null)
                continue;

            float possibleDistance = Vector3.Distance(collider.transform.position, enemiesInsideCollider[i].transform.position);

            //  && tower.ActionStrategy.CanShoot(enemiesInsideCollider[i].gameObject)
            if (possibleDistance < nearestDistance)
            {
                nearestDistance = possibleDistance;
                nearestEnemy = enemiesInsideCollider[i];
            }
        }

        return nearestEnemy;
    }

    public Enemy GetFarthestEnemyInRadius()
    {
        float farthestDistance = -Mathf.Infinity;
        Enemy farthestEnemy = null;

        if (enemiesInsideCollider.Count == 0)
            return null;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            if (enemiesInsideCollider[i] == null)
                continue;

            float possibleDistance = Vector3.Distance(collider.transform.position, enemiesInsideCollider[i].transform.position);

            //  && tower.ActionStrategy.CanShoot(enemiesInsideCollider[i].gameObject)
            if (possibleDistance > farthestDistance)
            {
                farthestDistance = possibleDistance;
                farthestEnemy = enemiesInsideCollider[i];
            }
        }

        return farthestEnemy;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
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
        enemiesInsideCollider.Remove(gameObject.GetComponent<Enemy>());
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
