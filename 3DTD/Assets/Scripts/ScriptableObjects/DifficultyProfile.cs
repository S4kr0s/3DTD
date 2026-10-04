using System.Collections.Generic;
using UnityEngine;

// Everything a difficulty changes. GameManager picks one per game (see GameSettings.SelectedDifficulty).
[CreateAssetMenu(fileName = "DifficultyProfile", menuName = "TowerDefense/DifficultyProfile", order = 0)]
public class DifficultyProfile : ScriptableObject
{
    [System.Serializable]
    public struct IncomeBracket
    {
        [Tooltip("First round (1-based) this multiplier applies to")]
        public int fromRound;
        public float multiplier;
    }

    public Difficulty difficulty = Difficulty.Medium;

    [Header("Start")]
    public int startMoney = 350;
    public int lives = 100;

    [Header("Economy")]
    [Tooltip("Applied to towers, building blocks and upgrade modules")]
    public float priceMultiplier = 1f;
    [Tooltip("Multiplier for the money earned per popped layer, by round. Brackets are sorted by fromRound.")]
    public List<IncomeBracket> incomeBrackets = new List<IncomeBracket> { new IncomeBracket { fromRound = 1, multiplier = 1f } };
    [Tooltip("Paid when a wave ends: base + perRound * round")]
    public int endOfWaveBonusBase = 25;
    public float endOfWaveBonusPerRound = 2f;
    [Tooltip("Share of everything invested in a building that selling refunds")]
    [Range(0f, 1f)]
    public float refundRate = 0.75f;

    [Header("Enemies")]
    public float enemySpeedMultiplier = 1f;
    [Tooltip("Applied to layers with at least 5 health; 1-health tetrahedron layers are never scaled")]
    public float layerHealthMultiplier = 1f;
    public float bossHealthMultiplier = 1f;
    [Tooltip("Added to the armor of Armored enemies")]
    public float armorBonus = 0f;
    [Tooltip("Added to the shield hits of Shielded enemies")]
    public int shieldBonus = 0;

    [Header("Length")]
    [Tooltip("The game counts as won after this round, or after the level's last wave if that comes first")]
    public int winRound = 60;
    [Tooltip("Growth per round of the repeated last wave after all authored waves")]
    public float freeplayScaling = 0.1f;

    public float IncomeMultiplier(int round)
    {
        float multiplier = 1f;
        int bestFrom = int.MinValue;
        foreach (IncomeBracket bracket in incomeBrackets)
        {
            if (bracket.fromRound <= round && bracket.fromRound >= bestFrom)
            {
                bestFrom = bracket.fromRound;
                multiplier = bracket.multiplier;
            }
        }
        return multiplier;
    }

    public int EndOfWaveBonus(int round)
    {
        return Mathf.Max(0, Mathf.RoundToInt(endOfWaveBonusBase + endOfWaveBonusPerRound * round));
    }
}
