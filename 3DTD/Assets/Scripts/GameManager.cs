using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> buildingPrefabs;

    [SerializeField] private int money;
    [SerializeField] private int lives;
    [SerializeField] private int round;
    [SerializeField] private float gameSpeed = 1f;
    [SerializeField] private Difficulty difficulty;
    [SerializeField] private int baseStartingMoney;
    [SerializeField] private int baseLives;
    [SerializeField] private int endOfWaveMoney;
    [Tooltip("Added to the profile's start money on this map, e.g. for maps with several lanes")]
    [SerializeField] private int extraStartingMoney = 0;
    [Tooltip("Indexed by Difficulty (Easy, Medium, Hard, Impossible). Without profiles the legacy fields above are used.")]
    [SerializeField] private DifficultyProfile[] difficultyProfiles;

    private DifficultyProfile profile;
    // Fractional income (income multipliers below 1) is banked until it adds up to whole money
    private float incomeRemainder = 0f;
    // Income from popped layers changes money hundreds of times per frame in late waves; the UI hears about
    // it once per frame (LateUpdate). Purchases and sells set Money directly and notify at once.
    private bool moneyChangePending;
    private int startingLives;

    public event Action<int> OnMoneyChanged;
    public event Action<int> OnLivesChanged;
    public event Action<int> OnRoundChanged;
    public event Action<float> OnGameSpeedChanged;

    [SerializeField] private bool isMainMenu = false;

    public List<GameObject> Buildings
    {
        get
        {
            return buildingPrefabs;
        }
    }

    public int Money 
    {
        get 
        { 
            return money; 
        } 
        
        set 
        { 
            money = value;
            moneyChangePending = false;
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
    public bool IsMainMenu => isMainMenu;
    public DifficultyProfile Profile => profile;
    public float RefundRate => Mathf.Clamp01((profile != null ? profile.refundRate : 1f) + (isMainMenu ? 0f : MetaUpgrades.RefundBonus));
    public bool IsGameOver { get; private set; }
    public bool IsWon { get; private set; }
    // Research paid for the medal of this game's win (0 when it was already owned)
    public int LastWinReward { get; private set; }
    // Lives the game started with; lives can be restored up to this value but never beyond it
    public int StartingLives => startingLives;

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
        if (GameSettings.HasSelectedDifficulty && !isMainMenu)
            difficulty = GameSettings.SelectedDifficulty;

        profile = isMainMenu ? null : FindProfile(difficulty);
        if (profile != null)
        {
            money = profile.startMoney + extraStartingMoney;
            lives = profile.lives;
        }
        else
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    money = Mathf.RoundToInt(baseStartingMoney * 1.5f);
                    lives = baseLives * 2;
                    break;
                case Difficulty.Medium:
                    money = baseStartingMoney;
                    lives = baseLives;
                    break;
                case Difficulty.Hard:
                    money = baseStartingMoney;
                    lives = Mathf.Max(1, Mathf.RoundToInt(baseLives * 0.5f));
                    break;
                case Difficulty.Impossible:
                    money = baseStartingMoney;
                    lives = 1;
                    break;
            }
        }

        if (!isMainMenu)
        {
            money += MetaUpgrades.StartMoney;
            // Impossible stays a one-life game
            if (difficulty != Difficulty.Impossible)
                lives += MetaUpgrades.StartLives;
        }

        startingLives = lives;

        if (isMainMenu)
            GameObject.FindGameObjectWithTag("Spawner").GetComponent<Spawner>().StartNextWave();
    }

    private void Start()
    {
        GameObject.FindGameObjectWithTag("End").GetComponent<End>().OnEnemyReachedExit += HandleEnemyReachedExit;
        GameObject.FindGameObjectWithTag("Spawner").GetComponent<Spawner>().OnWaveEnded += HandleEndOfWaveMoney;

        if (!isMainMenu && SaveGame.TryTakePending(SceneManager.GetActiveScene().name, out SaveGame save))
        {
            IsWon = save.won;
            StartCoroutine(save.Restore(this));
        }
    }

    [SerializeField] private GameObject canvas;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O) && canvas != null)
        {
            if (canvas.activeSelf)
                canvas.SetActive(false);
            else
                canvas.SetActive(true);
        }
    }

    private DifficultyProfile FindProfile(Difficulty wanted)
    {
        if (difficultyProfiles == null)
            return null;

        foreach (DifficultyProfile candidate in difficultyProfiles)
        {
            if (candidate != null && candidate.difficulty == wanted)
                return candidate;
        }
        return null;
    }

    // Money for popped layers, scaled by the profile's income curve
    public void AddIncome(float amount)
    {
        float multiplier = profile != null ? profile.IncomeMultiplier(Mathf.Max(1, round)) : 1f;
        if (!isMainMenu)
            multiplier *= MetaUpgrades.IncomeMultiplier;
        incomeRemainder += amount * multiplier;

        int whole = Mathf.FloorToInt(incomeRemainder);
        if (whole > 0)
        {
            incomeRemainder -= whole;
            money += whole;
            moneyChangePending = true;
        }
    }

    private void LateUpdate()
    {
        if (moneyChangePending)
        {
            moneyChangePending = false;
            OnMoneyChanged?.Invoke(money);
        }
    }

    // Gives back up to `amount` lives without going above the starting lives; returns how many were restored
    public int RestoreLives(int amount)
    {
        int restored = Mathf.Clamp(startingLives - lives, 0, Mathf.Max(0, amount));
        if (restored > 0)
            Lives += restored;
        return restored;
    }

    // Price() for code that may run without a GameManager in the scene
    public static int PriceOf(int basePrice)
    {
        return instance != null ? instance.Price(basePrice) : basePrice;
    }

    // Price after the difficulty multiplier (and meta-upgrade discount), rounded to 5 like the authored prices
    public int Price(int basePrice)
    {
        float multiplier = (profile != null ? profile.priceMultiplier : 1f) * (isMainMenu ? 1f : MetaUpgrades.PriceMultiplier);
        if (Mathf.Approximately(multiplier, 1f) || basePrice <= 0)
            return basePrice;

        float scaled = basePrice * multiplier;
        return scaled >= 20f ? Mathf.RoundToInt(scaled / 5f) * 5 : Mathf.RoundToInt(scaled);
    }

    public int SellValue(int invested, float worth = 1f)
    {
        return Mathf.RoundToInt(invested * RefundRate * worth);
    }

    // Round after which the game counts as won
    public int GetWinRound(int waveCount)
    {
        return profile != null ? Mathf.Clamp(profile.winRound, 1, waveCount) : waveCount;
    }

    private void HandleEnemyReachedExit(Enemy enemy)
    {
        int livesLost = enemy.Id + 1;
        enemy.DestroyWholeEnemy();
        Lives -= livesLost;
    }

    private void HandleEndOfWaveMoney(int round)
    {
        int bonus = endOfWaveMoney + (profile != null ? profile.EndOfWaveBonus(round) : 0);
        if (!isMainMenu)
            bonus += MetaUpgrades.WaveBonus;
        if (bonus > 0)
            Money += bonus;

        // Between waves nothing is in flight, so this is the moment to autosave for "Continue"
        if (!isMainMenu)
            SaveGame.Capture(round);
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
        if (isMainMenu || IsGameOver)
            return;
        IsGameOver = true;
        SaveGame.Delete();
        PauseMenu.Instance.GameOver();
    }

    public void GameWon()
    {
        if (isMainMenu || IsWon)
            return;
        IsWon = true;
        LastWinReward = PlayerProgress.RecordWin(SceneManager.GetActiveScene().name, difficulty);
        PauseMenu.Instance.GameWon();
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
