using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Targetter : MonoBehaviour
{
    [SerializeField] private Tower tower;

    [SerializeField] private new Collider collider;
    public Collider Collider => collider;

    // Enemies inside the trigger, each with the spawn serial of the life it entered with. An enemy that dies
    // inside gets no OnTriggerExit (it is deactivated and pooled), so stale entries are pruned lazily: a dead
    // enemy, or a pooled one that started a new life elsewhere, no longer matches its serial.
    private struct Entry
    {
        public Enemy Enemy;
        public int Serial;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<Enemy, int> indexOf = new Dictionary<Enemy, int>();
    private int prunedFrame = -1;

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

    private static bool IsValid(Entry entry)
    {
        return entry.Enemy != null && entry.Enemy.IsAlive && entry.Enemy.SpawnSerial == entry.Serial;
    }

    // Whether the enemy is currently inside the range
    public bool Contains(Enemy enemy)
    {
        return enemy != null && indexOf.TryGetValue(enemy, out int index) && IsValid(entries[index]);
    }

    public List<Enemy> GetAllEnemiesInRadius()
    {
        Prune();
        resultBuffer.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            if (IsValid(entries[i]))
                resultBuffer.Add(entries[i].Enemy);
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
        Prune();
        Enemy best = null;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (IsValid(entry) && (best == null || entry.Enemy.DistanceTraveled > best.DistanceTraveled))
                best = entry.Enemy;
        }
        return best;
    }

    // Highest Id; ties go to the enemy furthest along the path
    public Enemy GetStrongestEnemyInRadius()
    {
        Prune();
        Enemy best = null;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (!IsValid(entry))
                continue;

            Enemy enemy = entry.Enemy;
            if (best == null || enemy.Id > best.Id || (enemy.Id == best.Id && enemy.DistanceTraveled > best.DistanceTraveled))
                best = enemy;
        }
        return best;
    }

    public Enemy GetLastEnemyInRadius()
    {
        Prune();
        Enemy best = null;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (IsValid(entry) && (best == null || entry.Enemy.DistanceTraveled < best.DistanceTraveled))
                best = entry.Enemy;
        }
        return best;
    }

    public Enemy GetNearestEnemyInRadius()
    {
        Prune();
        float nearestDistance = Mathf.Infinity;
        Enemy nearestEnemy = null;
        Vector3 center = collider.transform.position;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (!IsValid(entry))
                continue;

            float sqrDistance = (center - entry.Enemy.transform.position).sqrMagnitude;
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestEnemy = entry.Enemy;
            }
        }

        return nearestEnemy;
    }

    public Enemy GetFarthestEnemyInRadius()
    {
        Prune();
        float farthestDistance = -Mathf.Infinity;
        Enemy farthestEnemy = null;
        Vector3 center = collider.transform.position;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (!IsValid(entry))
                continue;

            float sqrDistance = (center - entry.Enemy.transform.position).sqrMagnitude;
            if (sqrDistance > farthestDistance)
            {
                farthestDistance = sqrDistance;
                farthestEnemy = entry.Enemy;
            }
        }

        return farthestEnemy;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<Enemy>(out Enemy enemy) || !enemy.IsAlive)
            return;

        // A pooled enemy can come back with a new life while its old entry is still here
        if (indexOf.TryGetValue(enemy, out int index))
        {
            entries[index] = new Entry { Enemy = enemy, Serial = enemy.SpawnSerial };
            return;
        }

        indexOf.Add(enemy, entries.Count);
        entries.Add(new Entry { Enemy = enemy, Serial = enemy.SpawnSerial });
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Enemy>(out Enemy enemy) && indexOf.TryGetValue(enemy, out int index))
            RemoveAt(index);
    }

    // Drops dead and recycled enemies, at most once per frame
    private void Prune()
    {
        if (prunedFrame == Time.frameCount)
            return;
        prunedFrame = Time.frameCount;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!IsValid(entries[i]))
                RemoveAt(i);
        }
    }

    // Swap-remove: order doesn't matter, every query scans all entries
    private void RemoveAt(int index)
    {
        Enemy removed = entries[index].Enemy;
        int last = entries.Count - 1;
        if (index != last)
        {
            Entry moved = entries[last];
            entries[index] = moved;
            if (!ReferenceEquals(moved.Enemy, null))
                indexOf[moved.Enemy] = index;
        }
        entries.RemoveAt(last);
        if (!ReferenceEquals(removed, null))
            indexOf.Remove(removed);
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
