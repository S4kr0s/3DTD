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
        if (Spawner.Instance.EnemiesAlive.Count <= 1)
        {
            return null;
        }
        else
        {
            List<GameObject> possibleEnemies = Spawner.Instance.EnemiesAlive;
            possibleEnemies.Remove(enemy);
            if (possibleEnemies == null)
                return null;
            return Spawner.Instance.EnemiesAlive[UnityEngine.Random.Range(0, possibleEnemies.Count - 1)];
        }
    }

    public List<Enemy> GetAllEnemiesInRadius()
    {
        return enemiesInsideCollider;
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
        Enemy[] possibleEnemies = enemiesInsideCollider.OrderByDescending(e => e.DistanceTraveled).ToArray();
        for (int i = 0; i < possibleEnemies.Length; i++)
        {
            if (tower.ActionStrategy.CanShoot(possibleEnemies[i].gameObject))
                return possibleEnemies[i];
        }
        return null;
    }

    public Enemy GetStrongestEnemyInRadius()
    {
        Enemy[] possibleEnemies = enemiesInsideCollider.OrderByDescending(e => e.DistanceTraveled).ToArray();

        if (possibleEnemies.Length == 0)
            return null;

        int powerScore = 0;
        int index = 0;

        for (int i = 0; i < possibleEnemies.Length; i++)
        {
            if (tower.ActionStrategy.CanShoot(possibleEnemies[i].gameObject))
            {
                if (powerScore == 0)
                {
                    powerScore = (int)possibleEnemies[i].CurrentShape * 10 + (int)possibleEnemies[i].CurrentColor;
                }
                else
                {
                    if ((int)possibleEnemies[i].CurrentShape * 10 + (int)possibleEnemies[i].CurrentColor > powerScore)
                    {
                        powerScore = (int)possibleEnemies[i].CurrentShape * 10 + (int)possibleEnemies[i].CurrentColor;
                        index = i;
                    }
                }
            }
        }

        return possibleEnemies[index];
    }

    public Enemy GetLastEnemyInRadius()
    {
        for (int i = enemiesInsideCollider.Count - 1; i >= 0; i--)
        {
            if (tower.ActionStrategy.CanShoot(enemiesInsideCollider[i].gameObject))
                return enemiesInsideCollider[i];
        }
        return null;
    }

    public Enemy GetNearestEnemyInRadius()
    {
        float distance = Mathf.Infinity;
        int? index = null;

        if (enemiesInsideCollider.Count == 0)
            return null;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            float possibleDistance = Vector3.Distance(collider.gameObject.transform.position, enemiesInsideCollider[i].gameObject.transform.position);
            if (index == null)
            {
                distance = possibleDistance;
                index = i;
            }
            else
            {
                float distanceToCompare = Vector3.Distance(collider.gameObject.transform.position, enemiesInsideCollider[(int)index].gameObject.transform.position);

                if (distanceToCompare <= distance && tower.ActionStrategy.CanShoot(enemiesInsideCollider[(int)index].gameObject))
                {
                    distance = distanceToCompare;
                    index = i;
                }
            }
        }

        return enemiesInsideCollider[(int)index];
    }

    public Enemy GetFarthestEnemyInRadius()
    {
        float distance = Mathf.Infinity;
        int? index = null;

        if (enemiesInsideCollider.Count == 0)
            return null;

        for (int i = 0; i < enemiesInsideCollider.Count; i++)
        {
            if (index == null)
            {
                float possibleDistance = Vector3.Distance(collider.gameObject.transform.position, enemiesInsideCollider[i].gameObject.transform.position);

                distance = possibleDistance;
                index = i;
            }
            else
            {
                float distanceToCompare = Vector3.Distance(collider.gameObject.transform.position, enemiesInsideCollider[(int)index].gameObject.transform.position);
                //Debug.Log($"{distanceToCompare} > {distance}");
                if (distanceToCompare >= distance && tower.ActionStrategy.CanShoot(enemiesInsideCollider[(int)index].gameObject))
                {
                    distance = distanceToCompare;
                    index = i;
                }
            }
        }

        Debug.Log(index);
        //Debug.Log($"{Vector3.Distance(collider.gameObject.transform.position, enemiesInsideCollider[(int)index].gameObject.transform.position)} FINAL DISTANCE");
        return enemiesInsideCollider[(int)index];
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            enemiesInsideCollider.Add(enemy);
            enemy.OnDeath += HandleEnemyDeath;
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
