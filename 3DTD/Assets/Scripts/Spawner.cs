using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private List<WaveData> waves;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool autoPlay = false;
    [SerializeField] private GameState currentGameState;

    private List<GameObject> enemiesAlive = new List<GameObject>();
    [SerializeField] private int enemiesInWave = 0;

    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveEnded;

    private static Spawner instance;
    public static Spawner Instance { get { return instance; } }
    public List<GameObject> EnemiesAlive { get { return enemiesAlive; } }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }

    private void Start()
    {
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
        OnWaveStarted?.Invoke(GameManager.Instance.Round);
        if (currentGameState == GameState.IDLE)
        {
            StartCoroutine(nameof(SpawningWave));
        }
    }

    IEnumerator SpawningWave()
    {
        WaveData wave = waves[GameManager.Instance.Round];
        GameManager.Instance.Round++;

        int spawnCount = 0;

        for (int i = 0; i < wave.EnemiesToSpawn.Count; i++)
        {
            spawnCount += wave.EnemySpawnCount[i];
        }

        enemiesInWave = spawnCount;

        for (int i = 0; i < wave.EnemiesToSpawn.Count; i++)
        {
            for (int j = 0; j < wave.EnemySpawnCount[i]; j++)
            {
                enemyPrefab.GetComponent<Enemy>().data = wave.EnemiesToSpawn[i];
                AddEnemyToList(Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation));

                if (currentGameState == GameState.IDLE)
                    currentGameState = GameState.PROGRESSING;

                yield return new WaitForSeconds(wave.SpawnDelay);
            }
        }
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
