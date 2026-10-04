// Physics layers the code relies on (ProjectSettings/TagManager.asset). The collision matrix
// (ProjectSettings/DynamicsManager.asset) only lets Enemy touch Default (the End trigger) and
// Ignore Raycast (tower ranges, the remaining collider-based projectiles); see CLAUDE.md.
public static class GameLayers
{
    public const int Default = 0;
    public const int IgnoreRaycast = 2;
    public const int AnchorPoints = 8;
    public const int Map = 9;
    public const int Enemy = 11;

    public const int EnemyMask = 1 << Enemy;
}
