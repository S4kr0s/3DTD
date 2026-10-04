using System;
using System.Collections.Generic;
using UnityEngine;

// Playable levels in unlock order, grouped into the level-select categories, with the lane paths used for
// the level-card thumbnails. Lives in Resources so menus and in-game screens can name the current level.
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "TowerDefense/LevelCatalog", order = 1)]
public class LevelCatalog : ScriptableObject
{
    public const string ResourcePath = "Progress/LevelCatalog";

    [Serializable]
    public class Category
    {
        public string id;
        public string displayName;
        public string icon;
    }

    [Serializable]
    public class Lane
    {
        [Tooltip("Path from spawn to exit, normalised to the thumbnail (0..1, y up)")]
        public List<Vector2> points = new List<Vector2>();
    }

    [Serializable]
    public class Level
    {
        public string sceneName;
        public string displayName;
        public string category;
        [Tooltip("Width / height of the path drawing")]
        public float thumbnailAspect = 1.25f;
        public List<Lane> lanes = new List<Lane>();
    }

    public List<Category> categories = new List<Category>();
    [Tooltip("Unlock order: a level unlocks once the previous one is cleared on any difficulty")]
    public List<Level> levels = new List<Level>();

    private static LevelCatalog instance;

    public static LevelCatalog Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<LevelCatalog>(ResourcePath);
            return instance;
        }
    }

    public int IndexOf(string sceneName)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].sceneName == sceneName)
                return i;
        }
        return -1;
    }

    public Level Find(string sceneName)
    {
        int index = IndexOf(sceneName);
        return index >= 0 ? levels[index] : null;
    }

    public Category FindCategory(string id)
    {
        return categories.Find(c => c.id == id);
    }

    public List<Level> LevelsIn(string categoryId)
    {
        return levels.FindAll(l => l.category == categoryId);
    }

    public bool IsCleared(int index)
    {
        return index >= 0 && index < levels.Count && PlayerProgress.GetMedals(levels[index].sceneName) != 0;
    }

    public bool IsUnlocked(int index)
    {
        return index == 0 || IsCleared(index - 1);
    }

    // "Clear Level 02 to unlock", naming the category when the previous level is in another one
    public string UnlockHint(int index)
    {
        if (index <= 0 || index >= levels.Count)
            return string.Empty;
        Level previous = levels[index - 1];
        string name = previous.displayName;
        if (previous.category != levels[index].category)
        {
            Category category = FindCategory(previous.category);
            if (category != null)
                name = category.displayName + " " + name;
        }
        return "Clear " + name + " to unlock";
    }

    // Impossible unlocks after clearing Hard on the same level
    public static bool IsDifficultyUnlocked(string sceneName, Difficulty difficulty)
    {
        return difficulty != Difficulty.Impossible || PlayerProgress.HasMedal(sceneName, Difficulty.Hard);
    }

    public string DisplayName(string sceneName)
    {
        Level level = Find(sceneName);
        return level != null ? level.displayName : sceneName;
    }

    public string FullName(string sceneName)
    {
        Level level = Find(sceneName);
        if (level == null)
            return sceneName;
        Category category = FindCategory(level.category);
        return category != null ? category.displayName + " · " + level.displayName : level.displayName;
    }
}
