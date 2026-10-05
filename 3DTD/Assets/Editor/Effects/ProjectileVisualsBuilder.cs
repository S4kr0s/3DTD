using System;
using System.Collections.Generic;
using System.IO;
using PolygonArsenal;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Generates the towers' projectile and effect looks and wires them into the tower prefabs:
//   - effect copies of Polygon Arsenal prefabs (recoloured, rescaled, stretched, smoke turned into ember
//     wisps, effects merged) in Assets/Prefabs/Effects/<Tower>/
//   - projectile prefabs (copies of the Prefabs/ProjectileParent* wrappers with other effects; colliders and
//     settings untouched, so the hit shapes stay the same) in Assets/Prefabs/Projectiles/<Tower>/
//   - each tower's base look on its strategies and one VisualUpgrade (BeamVisualUpgrade, SniperVisualUpgrade)
//     per upgrade module, first in the module's upgrades
// Everything is rebuilt from the Polygon Arsenal sources on every run (same paths, same GUIDs), so edit the
// recipes in ProjectileVisualsBuilder.Recipes.cs, not the generated prefabs. A recipe may start from an effect
// made earlier in the run (a full "Assets/..." source path).
//
// Batch mode: Unity -batchmode -nographics -projectPath ... -executeMethod ProjectileVisualsBuilder.Run
public static partial class ProjectileVisualsBuilder
{
    private const string Combat = "Assets/Polygon Arsenal/Prefabs/Combat/";
    private const string EffectFolder = "Assets/Prefabs/Effects";
    private const string ProjectileFolder = "Assets/Prefabs/Projectiles";
    private const string TowerFolder = "Assets/Prefabs/Tower/";
    private const string GlowMaterialPath = "Assets/Polygon Arsenal/Materials/Main/PolySolidGlow.mat";
    private const string LitSmokeMaterialPath = "Assets/Polygon Arsenal/Materials/Main/PolyLitSurface.mat";
    private const string VisualKeyPrefix = "fx ";

    private static int errors;

    [MenuItem("3DTD/Effects/Rebuild Projectile Visuals")]
    public static void BuildAll()
    {
        errors = 0;
        BuildEffects();
        WireTowers();
        AssetDatabase.SaveAssets();
        Debug.Log("ProjectileVisualsBuilder: done, " + errors + " error(s)");
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
        Debug.LogError("ProjectileVisualsBuilder: " + message);
    }

    // ---- effect copies ---------------------------------------------------------------------------------

    private delegate void Edit(GameObject root);

    // Copies source (a path below Polygon Arsenal's Combat folder, or a full "Assets/..." path, e.g. an effect
    // made earlier) to Assets/Prefabs/Effects/<name>.prefab with the edits applied
    private static GameObject Fx(string name, string source, params Edit[] edits)
    {
        string sourcePath = source.StartsWith("Assets/") ? source : Combat + source + ".prefab";
        return Save(sourcePath, EffectFolder + "/" + name + ".prefab", edits);
    }

    private static GameObject Save(string sourcePath, string destination, Edit[] edits)
    {
        if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
        {
            Fail("missing source " + sourcePath);
            return null;
        }
        EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(destination);
            foreach (Edit edit in edits)
                edit?.Invoke(root);
            // No opaque smoke anywhere: what a recipe didn't turn into coloured wisps becomes grey ones
            NeutralSmoke(root);
            // No world collision either: it costs a physics query per particle, and the dispenser and minigun
            // impacts spawn sparks by the thousand (sparks bouncing off blocks aren't worth that)
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, destination, out bool saved);
            if (!saved)
                Fail("could not save " + destination);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(destination);
    }

    // ---- projectile copies -----------------------------------------------------------------------------

    // A copy of a projectile wrapper (Prefabs/ProjectileParent*.prefab) with other effects; null keeps one
    private static GameObject Projectile(string name, string wrapper, GameObject muzzle, GameObject flight, GameObject impact, GameObject clusterBomblet = null)
    {
        Edit wire = root =>
        {
            PolygonProjectileScript visuals = root.GetComponent<PolygonProjectileScript>();
            if (visuals == null)
            {
                Fail(name + ": wrapper has no PolygonProjectileScript");
                return;
            }
            if (muzzle != null)
                visuals.muzzleParticle = muzzle;
            if (flight != null)
                visuals.projectileParticle = flight;
            if (impact != null)
                visuals.impactParticle = impact;
            if (clusterBomblet != null)
            {
                if (root.TryGetComponent(out ProjectileBomb bomb))
                    SetField(bomb, "clusterProjectilePrefab", clusterBomblet);
                else
                    Fail(name + ": cluster bomblet on a projectile without ProjectileBomb");
            }
        };
        return Save("Assets/Prefabs/" + wrapper + ".prefab", ProjectileFolder + "/" + name + ".prefab", new[] { wire });
    }

    // ---- edits -----------------------------------------------------------------------------------------

    private static IEnumerable<ParticleSystem> Systems(GameObject root, string only)
    {
        foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (only == null || system.gameObject.name == only || (only == "<root>" && system.gameObject == root))
                yield return system;
        }
    }

    // Every colour of the effect (start colours, colour over lifetime/speed, particle trails, trail and line
    // renderers, lights) to a hue. Coloured keys keep their saturation times satMul; near-white keys get
    // whiteSat (0 keeps cores white); brightness times value
    private static Edit Tint(float hueDegrees, float satMul = 1f, float value = 1f, float whiteSat = 0f, string only = null)
    {
        return root => Recolor(root, only, c => Shift(c, hueDegrees / 360f, satMul, value, whiteSat));
    }

    private static Color Shift(Color c, float hue, float satMul, float value, float whiteSat)
    {
        float alpha = c.a;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (s < 0.15f)
            s = Mathf.Max(s, whiteSat);
        else
            s = Mathf.Clamp01(s * satMul);
        Color result = Color.HSVToRGB(hue, s, v * value, true);
        result.a = alpha;
        return result;
    }

    private static void Recolor(GameObject root, string only, Func<Color, Color> map)
    {
        foreach (ParticleSystem system in Systems(root, only))
        {
            ParticleSystem.MainModule main = system.main;
            main.startColor = Map(main.startColor, map);
            ParticleSystem.ColorOverLifetimeModule overLifetime = system.colorOverLifetime;
            if (overLifetime.enabled)
                overLifetime.color = Map(overLifetime.color, map);
            ParticleSystem.ColorBySpeedModule bySpeed = system.colorBySpeed;
            if (bySpeed.enabled)
                bySpeed.color = Map(bySpeed.color, map);
            ParticleSystem.TrailModule trails = system.trails;
            if (trails.enabled)
            {
                trails.colorOverLifetime = Map(trails.colorOverLifetime, map);
                trails.colorOverTrail = Map(trails.colorOverTrail, map);
            }
        }
        if (only != null)
            return;
        foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
            trail.colorGradient = Map(trail.colorGradient, map);
        foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
            line.colorGradient = Map(line.colorGradient, map);
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            light.color = map(light.color);
    }

    private static ParticleSystem.MinMaxGradient Map(ParticleSystem.MinMaxGradient gradient, Func<Color, Color> map)
    {
        switch (gradient.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(map(gradient.color));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(map(gradient.colorMin), map(gradient.colorMax));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(Map(gradient.gradient, map));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(Map(gradient.gradientMin, map), Map(gradient.gradientMax, map));
            case ParticleSystemGradientMode.RandomColor:
                ParticleSystem.MinMaxGradient random = new ParticleSystem.MinMaxGradient(Map(gradient.gradient, map));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
        }
        return gradient;
    }

    private static Gradient Map(Gradient gradient, Func<Color, Color> map)
    {
        if (gradient == null)
            return null;
        GradientColorKey[] keys = gradient.colorKeys;
        for (int i = 0; i < keys.Length; i++)
            keys[i].color = map(keys[i].color);
        Gradient result = new Gradient { mode = gradient.mode };
        result.SetKeys(keys, gradient.alphaKeys);
        return result;
    }

    // Size of the whole effect (EffectPool plays prefabs relative to their root scale)
    private static Edit Scale(float factor)
    {
        return root => root.transform.localScale *= factor;
    }

    private static Edit Size(string only, float factor)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
            {
                ParticleSystem.MainModule main = system.main;
                if (main.startSize3D)
                {
                    main.startSizeXMultiplier *= factor;
                    main.startSizeYMultiplier *= factor;
                    main.startSizeZMultiplier *= factor;
                }
                else
                {
                    main.startSize = Mul(main.startSize, factor);
                }
            }
        };
    }

    private static Edit Lifetime(string only, float factor)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
            {
                ParticleSystem.MainModule main = system.main;
                main.startLifetime = Mul(main.startLifetime, factor);
            }
        };
    }

    private static Edit Speed(string only, float factor)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
            {
                ParticleSystem.MainModule main = system.main;
                main.startSpeed = Mul(main.startSpeed, factor);
            }
        };
    }

    // Particle counts: bursts (at least one) and emission rates
    private static Edit Count(string only, float factor)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
                ScaleEmission(system, factor);
        };
    }

    private static void ScaleEmission(ParticleSystem system, float factor)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = Mul(emission.rateOverTime, factor);
        emission.rateOverDistance = Mul(emission.rateOverDistance, factor);
        ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[emission.burstCount];
        emission.GetBursts(bursts);
        for (int i = 0; i < bursts.Length; i++)
        {
            ParticleSystem.MinMaxCurve count = bursts[i].count;
            if (count.mode == ParticleSystemCurveMode.Constant)
                count.constant = count.constant > 0f ? Mathf.Max(1f, Mathf.Round(count.constant * factor)) : 0f;
            else if (count.mode == ParticleSystemCurveMode.TwoConstants)
            {
                count.constantMin = Mathf.Max(count.constantMin > 0f ? 1f : 0f, Mathf.Round(count.constantMin * factor));
                count.constantMax = Mathf.Max(1f, Mathf.Round(count.constantMax * factor));
            }
            else
                count.curveMultiplier *= factor;
            bursts[i].count = count;
        }
        emission.SetBursts(bursts);
    }

    // Long, thin particles: the start size along the particle's own Z (mesh bolts and needles) times length,
    // across times thickness; stretched billboards get a longer tail
    private static Edit Stretch(string only, float thickness, float length)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
            {
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    renderer.lengthScale *= length;
                    continue;
                }
                ParticleSystem.MainModule main = system.main;
                if (!main.startSize3D)
                {
                    ParticleSystem.MinMaxCurve size = main.startSize;
                    main.startSize3D = true;
                    main.startSizeX = size;
                    main.startSizeY = size;
                    main.startSizeZ = size;
                }
                main.startSizeXMultiplier *= thickness;
                main.startSizeYMultiplier *= thickness;
                main.startSizeZMultiplier *= length;
            }
        };
    }

    private static Edit TrailTime(float factor, float widthFactor = 1f)
    {
        return root =>
        {
            foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.time *= factor;
                trail.widthMultiplier *= widthFactor;
            }
        };
    }

    private static Edit Remove(string name)
    {
        return root =>
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root.transform && child.name == name)
                {
                    Object.DestroyImmediate(child.gameObject);
                    return;
                }
            }
            Fail(root.name + ": no child " + name + " to remove");
        };
    }

    // The opaque lit smoke puffs (PolyLitSurface) become a few small glowing wisps that fade out: ember
    // colour at birth, darker and transparent at the end. The explosions keep their flash, sparks and fire.
    private static Edit Smoke(Color ember, float countFactor = 0.3f, float sizeFactor = 0.5f, float lifetimeFactor = 0.55f, float alpha = 0.8f)
    {
        return root =>
        {
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitSmokeMaterialPath);
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.sharedMaterial != lit)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = glow;
                renderer.sharedMaterials = materials;

                ParticleSystem.MainModule main = system.main;
                main.startSize = Mul(main.startSize, sizeFactor);
                main.startLifetime = Mul(main.startLifetime, lifetimeFactor);
                main.startColor = Color.white;
                ScaleEmission(system, countFactor);

                Gradient fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(ember, 0f), new GradientColorKey(ember * 0.55f, 1f) },
                    new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.6f, 0.35f), new GradientAlphaKey(0f, 1f) });
                ParticleSystem.ColorOverLifetimeModule overLifetime = system.colorOverLifetime;
                overLifetime.enabled = true;
                overLifetime.color = new ParticleSystem.MinMaxGradient(fade);
            }
        };
    }

    private static readonly Edit NeutralSmoke = Smoke(new Color(0.36f, 0.36f, 0.4f), 0.3f);

    // Another effect inside this one (its sounds dropped), e.g. a shockwave ring under an explosion
    private static Edit Merge(string source, float scale = 1f, Vector3 offset = default, Vector3 euler = default, params Edit[] edits)
    {
        return root =>
        {
            string path = source.StartsWith("Assets/") ? source : Combat + source + ".prefab";
            GameObject other = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Edit edit in edits)
                    edit?.Invoke(other);
                GameObject copy = Object.Instantiate(other, root.transform);
                copy.name = Path.GetFileNameWithoutExtension(path);
                copy.transform.localPosition = offset;
                copy.transform.localRotation = Quaternion.Euler(euler);
                copy.transform.localScale = other.transform.localScale * scale;
                foreach (PolygonSoundSpawn sound in copy.GetComponentsInChildren<PolygonSoundSpawn>(true))
                    Object.DestroyImmediate(sound);
                foreach (AudioSource audio in copy.GetComponentsInChildren<AudioSource>(true))
                    Object.DestroyImmediate(audio);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(other);
            }
        };
    }

    private static Edit Delay(string only, float seconds)
    {
        return root =>
        {
            foreach (ParticleSystem system in Systems(root, only))
            {
                ParticleSystem.MainModule main = system.main;
                main.startDelay = seconds;
            }
        };
    }

    private static ParticleSystem.MinMaxCurve Mul(ParticleSystem.MinMaxCurve curve, float factor)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                curve.constant *= factor;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                curve.constantMin *= factor;
                curve.constantMax *= factor;
                break;
            default:
                curve.curveMultiplier *= factor;
                break;
        }
        return curve;
    }

    private static Color Rgb(int hex)
    {
        return new Color(((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f, 1f);
    }

    // ---- tower wiring ----------------------------------------------------------------------------------

    private sealed class TowerEdit
    {
        public GameObject Root;
        public SerializedObject Manager;
        public readonly HashSet<VisualUpgrade> Kept = new HashSet<VisualUpgrade>();
    }

    private static void EditTower(string prefabName, Action<TowerEdit> edit)
    {
        string path = TowerFolder + prefabName + ".prefab";
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            Fail("missing tower " + path);
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            UpgradeManager manager = root.GetComponent<UpgradeManager>();
            TowerEdit tower = new TowerEdit { Root = root, Manager = manager != null ? new SerializedObject(manager) : null };
            edit(tower);

            // Visual upgrades of an earlier build that no module wants any more
            foreach (VisualUpgrade stale in root.GetComponents<VisualUpgrade>())
            {
                if (!tower.Kept.Contains(stale))
                {
                    RemoveFromModules(tower, stale);
                    Object.DestroyImmediate(stale);
                }
            }
            tower.Manager?.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The visual upgrade of module (path, tier), both 1-based, with these slot prefabs
    private static T Visual<T>(TowerEdit tower, int path, int tier, params (VisualSlot slot, GameObject prefab)[] entries) where T : VisualUpgrade
    {
        SerializedProperty upgrades = ModuleUpgrades(tower, path, tier);
        if (upgrades == null)
            return null;

        string key = VisualKeyPrefix + "p" + path + "t" + tier;
        T upgrade = null;
        foreach (VisualUpgrade existing in tower.Root.GetComponents<VisualUpgrade>())
        {
            if (Comment(existing) != key)
                continue;
            if (existing is T match && existing.GetType() == typeof(T))
                upgrade = match;
            else
            {
                RemoveFromModules(tower, existing);
                Object.DestroyImmediate(existing);
            }
            break;
        }
        if (upgrade == null)
            upgrade = tower.Root.AddComponent<T>();
        else
        {
            // Back to the defaults, so a setting dropped from a recipe doesn't linger
            GameObject scratch = new GameObject("scratch") { hideFlags = HideFlags.HideAndDontSave };
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(scratch.AddComponent<T>()), upgrade);
            Object.DestroyImmediate(scratch);
        }
        tower.Kept.Add(upgrade);

        SerializedObject data = new SerializedObject(upgrade);
        data.FindProperty("Comment").stringValue = key;
        data.FindProperty("priority").intValue = tier * 10 + path;
        SerializedProperty list = data.FindProperty("entries");
        list.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("slot").intValue = (int)entries[i].slot;
            entry.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            if (entries[i].prefab != null && entry.FindPropertyRelative("prefab").objectReferenceValue == null)
                Fail(tower.Root.name + " " + key + ": prefab for " + entries[i].slot + " missing");
        }
        data.ApplyModifiedPropertiesWithoutUndo();

        // First in the module, so a strategy swapped in by the same module already sees the look
        bool listed = false;
        for (int i = 0; i < upgrades.arraySize; i++)
        {
            if (upgrades.GetArrayElementAtIndex(i).objectReferenceValue == upgrade)
                listed = true;
        }
        if (!listed)
        {
            upgrades.InsertArrayElementAtIndex(0);
            upgrades.GetArrayElementAtIndex(0).objectReferenceValue = upgrade;
        }
        return upgrade;
    }

    private static SerializedProperty ModuleUpgrades(TowerEdit tower, int path, int tier)
    {
        SerializedProperty paths = tower.Manager?.FindProperty("upgradePaths");
        if (paths == null || path < 1 || path > paths.arraySize)
        {
            Fail(tower.Root.name + ": no upgrade path " + path);
            return null;
        }
        SerializedProperty modules = paths.GetArrayElementAtIndex(path - 1).FindPropertyRelative("upgradeModules");
        if (modules == null || tier < 1 || tier > modules.arraySize)
        {
            Fail(tower.Root.name + ": path " + path + " has no tier " + tier);
            return null;
        }
        return modules.GetArrayElementAtIndex(tier - 1).FindPropertyRelative("upgrades");
    }

    private static void RemoveFromModules(TowerEdit tower, Upgrade upgrade)
    {
        SerializedProperty paths = tower.Manager?.FindProperty("upgradePaths");
        if (paths == null)
            return;
        for (int p = 0; p < paths.arraySize; p++)
        {
            SerializedProperty modules = paths.GetArrayElementAtIndex(p).FindPropertyRelative("upgradeModules");
            for (int t = 0; t < modules.arraySize; t++)
            {
                SerializedProperty upgrades = modules.GetArrayElementAtIndex(t).FindPropertyRelative("upgrades");
                for (int i = upgrades.arraySize - 1; i >= 0; i--)
                {
                    if (upgrades.GetArrayElementAtIndex(i).objectReferenceValue == upgrade)
                    {
                        upgrades.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        upgrades.DeleteArrayElementAtIndex(i);
                    }
                }
            }
        }
    }

    private static string Comment(Upgrade upgrade)
    {
        return new SerializedObject(upgrade).FindProperty("Comment").stringValue;
    }

    private static void SetField(Object target, string field, object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property == null)
        {
            Fail(target.GetType().Name + " has no serialized field " + field);
            return;
        }
        switch (value)
        {
            case GameObject prefab:
                property.objectReferenceValue = prefab;
                break;
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case float number:
                property.floatValue = number;
                break;
            case int number:
                property.intValue = number;
                break;
            case bool flag:
                property.boolValue = flag;
                break;
            case null:
                property.objectReferenceValue = null;
                break;
            default:
                Fail("unsupported value for " + field);
                break;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool GetBool(Object target, string field)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(field);
        return property != null && property.boolValue;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
