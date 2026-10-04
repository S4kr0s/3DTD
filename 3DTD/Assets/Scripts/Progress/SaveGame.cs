using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// One autosave slot for "Continue" (redesign B3). Written between waves (when no enemy is in flight) and
// restored after the level scene loaded: economy, wave, every player-built building with its upgrades.
[Serializable]
public class SaveGame
{
    [Serializable]
    public class SavedBuilding
    {
        public int palette;
        public Vector3 position;
        public Quaternion rotation;
        public int invested;
        public int targetBehaviour;
        public List<int> pathTiers = new List<int>();
        public bool hasAim;
        public Quaternion aimRotation;
    }

    public string sceneName;
    public int difficulty;
    public int round;
    public int money;
    public int lives;
    public bool won;
    public string savedAt;
    public List<SavedBuilding> buildings = new List<SavedBuilding>();

    private static string FilePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    // Set by the main menu's Continue button; consumed by GameManager once the level is loaded
    public static SaveGame PendingRestore { get; private set; }

    public static bool Exists()
    {
        return File.Exists(FilePath);
    }

    public static SaveGame Load()
    {
        if (!Exists())
            return null;
        try
        {
            SaveGame save = JsonUtility.FromJson<SaveGame>(File.ReadAllText(FilePath));
            return save != null && !string.IsNullOrEmpty(save.sceneName) ? save : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Savegame could not be read: " + exception.Message);
            return null;
        }
    }

    public static void Delete()
    {
        if (Exists())
            File.Delete(FilePath);
    }

    // "Level 01 · Hard · Wave 12"
    public string Summary()
    {
        LevelCatalog catalog = LevelCatalog.Instance;
        string level = catalog != null ? catalog.DisplayName(sceneName) : sceneName;
        return level + " · " + (Difficulty)difficulty + " · Wave " + round;
    }

    public static void BeginRestore(SaveGame save)
    {
        PendingRestore = save;
        GameSettings.SelectDifficulty((Difficulty)save.difficulty);
        SceneManager.LoadScene(save.sceneName);
    }

    public static void Capture(int completedRound)
    {
        GameManager game = GameManager.Instance;
        if (game == null || game.IsMainMenu || game.IsGameOver)
            return;

        SaveGame save = new SaveGame
        {
            sceneName = SceneManager.GetActiveScene().name,
            difficulty = (int)game.Difficulty,
            round = completedRound,
            money = game.Money,
            lives = game.Lives,
            won = game.IsWon,
            savedAt = DateTime.UtcNow.ToString("o"),
        };

        foreach (Building building in UnityEngine.Object.FindObjectsByType<Building>(FindObjectsInactive.Exclude))
        {
            if (!building.PlacedByPlayer)
                continue;

            SavedBuilding saved = new SavedBuilding
            {
                palette = building.PaletteIndex,
                position = building.transform.position,
                rotation = building.transform.rotation,
                invested = building.Invested,
            };
            if (building is Tower tower)
            {
                saved.targetBehaviour = (int)tower.TargetBehaviour;
                foreach (UpgradePath path in tower.UpgradeManager.GetUpgradePaths())
                    saved.pathTiers.Add(path != null ? path.activeUpgrades : 0);
                if (tower.UseRotationSlider && tower.Rotationbase != null)
                {
                    saved.hasAim = true;
                    saved.aimRotation = tower.Rotationbase.transform.rotation;
                }
            }
            save.buildings.Add(saved);
        }

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(save));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Savegame could not be written: " + exception.Message);
        }
    }

    // Runs on the GameManager after the scene's Start methods; returns false if the pending save is for another scene
    public static bool TryTakePending(string scene, out SaveGame save)
    {
        save = PendingRestore;
        if (save == null || save.sceneName != scene)
            return false;
        PendingRestore = null;
        return true;
    }

    public IEnumerator Restore(GameManager game)
    {
        // Let Spawner, End and the HUD finish their Start first
        yield return null;

        game.Money = money;
        game.Lives = lives;
        game.Round = round;
        if (Spawner.Instance != null)
            Spawner.Instance.RestoreProgress(won);
        if (MapHighlighter.Instance != null && round > 0)
            MapHighlighter.Instance.highlight = false;

        List<KeyValuePair<Building, SavedBuilding>> restored = new List<KeyValuePair<Building, SavedBuilding>>();
        foreach (SavedBuilding saved in buildings)
        {
            if (saved.palette < 0 || saved.palette >= game.Buildings.Count || game.Buildings[saved.palette] == null)
                continue;
            GameObject built = UnityEngine.Object.Instantiate(game.Buildings[saved.palette], saved.position, saved.rotation);
            if (built.TryGetComponent(out Building building))
            {
                building.MarkPlacedByPlayer(saved.palette);
                building.AddInvestment(saved.invested);
                restored.Add(new KeyValuePair<Building, SavedBuilding>(building, saved));
            }
        }

        // Towers set up their action strategy in Start; upgrades that swap it must come after that
        yield return null;

        foreach (KeyValuePair<Building, SavedBuilding> entry in restored)
        {
            if (!(entry.Key is Tower tower) || tower == null)
                continue;
            SavedBuilding saved = entry.Value;
            tower.ChangeTargettingBehaviour((TargetBehaviour)saved.targetBehaviour);
            UpgradePath[] paths = tower.UpgradeManager.GetUpgradePaths();
            for (int p = 0; p < paths.Length && p < saved.pathTiers.Count; p++)
            {
                if (paths[p] == null || paths[p].UpgradeModules == null)
                    continue;
                for (int t = 0; t < saved.pathTiers[p] && t < paths[p].UpgradeModules.Length; t++)
                {
                    UpgradeModule module = paths[p].UpgradeModules[t];
                    if (module != null && !module.IsActive)
                        module.ApplyUpgrade(tower.gameObject);
                }
            }
            tower.UpgradeManager.CheckPathBlocking();
            if (saved.hasAim && tower.Rotationbase != null)
                tower.Rotationbase.transform.rotation = saved.aimRotation;
        }
    }
}
