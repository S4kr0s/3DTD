using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseWaveData", menuName = "TowerDefense/WaveData", order = 0)]
public class WaveData : ScriptableObject
{
    [SerializeField] public List<EnemyData> enemiesToSpawn;
    [SerializeField] public List<int> enemySpawnCount;
    [SerializeField] public List<float> spawnDelay;

    public List<EnemyData> EnemiesToSpawn => enemiesToSpawn;
    public List<int> EnemySpawnCount => enemySpawnCount;
    public List<float> SpawnDelay => spawnDelay;
}