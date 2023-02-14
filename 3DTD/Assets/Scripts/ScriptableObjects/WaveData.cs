using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseWaveData", menuName = "TowerDefense/WaveData", order = 0)]
public class WaveData : ScriptableObject
{
    [SerializeField] private List<EnemyData> enemiesToSpawn;
    [SerializeField] private List<int> enemySpawnCount;
    [SerializeField] private float spawnDelay;

    public List<EnemyData> EnemiesToSpawn => enemiesToSpawn;
    public List<int> EnemySpawnCount => enemySpawnCount;
    public float SpawnDelay => spawnDelay;
}