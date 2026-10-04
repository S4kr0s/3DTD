// Settings that survive scene loads, chosen in the main menu
public static class GameSettings
{
    public static bool HasSelectedDifficulty { get; private set; }
    public static Difficulty SelectedDifficulty { get; private set; } = Difficulty.Medium;

    public static void SelectDifficulty(Difficulty difficulty)
    {
        SelectedDifficulty = difficulty;
        HasSelectedDifficulty = true;
    }
}
