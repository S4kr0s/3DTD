using System;
using System.Collections.Generic;
using EPOOutline;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Builds the Bullet Dispenser tower: its body (a Synty turret drum carrying a ball of gatling barrels), the barrels
// each tier adds, the ricochet emitters and gravity rings, and its three upgrade paths. Re-running it rebuilds all
// of that and then runs ProjectileVisualsBuilder for the looks of the modules, so edit this file, not the prefab.
// Batch mode: -executeMethod BulletDispenserBuilder.Run (exits with the number of errors); menu 3DTD > Towers.
//
// The paths:
//   1 Barrel Sphere  3 -> 6 -> 12 -> 24 barrels on a ball around the core, in every direction but down to the base
//   2 Ricochet       needles rebound off the inside of the range dome (DispenserRicochetUpgrade)
//   3 Gravity Well   enemies are dragged off their path towards the ball and slowed (DispenserGravityUpgrade)
public static class BulletDispenserBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Tower/Bullet Dispenser Tower.prefab";
    private const string Folder = "Assets/Prefabs/Tower/BulletDispenser";
    private const string Synty = "Assets/PolygonSciFiSpace/Models/";
    private const string SyntyMaterialPath = "Assets/PolygonSciFiSpace/Materials/PolygonScifiSpace_Material_01_A.mat";
    private const string BallMeshPath = "Assets/PolygonPrototype/Models/SM_Primitive_SoccerBall_01.fbx";
    private const string BodyName = "Dispenser Body";

    // Layout in the body's frame: y up from the face the tower stands on, x/z across it; the tower keeps to its
    // unit cell (|x|, |z| <= 0.5, y <= 1)
    private const float PlateWidth = 0.64f;
    private const float PlateHeight = 0.1f;
    private const float ColumnWidth = 0.38f;
    private const float ColumnTop = 0.52f;
    private const float HeadHeight = 0.7f;
    private const float CoreRadius = 0.15f;
    private const float BarrelLength = 0.22f;
    private const float RingRadius = 0.42f;

    // The barrel ball: (tier that adds the barrel, elevation, yaw) in degrees. Rings at -20, 0, 30 and 60 degrees,
    // neighbouring rings turned half a step against each other; nothing points down at the base. The first tier
    // closes the ring around the ball (the face's own plane), the next ones grow it into a ball above it.
    private static readonly (int tier, float elevation, float yaw)[] Barrels =
    {
        (0, 0f, 0f), (0, 0f, 120f), (0, 0f, 240f),
        (1, 0f, 60f), (1, 0f, 180f), (1, 0f, 300f),
        (2, 30f, 30f), (2, 30f, 150f), (2, 30f, 270f),
        (2, -20f, 90f), (2, -20f, 210f), (2, -20f, 330f),
        (3, 30f, 90f), (3, 30f, 210f), (3, 30f, 330f),
        (3, -20f, 30f), (3, -20f, 150f), (3, -20f, 270f),
        (3, 60f, 0f), (3, 60f, 60f), (3, 60f, 120f), (3, 60f, 180f), (3, 60f, 240f), (3, 60f, 300f),
    };

    private static int errors;
    private static Material syntyMaterial;
    private static Material neonMaterial;
    private static Material softNeonMaterial;
    private static Material coreMaterial;
    private static Material darkMaterial;
    private static Mesh torusMesh;

    [MenuItem("3DTD/Towers/Rebuild Bullet Dispenser")]
    public static void BuildAll()
    {
        errors = 0;
        Build();
        ProjectileVisualsBuilder.BuildAll();
        errors += ProjectileVisualsBuilder.Errors;
        AssetDatabase.SaveAssets();
        Debug.Log("BulletDispenserBuilder: done, " + errors + " error(s)");
    }

    public static void Run()
    {
        int code;
        try
        {
            BuildAll();
            code = errors;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            code = 1;
        }
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    private static void Fail(string message)
    {
        errors++;
        Debug.LogError("BulletDispenserBuilder: " + message);
    }

    // ---- materials ---------------------------------------------------------------------------------------

    private static void Materials()
    {
        syntyMaterial = AssetDatabase.LoadAssetAtPath<Material>(SyntyMaterialPath);
        if (syntyMaterial == null)
            Fail("missing " + SyntyMaterialPath);
        // Neon green like the needles' outline, dark green like their cores
        neonMaterial = LitMaterial("DispenserNeon", new Color(0.1f, 0.65f, 0.15f), 0.2f, 0.6f, new Color(0.2f, 1f, 0.25f) * 1.2f);
        softNeonMaterial = LitMaterial("DispenserNeonSoft", new Color(0.08f, 0.45f, 0.12f), 0.2f, 0.6f, new Color(0.15f, 0.8f, 0.2f) * 0.7f);
        coreMaterial = LitMaterial("DispenserCore", new Color(0.05f, 0.12f, 0.07f), 0.8f, 0.6f, new Color(0.02f, 0.14f, 0.04f));
        darkMaterial = LitMaterial("DispenserGunmetal", new Color(0.16f, 0.18f, 0.2f), 0.8f, 0.45f, Color.black);
        torusMesh = Torus("DispenserRing", 0.06f, 48, 8);
    }

    // A slim ring of radius 1 in the x/y plane (axis z), tube radius thickness: the neon rings of the tower
    private static Mesh Torus(string name, float thickness, int segments, int sides)
    {
        string path = Folder + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            EnsureFolder(Folder);
            mesh = new Mesh();
            AssetDatabase.CreateAsset(mesh, path);
        }
        mesh.Clear();
        mesh.name = name;
        Vector3[] vertices = new Vector3[(segments + 1) * (sides + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * sides * 6];
        for (int i = 0; i <= segments; i++)
        {
            float around = i * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(around), Mathf.Sin(around), 0f);
            for (int j = 0; j <= sides; j++)
            {
                float tube = j * Mathf.PI * 2f / sides;
                Vector3 normal = radial * Mathf.Cos(tube) + Vector3.forward * Mathf.Sin(tube);
                int index = i * (sides + 1) + j;
                vertices[index] = radial + normal * thickness;
                normals[index] = normal;
                uvs[index] = new Vector2((float)i / segments, (float)j / sides);
            }
        }
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j;
                int b = a + sides + 1;
                triangles[t++] = a;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = b + 1;
            }
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // A neon ring of the given radius, its axis along the rotation's z
    private static GameObject Ring(string name, Transform parent, float radius, Quaternion rotation, Material material, float thickness = 1f)
    {
        GameObject ring = new GameObject(name);
        ring.transform.SetParent(parent, false);
        ring.AddComponent<MeshFilter>().sharedMesh = torusMesh;
        MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.transform.localRotation = rotation;
        ring.transform.localScale = new Vector3(radius, radius, radius * thickness);
        return ring;
    }

    private static Material LitMaterial(string name, Color color, float metallic, float smoothness, Color emission)
    {
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Fail("URP Lit shader not found");
                return null;
            }
            EnsureFolder(Folder);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        bool glows = emission.maxColorComponent > 0f;
        material.SetColor("_EmissionColor", emission);
        if (glows)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        // The barrels and rings are many small copies of the same meshes
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- prefab ------------------------------------------------------------------------------------------

    private static void Build()
    {
        Materials();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Tower tower = root.GetComponent<Tower>();
            UpgradeManager manager = root.GetComponent<UpgradeManager>();
            if (tower == null || manager == null)
            {
                Fail("the prefab has no Tower or UpgradeManager");
                return;
            }
            SerializedObject towerData = new SerializedObject(tower);
            BulletDispenserTowerActionStrategy strategy = towerData.FindProperty("actionStrategy").objectReferenceValue as BulletDispenserTowerActionStrategy;
            if (strategy == null)
            {
                Fail("the tower's strategy is not a BulletDispenserTowerActionStrategy");
                return;
            }

            Outlinable oldOutline = root.GetComponentInChildren<Outlinable>(true);
            string outlineSettings = oldOutline != null ? EditorJsonUtility.ToJson(oldOutline) : null;
            RemoveOldParts(root, strategy, tower);

            Parts parts = BuildBody(root, outlineSettings);
            WireTower(towerData, parts);
            WireStrategy(strategy, parts);
            BuildUpgradePaths(root, manager, parts);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
                Fail("could not save " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private sealed class Parts
    {
        public Transform Head;
        public Transform Core;
        public readonly List<GameObject>[] BarrelGroups = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        public readonly List<ShootingPointReference> ShootingPoints = new List<ShootingPointReference>();
        public readonly List<GameObject>[] TierPoints = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        public readonly GameObject[] Rings = new GameObject[3];
        public readonly GameObject[] Ricochet = new GameObject[3];
    }

    // Everything but the targetter goes: the old turret, the flamethrower flames, the strategies and upgrades the
    // old paths swapped in, and a body of an earlier run
    private static void RemoveOldParts(GameObject root, BulletDispenserTowerActionStrategy keep, Tower tower)
    {
        Transform targetter = tower.Targetter != null ? tower.Targetter.transform : null;
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = root.transform.GetChild(i);
            if (targetter != null && (child == targetter || targetter.IsChildOf(child)))
                continue;
            Object.DestroyImmediate(child.gameObject);
        }

        foreach (BulletDispenserTowerActionStrategy other in root.GetComponents<BulletDispenserTowerActionStrategy>())
        {
            if (other != keep)
                Object.DestroyImmediate(other);
        }
        foreach (Upgrade upgrade in root.GetComponents<Upgrade>())
        {
            if (!(upgrade is VisualUpgrade))
                Object.DestroyImmediate(upgrade);
        }
    }

    private static Parts BuildBody(GameObject root, string outlineSettings)
    {
        Parts parts = new Parts();

        // The tower's forward axis is the face normal and its origin the centre of its unit cell; the body works
        // with y up from the face
        GameObject body = new GameObject(BodyName);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // A wide octagonal plate like the other towers', a chunky pillar and a gunmetal seat for the barrel ball
        GameObject plate = Part("Base Plate", body.transform, Synty + "SM_Prop_Turret_Small_Floor_01.fbx", "SM_Prop_Turret_Small_Floor_01", syntyMaterial);
        FitBox(plate, new Vector3(PlateWidth, PlateHeight, PlateWidth), 0f);
        GameObject column = Part("Column", body.transform, Synty + "SM_Prop_Detail_Pipe_Pillar_01.fbx", null, syntyMaterial);
        FitBox(column, new Vector3(ColumnWidth, ColumnTop - PlateHeight, ColumnWidth), PlateHeight);
        GameObject seat = Part("Seat", body.transform, Synty + "SM_Prop_Turret_Small_Floor_01.fbx", "SM_Prop_Turret_Small_Floor_01", darkMaterial);
        FitBox(seat, new Vector3(ColumnWidth + 0.04f, 0.06f, ColumnWidth + 0.04f), ColumnTop - 0.01f);

        // Head: turned by the aim slider (Tower.RotateTower), carries the ball and its barrels
        GameObject head = new GameObject("Head");
        head.transform.SetParent(body.transform, false);
        head.transform.localPosition = new Vector3(0f, HeadHeight, 0f);
        parts.Head = head.transform;

        GameObject core = Part("Core", head.transform, BallMeshPath, null, coreMaterial);
        FitSphere(core, CoreRadius);
        parts.Core = core.transform;

        string[] groupNames = { "Barrels (base)", "Barrels (Barrel Ring)", "Barrels (Barrel Crown)", "Barrels (Needle Sphere)" };
        GameObject[] groups = new GameObject[4];
        for (int tier = 0; tier < 4; tier++)
        {
            groups[tier] = new GameObject(groupNames[tier]);
            groups[tier].transform.SetParent(head.transform, false);
            groups[tier].SetActive(tier == 0);
            parts.BarrelGroups[tier].Add(groups[tier]);
        }
        for (int i = 0; i < Barrels.Length; i++)
        {
            (int tier, float elevation, float yaw) = Barrels[i];
            ShootingPointReference point = Barrel(groups[tier].transform, i, elevation, yaw);
            point.gameObject.SetActive(tier == 0);
            parts.ShootingPoints.Add(point);
            parts.TierPoints[tier].Add(point.gameObject);
        }

        // Gravity rings: gyroscope rings around the ball, one per tier of the gravity path, spun by the strategy
        // (flat, then tilted 30 degrees two ways, so they stay inside the unit cell)
        Quaternion[] ringTilts = { Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(60f, 0f, 0f), Quaternion.Euler(60f, 90f, 0f) };
        for (int i = 0; i < 3; i++)
        {
            GameObject ring = Ring("Gravity Ring " + (i + 1), head.transform, RingRadius - i * 0.03f, ringTilts[i], softNeonMaterial, 0.6f);
            ring.SetActive(false);
            parts.Rings[i] = ring;
        }

        // Ricochet emitters: a neon deflector band round the pillar, a second one round the plate, then a tracking dish
        GameObject band1 = Ring("Ricochet Band", body.transform, ColumnWidth * 0.5f + 0.02f, Quaternion.Euler(90f, 0f, 0f), neonMaterial);
        band1.transform.localPosition = new Vector3(0f, PlateHeight + (ColumnTop - PlateHeight) * 0.55f, 0f);
        band1.SetActive(false);
        parts.Ricochet[0] = band1;

        GameObject band2 = new GameObject("Ricochet Projector");
        band2.transform.SetParent(body.transform, false);
        for (int i = 0; i < 2; i++)
        {
            GameObject line = Ring("Projector Ring " + (i + 1), band2.transform, ColumnWidth * 0.5f + 0.02f, Quaternion.Euler(90f, 0f, 0f), neonMaterial);
            line.transform.localPosition = new Vector3(0f, PlateHeight + (ColumnTop - PlateHeight) * (0.25f + 0.55f * i) + 0.04f * (1 - 2 * i), 0f);
        }
        band2.SetActive(false);
        parts.Ricochet[1] = band2;

        GameObject dish = Part("Ricochet Dish", body.transform, Synty + "SM_Prop_Satellite_Stand_01.fbx", null, syntyMaterial);
        FitBox(dish, new Vector3(0.13f, 0.15f, 0.13f), PlateHeight - 0.01f);
        dish.transform.localPosition += new Vector3(PlateWidth * 0.3f, 0f, -PlateWidth * 0.18f);
        dish.transform.localRotation = Quaternion.Euler(0f, 120f, 0f);
        dish.SetActive(false);
        parts.Ricochet[2] = dish;

        // Hover outline over the whole body, with the old turret's settings
        Outlinable outline = body.AddComponent<Outlinable>();
        if (outlineSettings != null)
            EditorJsonUtility.FromJsonOverwrite(outlineSettings, outline);
        outline.AddAllChildRenderersToRenderingList(RenderersAddingMode.MeshRenderer);
        body.AddComponent<OutlineSwitch>();
        return parts;
    }

    // One barrel on the ball: a gatling barrel pointing out of the core, the shooting point at its muzzle
    private static ShootingPointReference Barrel(Transform group, int index, float elevation, float yaw)
    {
        Quaternion direction = Quaternion.Euler(-elevation, yaw, 0f);
        GameObject mount = new GameObject("Barrel " + (index + 1));
        mount.transform.SetParent(group, false);
        mount.transform.localRotation = direction;
        mount.transform.localPosition = direction * Vector3.forward * (CoreRadius * 0.7f);

        GameObject barrel = Part("Barrel", mount.transform, Synty + "SM_Prop_Turret_Base_Single_03.fbx", "SM_Prop_Turret_Gattling_Barrel_03", syntyMaterial);
        Mesh mesh = barrel.GetComponent<MeshFilter>().sharedMesh;
        Bounds bounds = mesh.bounds;
        float scale = BarrelLength / bounds.size.z;
        barrel.transform.localScale = Vector3.one * scale;
        barrel.transform.localPosition = new Vector3(-bounds.center.x, -bounds.center.y, -bounds.min.z) * scale;

        // Neon muzzle ring, so the barrel ball reads at a distance
        GameObject tip = Ring("Muzzle Ring", mount.transform, Mathf.Max(bounds.size.x, bounds.size.y) * scale * 0.5f, Quaternion.identity, neonMaterial, 1.5f);
        tip.transform.localPosition = new Vector3(0f, 0f, BarrelLength * 0.9f);

        GameObject point = new GameObject("ShootingPoint");
        point.transform.SetParent(mount.transform, false);
        point.transform.localPosition = new Vector3(0f, 0f, BarrelLength + 0.01f);
        ShootingPointReference reference = point.AddComponent<ShootingPointReference>();
        SerializedObject data = new SerializedObject(reference);
        data.FindProperty("shootingPointReference").objectReferenceValue = point;
        data.ApplyModifiedPropertiesWithoutUndo();
        return reference;
    }

    // A mesh part: the named child mesh of a model (or its first mesh), on its own GameObject
    private static GameObject Part(string name, Transform parent, string modelPath, string meshName, Material material)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        Mesh mesh = FindMesh(modelPath, meshName);
        if (mesh == null)
        {
            Fail("missing mesh " + modelPath + (meshName != null ? "#" + meshName : ""));
            return part;
        }
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return part;
    }

    private static Mesh FindMesh(string modelPath, string meshName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (asset is Mesh mesh && (meshName == null || mesh.name == meshName))
                return mesh;
        }
        return null;
    }

    // Scales a part to a box (x, y, z sizes) and stands it on the given height, centred on the axis
    private static void FitBox(GameObject part, Vector3 size, float bottom)
    {
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return;
        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        part.transform.localScale = scale;
        part.transform.localPosition = new Vector3(-bounds.center.x * scale.x, bottom - bounds.min.y * scale.y, -bounds.center.z * scale.z);
    }

    private static void FitSphere(GameObject part, float radius)
    {
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return;
        Bounds bounds = filter.sharedMesh.bounds;
        float scale = 2f * radius / bounds.size.x;
        part.transform.localScale = Vector3.one * scale;
        part.transform.localPosition = -bounds.center * scale;
    }

    // ---- wiring ------------------------------------------------------------------------------------------

    private static void WireTower(SerializedObject tower, Parts parts)
    {
        tower.FindProperty("description").stringValue = "Cheap ball of barrels that sprays needles in fixed directions.";
        tower.FindProperty("rotationPoint").objectReferenceValue = parts.Head.gameObject;
        tower.FindProperty("rotationBase").objectReferenceValue = parts.Head.gameObject;
        SerializedProperty points = tower.FindProperty("shootingPoints");
        points.arraySize = parts.ShootingPoints.Count;
        for (int i = 0; i < parts.ShootingPoints.Count; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = parts.ShootingPoints[i];
        tower.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireStrategy(BulletDispenserTowerActionStrategy strategy, Parts parts)
    {
        SerializedObject data = new SerializedObject(strategy);
        // The head's pivot is the centre of the ball (the ball mesh itself is offset to centre it there)
        data.FindProperty("core").objectReferenceValue = parts.Head;
        SerializedProperty rings = data.FindProperty("gravityRings");
        rings.arraySize = parts.Rings.Length;
        for (int i = 0; i < parts.Rings.Length; i++)
            rings.GetArrayElementAtIndex(i).objectReferenceValue = parts.Rings[i].transform;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- upgrade paths -----------------------------------------------------------------------------------

    private struct StatChange
    {
        public Stat.StatType Stat;
        public float Value;
        public bool Modifier;

        public StatChange(Stat.StatType stat, float value, bool modifier = false)
        {
            Stat = stat;
            Value = value;
            Modifier = modifier;
        }
    }

    private sealed class Module
    {
        public string Name;
        public string Description;
        public int Price;
        public Upgrade[] Upgrades = new Upgrade[0];
        public StatChange[] Stats = new StatChange[0];
        public GameObject[] Show = new GameObject[0];
    }

    private static void BuildUpgradePaths(GameObject root, UpgradeManager manager, Parts parts)
    {
        Module[][] paths =
        {
            new[]
            {
                new Module
                {
                    Name = "Barrel Ring", Price = 130,
                    Description = "Three more barrels close the ring around the ball.\n+3 Barrels",
                    Upgrades = new Upgrade[] { Firepoints(root, parts, 1) },
                },
                new Module
                {
                    Name = "Barrel Crown", Price = 200,
                    Description = "Six more barrels: three tilted up, three tilted down.\n+6 Barrels",
                    Upgrades = new Upgrade[] { Firepoints(root, parts, 2) },
                },
                new Module
                {
                    Name = "Needle Sphere", Price = 790,
                    Description = "Barrels all over the ball: needles fly everywhere but down.\n+12 Barrels\n+0.5 Range\n+1 Pierce",
                    Upgrades = new Upgrade[] { Firepoints(root, parts, 3) },
                    Stats = new[] { new StatChange(Stat.StatType.RANGE, 0.5f), new StatChange(Stat.StatType.PIERCING, 1f) },
                },
            },
            new[]
            {
                new Module
                {
                    Name = "Rebound Rounds", Price = 120,
                    Description = "Needles ricochet off an invisible dome around the tower and off the ground.\n2 Ricochets\n+0.6 s Needle Lifetime",
                    Upgrades = new Upgrade[] { Ricochet(root, 2, 0f, false) },
                    Stats = new[] { new StatChange(Stat.StatType.LIFETIME, 0.6f) },
                    Show = new[] { parts.Ricochet[0] },
                },
                new Module
                {
                    Name = "Kinetic Rebound", Price = 320,
                    Description = "Up to four ricochets, and every bounce makes a needle hit harder.\n4 Ricochets\n+1 Damage per Bounce\n+0.4 s Needle Lifetime",
                    Upgrades = new Upgrade[] { Ricochet(root, 4, 1f, false) },
                    Stats = new[] { new StatChange(Stat.StatType.LIFETIME, 0.4f) },
                    Show = new[] { parts.Ricochet[1] },
                },
                new Module
                {
                    Name = "Trick Shot Matrix", Price = 1100,
                    Description = "Six ricochets, and every bounce aims the needle at an enemy.\n6 Seeking Ricochets\n+0.8 s Needle Lifetime",
                    Upgrades = new Upgrade[] { Ricochet(root, 6, 1f, true) },
                    Stats = new[] { new StatChange(Stat.StatType.LIFETIME, 0.8f) },
                    Show = new[] { parts.Ricochet[2] },
                },
            },
            new[]
            {
                new Module
                {
                    Name = "Graviton Core", Price = 120,
                    Description = "Drags enemies in range off their path towards the tower. Bosses are too heavy.\nPull 0.5\n15% Slow",
                    Upgrades = new Upgrade[] { Gravity(root, 0.5f, 1.5f, 0.15f, 0f, 0f) },
                    Show = new[] { parts.Rings[0] },
                },
                new Module
                {
                    Name = "Event Horizon", Price = 300,
                    Description = "A deeper well that holds enemies right in front of the barrels.\nPull 1.2\n30% Slow\n+0.5 Range",
                    Upgrades = new Upgrade[] { Gravity(root, 1.2f, 2.5f, 0.3f, 0f, 0f) },
                    Stats = new[] { new StatChange(Stat.StatType.RANGE, 0.5f) },
                    Show = new[] { parts.Rings[1] },
                },
                new Module
                {
                    Name = "Singularity", Price = 1000,
                    Description = "Every 5 s the well collapses and holds every enemy in range around the barrel ball for 1.5 s.\nSingularity",
                    Upgrades = new Upgrade[] { Gravity(root, 1.2f, 2.5f, 0.3f, 5f, 1.5f) },
                    Show = new[] { parts.Rings[2] },
                },
            },
        };

        SerializedObject data = new SerializedObject(manager);
        SerializedProperty pathList = data.FindProperty("upgradePaths");
        pathList.arraySize = paths.Length;
        for (int p = 0; p < paths.Length; p++)
        {
            SerializedProperty path = pathList.GetArrayElementAtIndex(p);
            path.FindPropertyRelative("IsBlocked").boolValue = false;
            SerializedProperty modules = path.FindPropertyRelative("upgradeModules");
            modules.arraySize = paths[p].Length;
            for (int t = 0; t < paths[p].Length; t++)
                WriteModule(root, modules.GetArrayElementAtIndex(t), paths[p][t], p + 1, t + 1);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteModule(GameObject root, SerializedProperty module, Module source, int path, int tier)
    {
        module.FindPropertyRelative("name").stringValue = source.Name;
        module.FindPropertyRelative("description").stringValue = source.Description;
        module.FindPropertyRelative("price").intValue = source.Price;
        module.FindPropertyRelative("isActive").boolValue = false;
        module.FindPropertyRelative("isAvailable").boolValue = false;

        // The module's look (ProjectileVisualsBuilder) stays first
        List<Upgrade> upgrades = new List<Upgrade>();
        string visualKey = "fx p" + path + "t" + tier;
        foreach (VisualUpgrade visual in root.GetComponents<VisualUpgrade>())
        {
            if (new SerializedObject(visual).FindProperty("Comment").stringValue == visualKey)
                upgrades.Add(visual);
        }
        upgrades.AddRange(source.Upgrades);
        SetObjects(module.FindPropertyRelative("upgrades"), upgrades);

        SerializedProperty stats = module.FindPropertyRelative("statUpgrades");
        stats.arraySize = source.Stats.Length;
        for (int i = 0; i < source.Stats.Length; i++)
        {
            SerializedProperty stat = stats.GetArrayElementAtIndex(i);
            stat.FindPropertyRelative("targetStat").intValue = (int)source.Stats[i].Stat;
            stat.FindPropertyRelative("upgradeValue").floatValue = source.Stats[i].Value;
            stat.FindPropertyRelative("isModifier").boolValue = source.Stats[i].Modifier;
        }
        SetObjects(module.FindPropertyRelative("upgradeVisualGameObjects"), source.Show);
        SetObjects(module.FindPropertyRelative("disableVisualGameObjects"), new GameObject[0]);
    }

    private static void SetObjects<T>(SerializedProperty list, IList<T> objects) where T : Object
    {
        list.arraySize = objects.Count;
        for (int i = 0; i < objects.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }

    // Turns on a tier's barrels: their group and every shooting point in it
    private static Upgrade Firepoints(GameObject root, Parts parts, int tier)
    {
        ChangeFirepointsUpgrade upgrade = root.AddComponent<ChangeFirepointsUpgrade>();
        List<GameObject> activate = new List<GameObject>(parts.BarrelGroups[tier]);
        activate.AddRange(parts.TierPoints[tier]);
        SerializedObject data = new SerializedObject(upgrade);
        data.FindProperty("Comment").stringValue = "barrels t" + tier;
        SetObjects(data.FindProperty("ToDeactivate"), new GameObject[0]);
        SetObjects(data.FindProperty("ToActivate"), activate);
        data.ApplyModifiedPropertiesWithoutUndo();
        return upgrade;
    }

    private static Upgrade Ricochet(GameObject root, int bounces, float damagePerBounce, bool seek)
    {
        DispenserRicochetUpgrade upgrade = root.AddComponent<DispenserRicochetUpgrade>();
        SerializedObject data = new SerializedObject(upgrade);
        data.FindProperty("Comment").stringValue = "ricochet " + bounces;
        data.FindProperty("bounces").intValue = bounces;
        data.FindProperty("damagePerBounce").floatValue = damagePerBounce;
        data.FindProperty("seek").boolValue = seek;
        data.ApplyModifiedPropertiesWithoutUndo();
        return upgrade;
    }

    private static Upgrade Gravity(GameObject root, float pull, float pullSpeed, float slow, float interval, float duration)
    {
        DispenserGravityUpgrade upgrade = root.AddComponent<DispenserGravityUpgrade>();
        SerializedObject data = new SerializedObject(upgrade);
        data.FindProperty("Comment").stringValue = "gravity " + pull;
        data.FindProperty("pull").floatValue = pull;
        data.FindProperty("pullSpeed").floatValue = pullSpeed;
        data.FindProperty("slow").floatValue = slow;
        data.FindProperty("singularityInterval").floatValue = interval;
        data.FindProperty("singularityDuration").floatValue = duration;
        data.ApplyModifiedPropertiesWithoutUndo();
        return upgrade;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
