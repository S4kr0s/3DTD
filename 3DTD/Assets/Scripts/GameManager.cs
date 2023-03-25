using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private int money;
    [SerializeField] private int lives;
    [SerializeField] private int round;
    [SerializeField] private float gameSpeed = 1f;
    [SerializeField] private Difficulty difficulty;
    [SerializeField] private int baseStartingMoney;
    [SerializeField] private int baseLives;

    public event Action<int> OnMoneyChanged;
    public event Action<int> OnLivesChanged;
    public event Action<int> OnRoundChanged;
    public event Action<float> OnGameSpeedChanged;

    public int Money 
    {
        get 
        { 
            return money; 
        } 
        
        set 
        { 
            money = value;
            OnMoneyChanged?.Invoke(value);
        } 
    }

    public int Lives 
    { 
        get 
        { 
            return lives; 
        } 
        
        set 
        { 
            lives = value;
            OnLivesChanged?.Invoke(value);
        } 
    }

    public int Round 
    { 
        get 
        { 
            return round; 
        } 
        
        set 
        { 
            round = value;
            OnRoundChanged?.Invoke(value);
        } 
    }

    public float GameSpeed
    {
        get 
        { 
            return gameSpeed; 
        }

        private set 
        { 
            gameSpeed = value; 
            OnGameSpeedChanged?.Invoke(value);
        }
    }

    public Difficulty Difficulty => difficulty;

    private static GameManager instance;
    public static GameManager Instance { get { return instance; } }

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

        OnLivesChanged += HandleLivesChanged;
        OnGameSpeedChanged += HandleGameSpeedChanged;

        round = 0;
        switch (difficulty)
        {
            case Difficulty.Easy:
                money = baseStartingMoney * 2;
                lives = baseLives * 2;
                break;
            case Difficulty.Medium:
                money = baseStartingMoney;
                lives = baseLives;
                break;
            case Difficulty.Hard:
                money = Mathf.RoundToInt(baseStartingMoney * 1.5f);
                lives = Mathf.RoundToInt(baseLives * 1.5f);
                break;
            case Difficulty.Impossible:
                money = Mathf.RoundToInt(baseStartingMoney / 2);
                lives = Mathf.RoundToInt(baseLives / 2);
                break;
        }
    }

    private void Start()
    {
        GameObject.FindGameObjectWithTag("End").GetComponent<End>().OnEnemyReachedExit += HandleEnemyReachedExit;
    }

    [SerializeField] private GameObject canvas;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (canvas.activeSelf)
                canvas.SetActive(false);
            else
                canvas.SetActive(true);
        }
    }

    private void HandleEnemyReachedExit(Enemy enemy)
    {
        enemy.DestroyWholeEnemy();
        Lives -= enemy.Id + 1;
    }

    private void HandleLivesChanged(int value)
    {
        if (value <= 0)
            GameOver();
    }

    private void HandleGameSpeedChanged(float value)
    {
        Time.timeScale = value;
    }

    public void GameOver()
    {
        PauseMenu.Instance.GameOver();
    }

    public void ChangeGameSpeed(float newGameSpeed)
    {
        GameSpeed = newGameSpeed;
    }
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
    Impossible
}
