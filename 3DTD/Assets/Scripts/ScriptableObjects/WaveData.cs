using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseWaveData", menuName = "TowerDefense/WaveData", order = 0)]
public class WaveData : ScriptableObject
{
    [SerializeField] public List<EnemyData> enemiesToSpawn;
    [SerializeField] public List<int> enemySpawnCount;
    [SerializeField] public List<float> spawnDelay;
    [Tooltip("Optional, parallel to the lists above. Missing entries mean no traits.")]
    [SerializeField] public List<EnemyTrait> enemyTraits;

    public List<EnemyData> EnemiesToSpawn => enemiesToSpawn;
    public List<int> EnemySpawnCount => enemySpawnCount;
    public List<float> SpawnDelay => spawnDelay;
    public List<EnemyTrait> EnemyTraits => enemyTraits;

    public EnemyTrait TraitsAt(int index)
    {
        return enemyTraits != null && index < enemyTraits.Count ? enemyTraits[index] : EnemyTrait.None;
    }
}
