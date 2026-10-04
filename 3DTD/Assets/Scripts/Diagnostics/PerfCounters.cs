// Running totals the performance benchmark (PerfScenario) reads to check that nothing is culled:
// every requested projectile, death animation and effect has to be spawned. Plain static ints, so
// counting costs nothing in the hot paths.
public static class PerfCounters
{
    public static long ProjectilesRequested;
    public static long ProjectilesSpawned;
    public static long Pops;
    public static long DeathEffectsRequested;
    public static long DeathEffectsPlayed;
    public static long EffectsRequested;
    public static long EffectsPlayed;

    public static void Reset()
    {
        ProjectilesRequested = 0;
        ProjectilesSpawned = 0;
        Pops = 0;
        DeathEffectsRequested = 0;
        DeathEffectsPlayed = 0;
        EffectsRequested = 0;
        EffectsPlayed = 0;
    }
}
