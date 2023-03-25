using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseWaveData", menuName = "TowerDefense/WaveData", order = 0)]
public class WaveData : ScriptableObject
{
    [SerializeField] private List<EnemyData> enemiesToSpawn;
    [SerializeField] private List<int> enemySpawnCount;
    [SerializeField] private List<float> spawnDelay;

    public List<EnemyData> EnemiesToSpawn => enemiesToSpawn;
    public List<int> EnemySpawnCount => enemySpawnCount;
    public List<float> SpawnDelay => spawnDelay;
}