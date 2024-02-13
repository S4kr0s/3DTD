using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    private System.Random random = new System.Random();
    public readonly int[] EnemyThresholdsBasic = { 0, 3, 5, 7, 9, 11 };
    public readonly int[] EnemyCutoffsBasic = { 10, 20, 30, 35, 40, 45 };

    private void Start()
    {
        List<List<EnemyWaveGenData>> generated = GenerateWaves(WaveGenerationFormula.LINEAR, 10, 1.25f, 31);
        string json = JsonConvert.SerializeObject(generated, Formatting.Indented);
        string filePath = "C:\\Users\\phili\\Downloads\\generated.json";
        File.WriteAllText(filePath, json);
        System.Console.WriteLine("Wave-Gen JSON saved to " + filePath);
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
