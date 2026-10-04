using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// Positions of the living enemies in flat native arrays, for the projectile hit tests (Burst jobs) and the
// blast queries. Enemies register when they spawn and unregister when they die or leak. Sync() runs once per
// frame after the enemies moved and keeps last frame's position, so hits can follow both movements.
//
// A slot is reused by later enemies; the enemy's SpawnSerial tells the lives apart.
public sealed class EnemyRegistry : IDisposable
{
    public NativeList<float3> Positions;
    public NativeList<float3> PreviousPositions;
    public NativeList<float> Radii;
    public NativeList<int> Serials;      // 0 = free slot
    public int Count => enemies.Count;

    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<Transform> transforms = new List<Transform>();
    private readonly Stack<int> freeSlots = new Stack<int>();

    public EnemyRegistry()
    {
        Positions = new NativeList<float3>(1024, Allocator.Persistent);
        PreviousPositions = new NativeList<float3>(1024, Allocator.Persistent);
        Radii = new NativeList<float>(1024, Allocator.Persistent);
        Serials = new NativeList<int>(1024, Allocator.Persistent);
    }

    public void Dispose()
    {
        if (Positions.IsCreated) Positions.Dispose();
        if (PreviousPositions.IsCreated) PreviousPositions.Dispose();
        if (Radii.IsCreated) Radii.Dispose();
        if (Serials.IsCreated) Serials.Dispose();
    }

    public Enemy EnemyAt(int slot)
    {
        return enemies[slot];
    }

    // True while the slot still holds the enemy life that had this serial
    public bool IsSame(int slot, int serial)
    {
        return slot >= 0 && slot < enemies.Count && serial != 0 && Serials[slot] == serial && enemies[slot] != null && enemies[slot].IsAlive;
    }

    public int Register(Enemy enemy, float radius)
    {
        int slot;
        float3 position = enemy.transform.position;
        if (freeSlots.Count > 0)
        {
            slot = freeSlots.Pop();
            enemies[slot] = enemy;
            transforms[slot] = enemy.transform;
            Positions[slot] = position;
            PreviousPositions[slot] = position;
            Radii[slot] = radius;
            Serials[slot] = enemy.SpawnSerial;
        }
        else
        {
            slot = enemies.Count;
            enemies.Add(enemy);
            transforms.Add(enemy.transform);
            Positions.Add(position);
            PreviousPositions.Add(position);
            Radii.Add(radius);
            Serials.Add(enemy.SpawnSerial);
        }
        return slot;
    }

    public void Unregister(int slot)
    {
        if (slot < 0 || slot >= enemies.Count || Serials[slot] == 0)
            return;
        Serials[slot] = 0;
        enemies[slot] = null;
        transforms[slot] = null;
        freeSlots.Push(slot);
    }

    // Once per frame, after the enemies moved
    public void Sync()
    {
        for (int slot = 0; slot < enemies.Count; slot++)
        {
            if (Serials[slot] == 0)
                continue;
            PreviousPositions[slot] = Positions[slot];
            Positions[slot] = transforms[slot].position;
        }
    }

    // Living enemies whose collider overlaps the sphere, like Physics.OverlapSphere on their colliders
    // (no buffer limit, and blocks or tower ranges can't crowd them out)
    public void Overlap(Vector3 center, float radius, List<Enemy> results)
    {
        results.Clear();
        float3 c = center;
        for (int slot = 0; slot < enemies.Count; slot++)
        {
            if (Serials[slot] == 0)
                continue;
            float reach = radius + Radii[slot];
            if (math.distancesq(Positions[slot], c) <= reach * reach && enemies[slot].IsAlive)
                results.Add(enemies[slot]);
        }
    }
}
