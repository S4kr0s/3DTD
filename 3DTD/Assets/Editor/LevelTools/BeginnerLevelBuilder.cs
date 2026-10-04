using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Generates the Beginner levels 02-05 from Beginner Level 01 (batch: -executeMethod BeginnerLevelBuilder.Run,
// project closed in the Editor; also menu 3DTD > Levels > Rebuild Beginner Levels).
//
// Each level is a copy of the Beginner Level 01 scene (GAME_SETUP, lights, void walls) whose map is replaced by
// a generated map prefab in Prefabs/Maps/BeginnerLevel0X.prefab: the track tiles of Level 01 laid along a path,
// a nested SpawnerNew instance, EndNew, the WaypointManager and the starter blocks. The Spawner's wave list is
// a list override on that nested instance, written by Tools/BalanceDashboard/write_waves.py (container mode),
// so re-running this builder drops the waves: run write_waves.py and extract.py afterwards.
//
// The first run also moves the old Beginner Level 02/03 (scenes, the old map prefab, the old wave set) to
// "Disabled" folders and rewrites the build list and the level catalog.
public static class BeginnerLevelBuilder
{
    private const string TemplateScene = "Assets/Scenes/Beginner Level 01.unity";
    private const string StraightPrefab = "Assets/SimplePrefabs/MapBlockSimple.prefab";
    private const string TurnPrefab = "Assets/SimplePrefabs/MapBlockTurn.prefab";
    private const string EdgePrefab = "Assets/SimplePrefabs/MapBlockHalfstep.prefab";
    private const string SpawnerPrefab = "Assets/SimplePrefabs/SpawnerNew.prefab";
    private const string EndPrefab = "Assets/SimplePrefabs/EndNew.prefab";
    private const string BlockPrefab = "Assets/Prefabs/MAP BuildingBlock.prefab";
    private const string MapFolder = "Assets/Prefabs/Maps";

    private static readonly string[] BuildOrder =
    {
        "Assets/Scenes/MainMenuLevel.unity",
        "Assets/Scenes/Beginner Level 01.unity",
        "Assets/Scenes/Beginner Level 02.unity",
        "Assets/Scenes/Beginner Level 03.unity",
        "Assets/Scenes/Beginner Level 04.unity",
        "Assets/Scenes/Beginner Level 05.unity",
        "Assets/Scenes/Intermediate Level 01.unity",
        "Assets/Scenes/Intermediate Level 02.unity",
    };

    // Old content that the new levels replace: source -> destination (moved once, GUIDs are kept)
    private static readonly string[,] Retired =
    {
        { "Assets/Scenes/Beginner Level 02.unity", "Assets/Scenes/Disabled/Beginner Level 02 (Old).unity" },
        { "Assets/Scenes/Beginner Level 03.unity", "Assets/Scenes/Disabled/Beginner Level 03 (Old).unity" },
        { "Assets/Scenes/Beginner Level 03", "Assets/Scenes/Disabled/Beginner Level 03 (Old)" },
        { "Assets/Prefabs/Maps/BeginnerLevel02.prefab", "Assets/Prefabs/Maps/Disabled/BeginnerLevel02 (Old).prefab" },
        { "Assets/ScriptableObjects/WaveData/Beginner02", "Assets/ScriptableObjects/WaveData/Disabled/Beginner02 (Old)" },
    };

    private static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

    private class Level
    {
        public string Scene;
        public string Map;
        public Func<Track> Path;
        public Vector3[] Blocks;
    }

    // The maps. Tiles lie on a unit lattice; enemies fly 0.75 above a tile (Track.Lift). Starter blocks are
    // unit cubes, placed so their faces line up with the lanes the level is about. Each level's waves (sets
    // Beginner02-05 in Tools/BalanceDashboard/wave-design.json) are tuned to the same strategy.
    private static Level[] Levels()
    {
        return new[]
        {
            // 02 Ring Road: the lane runs around the whole map, climbs onto a raised deck for the far half and
            // drops back down before the exit next to the spawn. The only starter blocks are in the middle, 4.5-8.5
            // units from the ring: a Sniper covers all of it, Hangar and Rockets reach parts of the near sides,
            // short range has to build out with blocks.
            new Level
            {
                Scene = "Beginner Level 02",
                Map = "BeginnerLevel02",
                Path = () => new Track(new Vector3(0, 1, 0), Y, X)
                    .F(17).Turn(Z).F(2)
                    .Concave(3).Convex(1).F(8).Turn(-X)
                    .F(17).Turn(-Z).F(2)
                    .Convex(3).Concave(1).F(7).End(),
                Blocks = new[]
                {
                    new Vector3(9, 1, 6), new Vector3(8, 1, 6), new Vector3(10, 1, 6),
                    new Vector3(9, 1, 5), new Vector3(9, 1, 7), new Vector3(9, 2, 6),
                },
            },
            // 03 Back to Back: two folded stacks. Each runs along the top of a slab, wraps around its end, comes
            // back upside down underneath and drops to the floor below, so three passes of the lane are stacked
            // within 4 units. The starter blocks sit between the stacks: short range and splash towers (Mine
            // Factory, Rockets, Laser, Core) reach up to six passes from one spot.
            new Level
            {
                Scene = "Beginner Level 03",
                Map = "BeginnerLevel03",
                Path = () => new Track(new Vector3(-1, 4, 0), Y, X)
                    .F(10).Convex(1).Convex(1).F(8)
                    .Concave(3).Concave(1).F(8).Turn(Z).F(4).Turn(-X)
                    .F(9).Concave(3).Concave(1).F(8)
                    .Convex(1).Convex(1).F(10).End(),
                Blocks = new[]
                {
                    new Vector3(3, 2.5f, 2.5f), new Vector3(4, 2.5f, 2.5f),
                    new Vector3(6, 2.5f, 2.5f), new Vector3(7, 2.5f, 2.5f),
                    new Vector3(12, 2, 2.5f),
                },
            },
            // 04 Upside Down: after a flip over the edge of the canopy the lane runs upside down along its
            // underside in three long straights, falls down a 6 unit shaft and leaves along the ground. The
            // starter blocks hang in line with the straights (their bottom faces level with the lane), where a
            // Beam Tower fires down the straight and pierces the queue; no spot sees much of the rest of the lane.
            new Level
            {
                Scene = "Beginner Level 04",
                Map = "BeginnerLevel04",
                Path = () => new Track(new Vector3(0, 7, 1), Y, -Z)
                    .F(1).Convex(1).Convex(1).Turn(X)
                    .F(12).Turn(Z).F(3).Turn(-X)
                    .F(12).Turn(Z).F(3).Turn(X)
                    .F(11).Concave(6).Concave(1).Turn(-Z)
                    .F(10).End(),
                Blocks = new[]
                {
                    new Vector3(-1, 6, 1), new Vector3(14, 6, 1),
                    new Vector3(-1, 6, 5), new Vector3(14, 6, 5),
                    new Vector3(-1, 6, 9), new Vector3(12, 6, 9),
                    new Vector3(10, 0, 10), new Vector3(10, 0, -2),
                },
            },
            // 05 Corkscrew: a square helix that winds down two and a quarter turns around a central pillar, 3
            // units per turn, 4 units out from the pillar. From the pillar, a tower that covers every direction
            // and reaches 4+ units (Hangar, Rockets on the side faces) hits several sides and turns at once, short
            // range barely reaches, and the swarm waves of this level waste a Sniper's big single shots.
            new Level
            {
                Scene = "Beginner Level 05",
                Map = "BeginnerLevel05",
                Path = () => Corkscrew(),
                Blocks = new[]
                {
                    new Vector3(0, 1, 0), new Vector3(0, 4, 0), new Vector3(0, 7, 0),
                },
            },
        };
    }

    private static Track Corkscrew()
    {
        // Sides 8 long around the pillar; one step down on three sides of every turn (none on the west side),
        // so the turns are 3 units apart. Nine sides: two and a quarter turns from y 7 down to the ground.
        Track track = new Track(new Vector3(-7, 7, -4), Y, X).F(4);
        Vector3[] sides = { X, Z, -X, -Z };
        for (int side = 0; side < 9; side++)
        {
            // corner tile, then the side; the first side starts without a corner
            if (side > 0)
                track.Turn(sides[side % 4]);
            int ahead = side > 0 ? 3 : 2;
            if (side % 4 == 3)
                track.F(ahead + 4);
            else
                track.F(ahead).Convex(1).Concave(1).F(3);
        }
        return track.F(4).End();
    }

    [MenuItem("3DTD/Levels/Rebuild Beginner Levels")]
    public static void BuildAll()
    {
        RetireOldLevels();
        foreach (Level level in Levels())
            BuildLevel(level);
        WriteBuildSettings();
        RebuildLevelCatalog();
        AssetDatabase.SaveAssets();
        Debug.Log("BeginnerLevelBuilder: done");
    }

    public static void Run()
    {
        int code = 0;
        try
        {
            BuildAll();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            code = 1;
        }
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    // ---- retiring the old levels -----------------------------------------------------------------------

    private static void RetireOldLevels()
    {
        for (int i = 0; i < Retired.GetLength(0); i++)
        {
            string from = Retired[i, 0], to = Retired[i, 1];
            // Once moved, the source path holds the new level, so it must not move again
            if (AssetDatabase.LoadMainAssetAtPath(to) != null || AssetDatabase.IsValidFolder(to))
                continue;
            if (AssetDatabase.LoadMainAssetAtPath(from) == null && !AssetDatabase.IsValidFolder(from))
                continue;
            EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Moving " + from + " failed: " + error);
            Debug.Log("BeginnerLevelBuilder: moved " + from + " -> " + to);
        }
    }

    // ---- one level -------------------------------------------------------------------------------------

    private static void BuildLevel(Level level)
    {
        string scenePath = "Assets/Scenes/" + level.Scene + ".unity";
        if (AssetDatabase.LoadMainAssetAtPath(scenePath) == null && !AssetDatabase.CopyAsset(TemplateScene, scenePath))
            throw new InvalidOperationException("Could not copy " + TemplateScene + " to " + scenePath);

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefab);
        GameObject setup = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "GAME_SETUP")
                setup = root;
            // the template's map (BeginnerLevel01 with track, spawner, exit and waypoints) and its starter blocks
            bool oldMap = root.name.StartsWith("BeginnerLevel");
            bool oldBlock = PrefabUtility.IsAnyPrefabInstanceRoot(root) && PrefabUtility.GetCorrespondingObjectFromSource(root) == blockPrefab;
            if (oldMap || oldBlock)
                Object.DestroyImmediate(root);
        }
        if (setup == null)
            throw new InvalidOperationException(scenePath + " has no GAME_SETUP");

        Track track = level.Path();
        GameObject map = BuildMap(level, track, out Bounds bounds);
        EnsureFolder(MapFolder);
        string mapPath = MapFolder + "/" + level.Map + ".prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(map, mapPath, InteractionMode.AutomatedAction);

        // The camera starts looking at the middle of the map (OrbitCamera orbits CameraAnchor), from a distance
        // that frames the map like Level 01 (22 x 15 units at the default distance of 20)
        Transform cameraAnchor = setup.transform.Find("CameraAnchor");
        if (cameraAnchor != null)
        {
            cameraAnchor.position = new Vector3(Mathf.Round(bounds.center.x * 2) / 2, Mathf.Round((bounds.center.y - 1) * 2) / 2, Mathf.Round(bounds.center.z * 2) / 2);
            OrbitCamera orbit = cameraAnchor.GetComponentInChildren<OrbitCamera>(true);
            if (orbit != null)
            {
                float extent = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y * 1.5f);
                SerializedObject orbitData = new SerializedObject(orbit);
                orbitData.FindProperty("distance").floatValue = Mathf.Clamp(Mathf.Round(extent + 2), 15, 20);
                orbitData.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + scenePath);
        Debug.Log(string.Format("BeginnerLevelBuilder: {0}: {1} tiles, {2} waypoints, path {3:0.0}, {4} starter blocks",
            level.Scene, track.Pieces.Count, track.Waypoints.Count, track.Length(), level.Blocks.Length));
    }

    private static GameObject BuildMap(Level level, Track track, out Bounds bounds)
    {
        GameObject straight = AssetDatabase.LoadAssetAtPath<GameObject>(StraightPrefab);
        GameObject turn = AssetDatabase.LoadAssetAtPath<GameObject>(TurnPrefab);
        GameObject edge = AssetDatabase.LoadAssetAtPath<GameObject>(EdgePrefab);

        GameObject root = new GameObject(level.Map);
        Transform tiles = new GameObject("Track").transform;
        tiles.SetParent(root.transform, false);
        bounds = new Bounds(track.Start, Vector3.zero);
        int index = 0;
        foreach (Track.Piece piece in track.Pieces)
        {
            GameObject prefab = piece.Kind == Track.PieceKind.Turn ? turn : piece.Kind == Track.PieceKind.Edge ? edge : straight;
            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tiles);
            tile.name = prefab.name + " (" + index++ + ")";
            tile.transform.SetPositionAndRotation(piece.Position, piece.Rotation);
            bounds.Encapsulate(piece.Position);
        }

        GameObject spawner = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPrefab), root.transform);
        spawner.transform.SetPositionAndRotation(track.Start + 0.2f * track.StartNormal, Quaternion.LookRotation(-track.StartDir, track.StartNormal));
        // GameManager and the HUD find it by tag; the prefab itself is untagged (Level 01 tags its instance)
        spawner.tag = "Spawner";
        GameObject end = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EndPrefab), root.transform);
        end.transform.SetPositionAndRotation(track.Exit + 0.2f * track.ExitNormal, Quaternion.LookRotation(-track.ExitDir, track.ExitNormal));

        GameObject manager = new GameObject("WaypointManager");
        manager.tag = "WaypointManager";
        manager.transform.SetParent(root.transform, false);
        Waypoints waypoints = manager.AddComponent<Waypoints>();
        for (int i = 0; i < track.Waypoints.Count; i++)
        {
            GameObject point = new GameObject("Waypoint (" + (i + 1) + ")");
            point.tag = "Waypoint";
            point.transform.SetParent(manager.transform, false);
            point.transform.position = track.Waypoints[i];
        }

        SerializedObject spawnerData = new SerializedObject(spawner.GetComponent<Spawner>());
        SerializedProperty lanes = spawnerData.FindProperty("waypoints");
        lanes.arraySize = 1;
        lanes.GetArrayElementAtIndex(0).objectReferenceValue = waypoints;
        spawnerData.ApplyModifiedPropertiesWithoutUndo();

        Transform blocks = new GameObject("Starter Blocks").transform;
        blocks.SetParent(root.transform, false);
        GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefab);
        for (int i = 0; i < level.Blocks.Length; i++)
        {
            GameObject block = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab, blocks);
            block.name = "MAP BuildingBlock (" + (i + 1) + ")";
            block.transform.position = level.Blocks[i];
            bounds.Encapsulate(level.Blocks[i]);
        }
        return root;
    }

    // ---- project settings ------------------------------------------------------------------------------

    private static void WriteBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in BuildOrder)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new InvalidOperationException("Missing build scene " + path);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // The level select lists the build scenes in order; its catalog bakes the lane thumbnails from the scenes
    private static void RebuildLevelCatalog()
    {
        MethodInfo create = typeof(UIRedesignBuilder).GetMethod("CreateLevelCatalog", BindingFlags.Static | BindingFlags.NonPublic);
        if (create == null)
            throw new InvalidOperationException("UIRedesignBuilder.CreateLevelCatalog not found");
        create.Invoke(null, null);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // ---- track layout ----------------------------------------------------------------------------------

    // Lays Level 01's track tiles along a path on the faces of an imaginary solid. A tile is a unit panel with a
    // surface normal; the lane runs Lift above its centre. Moves:
    //   F(n)       n straight tiles ahead
    //   Turn(dir)  a corner tile ahead, then continue towards dir (in the same plane)
    //   Concave(n) the plane ahead rises against the normal (floor -> wall going up, wall -> ceiling, ...)
    //   Convex(n)  go over the edge ahead (floor -> wall going down, wall -> underside, ...)
    // Corners between planes sit Lift away from both planes, like the hump and the dive in Level 01.
    private class Track
    {
        public const float Lift = 0.75f;

        public enum PieceKind { Straight, Turn, Edge }

        public struct Piece
        {
            public PieceKind Kind;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public readonly List<Piece> Pieces = new List<Piece>();
        public readonly List<Vector3> Waypoints = new List<Vector3>();
        public readonly Vector3 Start, StartNormal, StartDir;
        public Vector3 Exit, ExitNormal, ExitDir;

        private Vector3 center, normal, dir;

        public Track(Vector3 start, Vector3 startNormal, Vector3 startDir)
        {
            Start = center = start;
            StartNormal = normal = startNormal;
            StartDir = dir = startDir;
            Add(PieceKind.Straight, center, Quaternion.LookRotation(dir, normal));
            Waypoints.Add(center + Lift * normal);
        }

        public Track F(int count)
        {
            for (int i = 0; i < count; i++)
            {
                center += dir;
                Add(PieceKind.Straight, center, Quaternion.LookRotation(dir, normal));
            }
            return this;
        }

        public Track Turn(Vector3 newDir)
        {
            center += dir;
            // The corner tile joins its local -z and +x sides
            Quaternion rotation = Vector3.Dot(Vector3.Cross(normal, dir), newDir) > 0.5f
                ? Quaternion.LookRotation(dir, normal)
                : Quaternion.LookRotation(-newDir, normal);
            Add(PieceKind.Turn, center, rotation);
            Waypoints.Add(center + Lift * normal);
            dir = newDir;
            return this;
        }

        public Track Concave(int count)
        {
            Vector3 edge = center + 0.5f * dir;
            Vector3 newNormal = -dir, newDir = normal;
            Waypoints.Add(edge + Lift * (normal + newNormal));
            normal = newNormal;
            dir = newDir;
            center = edge + 0.5f * dir;
            Add(PieceKind.Straight, center, Quaternion.LookRotation(dir, normal));
            return F(count - 1);
        }

        public Track Convex(int count)
        {
            Vector3 edge = center + 0.5f * dir;
            Vector3 newNormal = dir, newDir = -normal;
            Waypoints.Add(edge + Lift * (normal + newNormal));
            AddEdge(edge, normal, newNormal);
            normal = newNormal;
            dir = newDir;
            center = edge + 0.5f * dir;
            Add(PieceKind.Straight, center, Quaternion.LookRotation(dir, normal));
            return F(count - 1);
        }

        public Track End()
        {
            Exit = center;
            ExitNormal = normal;
            ExitDir = dir;
            Waypoints.Add(center + Lift * normal);
            return this;
        }

        public float Length()
        {
            float length = 0;
            for (int i = 1; i < Waypoints.Count; i++)
                length += Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
            return length;
        }

        // The bevel piece Level 01 puts on outer edges: it lies on the more upward facing of the two planes
        private void AddEdge(Vector3 edge, Vector3 a, Vector3 b)
        {
            Vector3 up = a.y >= b.y ? a : b;
            Vector3 forward = -(a.y >= b.y ? b : a);
            Add(PieceKind.Edge, edge - 0.1f * up + 0.4f * forward, Quaternion.LookRotation(forward, up));
        }

        private void Add(PieceKind kind, Vector3 position, Quaternion rotation)
        {
            Pieces.Add(new Piece { Kind = kind, Position = Snap(position), Rotation = rotation });
        }

        private static Vector3 Snap(Vector3 v)
        {
            return new Vector3(Mathf.Round(v.x * 1000) / 1000, Mathf.Round(v.y * 1000) / 1000, Mathf.Round(v.z * 1000) / 1000);
        }
    }
}
