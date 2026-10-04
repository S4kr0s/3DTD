using Unity.Profiling;

// Profiler markers of the effect system, read by the performance benchmark (PerfScenario)
public static class EffectMarkers
{
    public static readonly ProfilerMarker Batched = new ProfilerMarker("3DTD.Effects.Batched");
    public static readonly ProfilerMarker Emit = new ProfilerMarker("3DTD.Effects.Emit");
    public static readonly ProfilerMarker Particles = new ProfilerMarker("3DTD.Effects.Particles");
    public static readonly ProfilerMarker Retire = new ProfilerMarker("3DTD.Effects.Retire");
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Per particle system: how often the effect system emitted into it and what that cost (logged by PerfScenario)
public static class EffectStats
{
    private sealed class Entry
    {
        public string Name;
        public long Calls, Particles, Ticks, MaxTicks, FirstCalls, FirstTicks;
        public int CountAtMax, AliveAtMax;
    }

    private static readonly System.Collections.Generic.Dictionary<UnityEngine.ParticleSystem, Entry> entries =
        new System.Collections.Generic.Dictionary<UnityEngine.ParticleSystem, Entry>();

    private static int lastFrame = -1;

    public static long Now => System.Diagnostics.Stopwatch.GetTimestamp();

    public static void Record(UnityEngine.ParticleSystem system, UnityEngine.Object owner, int count, int alive, long startTicks)
    {
        long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
        if (!entries.TryGetValue(system, out Entry entry))
        {
            entry = new Entry { Name = owner.name + "/" + system.name };
            entries.Add(system, entry);
        }
        entry.Calls++;
        if (UnityEngine.Time.frameCount != lastFrame)
        {
            lastFrame = UnityEngine.Time.frameCount;
            entry.FirstCalls++;
            entry.FirstTicks += ticks;
        }
        entry.Particles += count;
        entry.Ticks += ticks;
        if (ticks > entry.MaxTicks)
        {
            entry.MaxTicks = ticks;
            entry.CountAtMax = count;
            entry.AliveAtMax = alive;
        }
    }

    public static void Reset()
    {
        entries.Clear();
    }

    public static string Report(int top)
    {
        var list = new System.Collections.Generic.List<Entry>(entries.Values);
        list.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
        double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        var text = new System.Text.StringBuilder("PERF EMIT (system: calls, particles, total ms, us/particle, max ms at count/alive)");
        for (int i = 0; i < list.Count && i < top; i++)
        {
            Entry e = list[i];
            text.Append("\n  ").Append(e.Name).Append(": ").Append(e.Calls).Append(", ").Append(e.Particles).Append(", ")
                .Append((e.Ticks * toMs).ToString("F1")).Append(" ms, ")
                .Append((e.Ticks * toMs * 1000.0 / System.Math.Max(1, e.Particles)).ToString("F2")).Append(" us, max ")
                .Append((e.MaxTicks * toMs).ToString("F2")).Append(" ms at ").Append(e.CountAtMax).Append('/').Append(e.AliveAtMax)
                .Append(", first in frame ").Append(e.FirstCalls).Append("x ").Append((e.FirstTicks * toMs).ToString("F1")).Append(" ms");
        }
        return text.ToString();
    }
}
#endif
