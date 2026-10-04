using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent progression: medals per level and difficulty, the Research meta-currency and unlocked
// meta upgrades. Stored as JSON in PlayerPrefs.
public static class PlayerProgress
{
    private const string PrefsKey = "3DTD.Progress";

    [Serializable]
    private class LevelEntry
    {
        public string id;
        public int medals;
    }

    [Serializable]
    private class Data
    {
        public List<LevelEntry> levels = new List<LevelEntry>();
        public int research;
        public List<string> metaNodes = new List<string>();
    }

    private static Data data;

    public static event Action Changed;

    private static Data State
    {
        get
        {
            if (data == null)
                data = Load();
            return data;
        }
    }

    public static int Research => State.research;

    // Research paid out the first time a medal is earned
    public static int ResearchForMedal(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy: return 1;
            case Difficulty.Medium: return 2;
            case Difficulty.Hard: return 3;
            default: return 4;
        }
    }

    public static int MedalBit(Difficulty difficulty)
    {
        return 1 << (int)difficulty;
    }

    // Bit 0 Easy, 1 Medium, 2 Hard, 3 Impossible
    public static int GetMedals(string levelId)
    {
        LevelEntry entry = Find(levelId, false);
        return entry != null ? entry.medals : 0;
    }

    public static bool HasMedal(string levelId, Difficulty difficulty)
    {
        return (GetMedals(levelId) & MedalBit(difficulty)) != 0;
    }

    public static int CountMedals(int mask)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
            if ((mask & (1 << i)) != 0) count++;
        return count;
    }

    // Returns the Research earned by this win (0 when the medal was already owned)
    public static int RecordWin(string levelId, Difficulty difficulty)
    {
        if (string.IsNullOrEmpty(levelId))
            return 0;
        LevelEntry entry = Find(levelId, true);
        int bit = MedalBit(difficulty);
        if ((entry.medals & bit) != 0)
            return 0;

        entry.medals |= bit;
        int reward = ResearchForMedal(difficulty);
        State.research += reward;
        Save();
        return reward;
    }

    public static bool OwnsNode(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && State.metaNodes.Contains(nodeId);
    }

    public static IReadOnlyList<string> OwnedNodes => State.metaNodes;

    public static bool TryUnlockNode(string nodeId, int cost)
    {
        if (OwnsNode(nodeId) || State.research < cost)
            return false;
        State.research -= cost;
        State.metaNodes.Add(nodeId);
        Save();
        return true;
    }

    // Drops the cached state so the next access reads PlayerPrefs again (tests restore the player's data this way)
    public static void Reload()
    {
        data = null;
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        data = new Data();
        Save();
    }

    // Development helper: every medal on the given levels
    public static void GrantAllMedals(IEnumerable<string> levelIds)
    {
        foreach (string id in levelIds)
        {
            for (int d = 0; d < 4; d++)
                RecordWin(id, (Difficulty)d);
        }
    }

    private static LevelEntry Find(string levelId, bool create)
    {
        foreach (LevelEntry entry in State.levels)
        {
            if (entry.id == levelId)
                return entry;
        }
        if (!create)
            return null;
        LevelEntry created = new LevelEntry { id = levelId };
        State.levels.Add(created);
        return created;
    }

    private static Data Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                Data loaded = JsonUtility.FromJson<Data>(json);
                if (loaded != null)
                    return loaded;
            }
            catch (ArgumentException)
            {
                // Corrupt progress starts over
            }
        }
        return new Data();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(State));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
