using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    private System.Random random = new System.Random();
    public readonly int[] EnemyThresholdsBasic = { 0, 3, 5, 7, 9, 11 };
    public readonly int[] EnemyCutoffsBasic = { 10, 20, 30, 35, 40, 45 };

    [SerializeField] private WaveGenerationFormula defaultFormula = WaveGenerationFormula.LINEAR;
    [SerializeField] private int defaultBaseValue = 10;
    [SerializeField] private float defaultGrowthFactor = 1.25f;
    [SerializeField] private int defaultWaveAmount = 31;
    [SerializeField] private string generatedFileName = "generated.json";

    [Header("Import from the wave designer (Tools/BalanceDashboard/write_waves.py writes waves-generated.json)")]
    [SerializeField] private string designerJsonPath = "../../Tools/BalanceDashboard/waves-generated.json";
    [SerializeField] private string importSetName = "Beginner01";
    [SerializeField] private string importFolder = "Assets/ScriptableObjects/WaveData";

#if UNITY_EDITOR
    // Creates or updates one WaveData asset per round of a generated set. write_waves.py does the same
    // and also assigns the set to the level's Spawner; this is for working inside the Editor.
    [ContextMenu("Import Waves From Wave Designer Json")]
    private void ImportDesignerWaves()
    {
        string jsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, designerJsonPath));
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("Wave designer output not found: " + jsonPath);
            return;
        }

        JArray waves = JObject.Parse(File.ReadAllText(jsonPath))[importSetName] as JArray;
        if (waves == null)
        {
            Debug.LogError("Set '" + importSetName + "' not found in " + jsonPath);
            return;
        }

        // Enemy ids index Default Enemy.allPossibleEnemyData
        GameObject enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Default Enemy.prefab");
        UnityEditor.SerializedProperty dataById = new UnityEditor.SerializedObject(enemyPrefab.GetComponent<Enemy>()).FindProperty("allPossibleEnemyData");

        string folder = importFolder + "/" + importSetName;
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder(importFolder, importSetName);

        foreach (JToken wave in waves)
        {
            string assetPath = folder + "/" + importSetName + " " + (string)wave["name"] + ".asset";
            WaveData data = UnityEditor.AssetDatabase.LoadAssetAtPath<WaveData>(assetPath);
            bool isNew = data == null;
            if (isNew)
                data = ScriptableObject.CreateInstance<WaveData>();

            data.enemiesToSpawn = new List<EnemyData>();
            data.enemySpawnCount = new List<int>();
            data.spawnDelay = new List<float>();
            data.enemyTraits = new List<EnemyTrait>();
            foreach (JToken entry in wave["entries"])
            {
                data.enemiesToSpawn.Add((EnemyData)dataById.GetArrayElementAtIndex((int)entry["enemy"]).objectReferenceValue);
                data.enemySpawnCount.Add((int)entry["count"]);
                data.spawnDelay.Add((float)entry["delay"]);
                data.enemyTraits.Add((EnemyTrait)(int)entry["traits"]);
            }

            if (isNew)
                UnityEditor.AssetDatabase.CreateAsset(data, assetPath);
            else
                UnityEditor.EditorUtility.SetDirty(data);
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Imported " + waves.Count + " waves of " + importSetName + " into " + folder);
    }
#endif

    [ContextMenu("Generate Waves Json")]
    private void GenerateWavesJson()
    {
        List<List<EnemyWaveGenData>> generated = GenerateWaves(defaultFormula, defaultBaseValue, defaultGrowthFactor, defaultWaveAmount);
        string json = JsonConvert.SerializeObject(generated, Formatting.Indented);
        string filePath = Path.Combine(Application.dataPath, generatedFileName);
        File.WriteAllText(filePath, json);
        Debug.Log("Wave-Gen JSON saved to " + filePath);
    }

    public List<List<EnemyWaveGenData>> GenerateWaves(WaveGenerationFormula formula, int baseValue, float growthFactor, int waveAmount)
    {
        List<List<EnemyWaveGenData>> data = new List<List<EnemyWaveGenData>>();

        for (int i = 1; i < waveAmount; i++)
        {
            data.Add(GenerateWave(CalculateTotalCurrency(formula, baseValue, growthFactor, i), i));
        }

        return data;
    }

    private int CalculateTotalCurrency(WaveGenerationFormula formula, int baseValue, float growthFactor, int waveNumber)
    {
        int totalCurrency = 0;

        switch (formula)
        {
            case WaveGenerationFormula.LINEAR:
                totalCurrency = Mathf.RoundToInt(baseValue + (waveNumber * growthFactor));
                break;
            case WaveGenerationFormula.EXPONENTIAL:
                totalCurrency = Mathf.RoundToInt(baseValue * Mathf.Pow(growthFactor, waveNumber));
                break;
            case WaveGenerationFormula.HYBRID:
                totalCurrency = Mathf.RoundToInt(baseValue + (Mathf.Pow(waveNumber, 2) * growthFactor));
                break;
            default:
                break;
        }

        return totalCurrency;
    }

    public List<EnemyWaveGenData> GenerateWave(int totalCurrency, int waveNumber)
    {
        List<EnemyWaveGenData> enemies = new List<EnemyWaveGenData>();
        enemies.Add(new EnemyWaveGenData(1, 0));
        enemies.Add(new EnemyWaveGenData(2, 0));
        enemies.Add(new EnemyWaveGenData(3, 0));
        enemies.Add(new EnemyWaveGenData(4, 0));
        enemies.Add(new EnemyWaveGenData(5, 0));
        enemies.Add(new EnemyWaveGenData(6, 0));

        while (totalCurrency > 0) 
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (waveNumber >= EnemyThresholdsBasic[i] && waveNumber <= EnemyCutoffsBasic[i] && totalCurrency >= enemies[i].Id)
                {
                    enemies[i].Amount++;
                    totalCurrency -= enemies[i].Id;
                }
                else if (totalCurrency > 0)
                {
                    enemies[0].Amount++;
                    totalCurrency -= enemies[0].Id;
                }
            }
        }

        return enemies;
    }

    private bool RandomChance()
    {
        return random.NextDouble() < 0.5;
    }
}

public class EnemyWaveGenData
{
    public int Id;
    public int Amount;

    public EnemyWaveGenData(int id, int amount)
    {
        Id = id;
        Amount = amount;
    }
}

public enum WaveGenerationFormula
{
    LINEAR,
    EXPONENTIAL,
    HYBRID,
}
