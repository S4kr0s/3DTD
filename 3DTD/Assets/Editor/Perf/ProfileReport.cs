using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// Reads a profiler capture written by the benchmark (PerfScenario -perfProfile) and logs where the main thread
// allocates managed memory (GC.Alloc samples by call path) and what the slowest frames spent their time on.
//   Unity -batchmode -projectPath ... -executeMethod ProfileReport.Run -profileFile <file.raw> -logFile -
public static class ProfileReport
{
    private const int PathDepth = 5;

    public static void Run()
    {
        string file = Argument("-profileFile");
        int exit = 0;
        try
        {
            if (string.IsNullOrEmpty(file) || !ProfilerDriver.LoadProfile(file, false))
            {
                Debug.LogError("PROFILE could not load " + file);
                exit = 1;
                return;
            }
            Report();
        }
        finally
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(exit);
        }
    }

    private static void Report()
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        Dictionary<string, (double bytes, int count)> allocations = new Dictionary<string, (double, int)>();
        List<(int frame, float ms)> frameTimes = new List<(int, float)>();
        int frames = 0;
        for (int frame = first; frame <= last; frame++)
        {
            using HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnGcMemory, false);
            if (view == null || !view.valid)
                continue;
            frames++;
            frameTimes.Add((frame, view.frameTimeMs));
            CollectAllocations(view, view.GetRootItemID(), new List<string>(), allocations);
        }

        StringBuilder text = new StringBuilder();
        double total = allocations.Values.Sum(a => a.bytes);
        text.AppendFormat(CultureInfo.InvariantCulture, "PROFILE {0} frames, GC {1:F0} B/frame on the main thread; top allocating paths:", frames, total / Math.Max(1, frames));
        foreach (var entry in allocations.OrderByDescending(a => a.Value.bytes).Take(30))
            text.AppendFormat(CultureInfo.InvariantCulture, "\n  {0,10:F0} B/frame {1,8:F1} allocs/frame  {2}", entry.Value.bytes / frames, entry.Value.count / (double)frames, entry.Key);

        // Average self time per call path over all frames
        Dictionary<string, double> averageSelf = new Dictionary<string, double>();
        foreach ((int frame, float ms) in frameTimes)
        {
            using HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
            List<(string path, float self)> selfTimes = new List<(string, float)>();
            CollectSelfTimes(view, view.GetRootItemID(), new List<string>(), selfTimes, 0f);
            foreach ((string path, float self) in selfTimes)
            {
                averageSelf.TryGetValue(path, out double sum);
                averageSelf[path] = sum + self;
            }
        }
        text.AppendFormat(CultureInfo.InvariantCulture, "\nPROFILE average frame {0:F2} ms; top self times per frame:", frameTimes.Average(f => f.ms));
        foreach (var entry in averageSelf.OrderByDescending(a => a.Value).Take(40))
            text.AppendFormat(CultureInfo.InvariantCulture, "\n  {0,6:F2} ms  {1}", entry.Value / Math.Max(1, frames), entry.Key);

        text.Append("\nPROFILE slowest frames (top self times):");
        foreach ((int frame, float ms) in frameTimes.OrderByDescending(f => f.ms).Take(6))
        {
            using HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
            List<(string path, float self)> selfTimes = new List<(string, float)>();
            CollectSelfTimes(view, view.GetRootItemID(), new List<string>(), selfTimes, 0.3f);
            text.AppendFormat(CultureInfo.InvariantCulture, "\n  frame {0}: {1:F1} ms", frame - first, ms);
            foreach ((string path, float self) in selfTimes.OrderByDescending(s => s.self).Take(8))
                text.AppendFormat(CultureInfo.InvariantCulture, "\n    {0,6:F2} ms  {1}", self, path);
        }
        Debug.Log(text.ToString());
    }

    private static void CollectAllocations(HierarchyFrameDataView view, int id, List<string> path, Dictionary<string, (double bytes, int count)> allocations)
    {
        List<int> children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int child in children)
        {
            float gc = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
            if (gc <= 0f)
                continue;
            string name = view.GetItemName(child);
            if (name == "GC.Alloc")
            {
                string key = string.Join(" > ", path.Skip(Math.Max(0, path.Count - PathDepth)));
                int calls = (int)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnCalls);
                allocations.TryGetValue(key, out var sum);
                allocations[key] = (sum.bytes + gc, sum.count + calls);
                continue;
            }
            path.Add(name);
            CollectAllocations(view, child, path, allocations);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static void CollectSelfTimes(HierarchyFrameDataView view, int id, List<string> path, List<(string, float)> selfTimes, float minimum)
    {
        List<int> children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int child in children)
        {
            string name = view.GetItemName(child);
            path.Add(name);
            float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
            if (self >= minimum && self > 0f)
                selfTimes.Add((string.Join(" > ", path.Skip(Math.Max(0, path.Count - PathDepth))), self));
            CollectSelfTimes(view, child, path, selfTimes, minimum);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }
}
