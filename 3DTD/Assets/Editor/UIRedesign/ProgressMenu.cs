using System.Collections.Generic;
using UnityEditor;

// Editor shortcuts for testing the level select, medals and meta upgrades without playing through levels
public static class ProgressMenu
{
    [MenuItem("3DTD/Progress/Grant All Medals")]
    private static void GrantAllMedals()
    {
        List<string> levels = new List<string>();
        foreach (LevelCatalog.Level level in LevelCatalog.Instance.levels)
            levels.Add(level.sceneName);
        PlayerProgress.GrantAllMedals(levels);
        EditorUtility.DisplayDialog("Progress", "Every level has all four medals. Research: " + PlayerProgress.Research, "OK");
    }

    [MenuItem("3DTD/Progress/Reset Progress And Savegame")]
    private static void ResetProgress()
    {
        if (!EditorUtility.DisplayDialog("Progress", "Delete all medals, Research, meta upgrades and the Continue savegame?", "Reset", "Cancel"))
            return;
        PlayerProgress.ResetAll();
        SaveGame.Delete();
    }
}
