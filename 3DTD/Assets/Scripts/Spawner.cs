using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private List<WaveData> waves;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Waypoints[] waypoints;
    [SerializeField] private bool autoPlay = false;
    [SerializeField] private GameState currentGameState;
    [SerializeField] private float scalingFactor = 0.1f;

    private List<GameObject> enemiesAlive = new List<GameObject>();
    [SerializeField] private int enemiesInWave = 0;

    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveEnded;

    private static Spawner instance;
    public static Spawner Instance { get { return instance; } }
    public List<GameObject> EnemiesAlive { get { return enemiesAlive; } }

    private bool isWon = false;

    private class RuntimeWave
    {
        public readonly List<EnemyData> EnemiesToSpawn;
        public readonly List<int> EnemySpawnCount;
        public readonly List<float> SpawnDelay;

        public RuntimeWave(WaveData source)
        {
            EnemiesToSpawn = source.EnemiesToSpawn != null ? new List<EnemyData>(source.EnemiesToSpawn) : new List<EnemyData>();
            EnemySpawnCount = source.EnemySpawnCount != null ? new List<int>(source.EnemySpawnCount) : new List<int>();
            SpawnDelay = source.SpawnDelay != null ? new List<float>(source.SpawnDelay) : new List<float>();
        }

        public int EntryCount
        {
            get
            {
                return Mathf.Min(EnemiesToSpawn.Count, EnemySpawnCount.Count, SpawnDelay.Count);
            }
        }
    }

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        currentGameState = GameState.IDLE;
    }

    private void Update()
    {
        if (currentGameState != GameState.PROGRESSING)
            return;

        if (enemiesAlive.Count != 0)
            return;

        currentGameState = GameState.IDLE;
        OnWaveEnded?.Invoke(GameManager.Instance.Round);

        if (autoPlay)
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (currentGameState != GameState.IDLE || waves == null || waves.Count == 0 || spawnPoints == null || waypoints == null)
            return;

        bool infiniteWave = GameManager.Instance.Round >= waves.Count;
        if (infiniteWave && !isWon)
        {
            GameManager.Instance.GameWon();
            isWon = true;
        }

        RuntimeWave wave = BuildRuntimeWave(infiniteWave);
        int laneCount = Mathf.Min(spawnPoints.Length, waypoints.Length);
        int spawnCount = GetSpawnCount(wave);

        if (laneCount == 0 || spawnCount == 0)
            return;

        enemiesInWave = spawnCount * laneCount;

        int roundStarted = GameManager.Instance.Round;
        GameManager.Instance.Round++;
        currentGameState = GameState.PROGRESSING;
        OnWaveStarted?.Invoke(roundStarted);

        for (int i = 0; i < laneCount; i++)
        {
            StartCoroutine(SpawningWave(spawnPoints[i], waypoints[i], wave));
        }
    }

    private RuntimeWave BuildRuntimeWave(bool infiniteWave)
    {
        if (infiniteWave)
        {
            RuntimeWave wave = new RuntimeWave(waves[waves.Count - 1]);
            int infiniteRoundOffset = GameManager.Instance.Round - (waves.Count - 1);

            for (int i = 0; i < wave.EntryCount; i++)
            {
                float scaling = scalingFactor * infiniteRoundOffset;
                wave.EnemySpawnCount[i] += Mathf.RoundToInt(wave.EnemySpawnCount[i] * scaling);
                wave.SpawnDelay[i] = Mathf.Max(0.01f, wave.SpawnDelay[i] - (wave.SpawnDelay[i] * scaling));
            }

            return wave;
        }

        return new RuntimeWave(waves[GameManager.Instance.Round]);
    }

    private int GetSpawnCount(RuntimeWave wave)
    {
        int spawnCount = 0;

        for (int i = 0; i < wave.EntryCount; i++)
        {
            if (wave.EnemiesToSpawn[i] != null)
                spawnCount += Mathf.Max(0, wave.EnemySpawnCount[i]);
        }

        return spawnCount;
    }

    IEnumerator SpawningWave(Transform spawnPoint, Waypoints waypoints, RuntimeWave wave)
    {
        GameObject lastEnemy = null;

        for (int i = 0; i < wave.EntryCount; i++)
        {
            if (wave.EnemiesToSpawn[i] == null)
                continue;

            for (int j = 0; j < Mathf.Max(0, wave.EnemySpawnCount[i]); j++)
            {
                if (lastEnemy != null)
                    lastEnemy.SetActive(true);

                GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
                Enemy enemyComponent = enemy.GetComponent<Enemy>();
                enemyComponent.data = wave.EnemiesToSpawn[i];
                enemyComponent.CurrentShape = wave.EnemiesToSpawn[i].StartShape;
                enemyComponent.CurrentColor = wave.EnemiesToSpawn[i].StartColor;
                enemyComponent.waypoints = waypoints;

                AddEnemyToList(enemy);
                enemy.SetActive(false);
                lastEnemy = enemy;

                yield return new WaitForSeconds(wave.SpawnDelay[i]);
            }
        }

        if (lastEnemy != null)
            lastEnemy.SetActive(true);
    }

    private void AddEnemyToList(GameObject enemy)
    {
        enemiesAlive.Add(enemy);
        enemy.GetComponent<Enemy>().OnDeath += HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        enemiesAlive.Remove(enemy);
        enemiesInWave--;
    }
}

public enum GameState
{
    IDLE,
    PROGRESSING
}
