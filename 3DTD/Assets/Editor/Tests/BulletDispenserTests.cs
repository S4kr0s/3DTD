using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

// EditMode tests for the Bullet Dispenser built by BulletDispenserBuilder and the ricochet maths it relies on
public class BulletDispenserTests
{
    private const string PrefabPath = "Assets/Prefabs/Tower/Bullet Dispenser Tower.prefab";

    private static readonly ProjectileSystem.Dome UnitDome = new ProjectileSystem.Dome
    {
        Center = float3.zero,
        Up = new float3(0f, 1f, 0f),
        Radius = 3f,
    };

    [Test]
    public void DomeExitMeetsTheSphere()
    {
        Assert.IsTrue(ProjectileSystem.DomeExit(new float3(0f, 1f, 0f), new float3(1f, 0f, 0f), 10f, UnitDome, out float travel, out float3 normal));
        Assert.AreEqual(math.sqrt(8f), travel, 1e-4f);
        Assert.AreEqual(1f, math.length(normal), 1e-4f);
        Assert.Greater(normal.x, 0.9f);
    }

    [Test]
    public void DomeExitMeetsTheFlatSide()
    {
        Assert.IsTrue(ProjectileSystem.DomeExit(new float3(0f, 1f, 0f), math.normalize(new float3(1f, -1f, 0f)), 10f, UnitDome, out float travel, out float3 normal));
        Assert.AreEqual(math.sqrt(2f), travel, 1e-4f);
        Assert.AreEqual(-1f, normal.y, 1e-4f);
        // Reflected off the floor it goes up again
        float3 bounced = ProjectileSystem.Reflect(math.normalize(new float3(1f, -1f, 0f)), normal);
        Assert.Greater(bounced.y, 0.7f);
        Assert.Greater(bounced.x, 0.7f);
    }

    [Test]
    public void DomeExitIgnoresShortMovesAndOutsiders()
    {
        Assert.IsFalse(ProjectileSystem.DomeExit(new float3(0f, 1f, 0f), new float3(1f, 0f, 0f), 2f, UnitDome, out _, out _));
        Assert.IsFalse(ProjectileSystem.DomeExit(new float3(5f, 1f, 0f), new float3(-1f, 0f, 0f), 10f, UnitDome, out _, out _));
        Assert.IsFalse(ProjectileSystem.DomeExit(new float3(0f, -1f, 0f), new float3(1f, 0f, 0f), 10f, UnitDome, out _, out _));
    }

    [Test]
    public void ARicochetStaysInsideTheDome()
    {
        // Bounce a needle around the dome a few times: every rebound starts inside and moves inwards
        float3 position = new float3(0.2f, 1f, -0.3f);
        float3 direction = math.normalize(new float3(0.7f, 0.2f, 0.4f));
        for (int bounce = 0; bounce < 6; bounce++)
        {
            Assert.IsTrue(ProjectileSystem.DomeExit(position, direction, 100f, UnitDome, out float travel, out float3 normal), "bounce " + bounce);
            position += direction * travel - normal * 0.001f;
            direction = ProjectileSystem.Reflect(direction, normal);
            Assert.Less(math.dot(direction, normal), 0f);
            Assert.LessOrEqual(math.length(position), UnitDome.Radius + 1e-3f);
            Assert.GreaterOrEqual(position.y, -1e-3f);
        }
    }

    [Test]
    public void BarrelsGrowFromThreeToTwentyFour()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Tower tower = prefab.GetComponent<Tower>();
        Assert.AreEqual(24, tower.ShootingPoints.Length);

        UpgradePath barrels = prefab.GetComponent<UpgradeManager>().GetUpgradePaths()[0];
        int[] expected = { 3, 6, 12, 24 };
        HashSet<GameObject> enabled = new HashSet<GameObject>();
        foreach (ShootingPointReference point in tower.ShootingPoints)
        {
            if (point.IsReferenceEnabled)
                enabled.Add(point.gameObject);
        }
        Assert.AreEqual(expected[0], enabled.Count);
        for (int tier = 0; tier < 3; tier++)
        {
            foreach (GameObject activated in Activated(prefab, 0, tier))
            {
                if (activated.GetComponent<ShootingPointReference>() != null)
                    enabled.Add(activated);
            }
            Assert.AreEqual(expected[tier + 1], enabled.Count, "tier " + (tier + 1));
        }
        Assert.AreEqual(3, barrels.UpgradeModules.Length);
    }

    [Test]
    public void NoBarrelPointsDownAtTheBase()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Vector3 up = prefab.transform.forward;
        foreach (ShootingPointReference point in prefab.GetComponent<Tower>().ShootingPoints)
        {
            float elevation = Mathf.Asin(Vector3.Dot(point.transform.forward, up)) * Mathf.Rad2Deg;
            Assert.GreaterOrEqual(elevation, -25f, point.transform.parent.name);
        }
    }

    // Like the other towers it stays in its unit cell above the face it stands on, with every part shown
    [Test]
    public void TheTowerFitsItsCell()
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            Tower tower = instance.GetComponent<Tower>();
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
            {
                if (tower.Targetter == null || !part.IsChildOf(tower.Targetter.transform))
                    part.gameObject.SetActive(true);
            }
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (tower.Targetter != null && renderer.transform.IsChildOf(tower.Targetter.transform))
                    continue;
                Bounds bounds = renderer.bounds;
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), 0.5f, renderer.name);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)), 0.5f, renderer.name);
                Assert.GreaterOrEqual(bounds.min.y, -0.501f, renderer.name);
                Assert.LessOrEqual(bounds.max.y, 0.5f, renderer.name);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void PathsAreBarrelsRicochetAndGravity()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNull(prefab.GetComponent<ChangeActionStrategyUpgrade>(), "no strategy swaps any more");
        Assert.AreEqual(1, prefab.GetComponents<BulletDispenserTowerActionStrategy>().Length);

        int[] bounces = { 2, 4, 6 };
        int[] lastBounces = new int[3];
        float[] pulls = new float[3];
        for (int tier = 0; tier < 3; tier++)
        {
            DispenserRicochetUpgrade ricochet = Find<DispenserRicochetUpgrade>(prefab, 1, tier);
            Assert.IsNotNull(ricochet, "ricochet tier " + (tier + 1));
            lastBounces[tier] = ricochet.Bounces;
            DispenserGravityUpgrade gravity = Find<DispenserGravityUpgrade>(prefab, 2, tier);
            Assert.IsNotNull(gravity, "gravity tier " + (tier + 1));
            pulls[tier] = gravity.Pull;
        }
        CollectionAssert.AreEqual(bounces, lastBounces);
        Assert.Greater(pulls[1], pulls[0]);
        Assert.IsTrue(Find<DispenserRicochetUpgrade>(prefab, 1, 2).Seek);
        Assert.Greater(Find<DispenserGravityUpgrade>(prefab, 2, 2).SingularityInterval, 0f);
    }

    private static T Find<T>(GameObject prefab, int path, int tier) where T : Upgrade
    {
        SerializedProperty upgrades = Module(prefab, path, tier).FindPropertyRelative("upgrades");
        for (int i = 0; i < upgrades.arraySize; i++)
        {
            if (upgrades.GetArrayElementAtIndex(i).objectReferenceValue is T match)
                return match;
        }
        return null;
    }

    private static IEnumerable<GameObject> Activated(GameObject prefab, int path, int tier)
    {
        SerializedProperty upgrades = Module(prefab, path, tier).FindPropertyRelative("upgrades");
        for (int i = 0; i < upgrades.arraySize; i++)
        {
            if (!(upgrades.GetArrayElementAtIndex(i).objectReferenceValue is ChangeFirepointsUpgrade firepoints))
                continue;
            SerializedProperty list = new SerializedObject(firepoints).FindProperty("ToActivate");
            for (int k = 0; k < list.arraySize; k++)
                yield return list.GetArrayElementAtIndex(k).objectReferenceValue as GameObject;
        }
    }

    private static SerializedProperty Module(GameObject prefab, int path, int tier)
    {
        return new SerializedObject(prefab.GetComponent<UpgradeManager>()).FindProperty("upgradePaths")
            .GetArrayElementAtIndex(path).FindPropertyRelative("upgradeModules").GetArrayElementAtIndex(tier);
    }
}
