using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for game logic that must give the same result at 10x speed and on slow frames
public class FireCycleTests
{
    private const float Tolerance = 1e-4f;

    // A frame that hits the volley cap keeps its debt: the next frame fires the rest instead of dropping it
    [Test]
    public void DebtCarriesOverAfterTheVolleyCap()
    {
        // 2 s of fire at 1/16 s intervals (exact in binary): 32 volleys due, 8 per frame
        FireCycle cycle = new FireCycle(0f);
        int first = cycle.Tick(2f, true, 0.0625f, 0f, 0f);
        int second = cycle.Tick(0f, true, 0.0625f, 0f, 0f);
        int third = cycle.Tick(0.01f, true, 0.0625f, 0f, 0f);

        Assert.AreEqual(FireCycle.MaxVolleysPerFrame, first);
        Assert.AreEqual(FireCycle.MaxVolleysPerFrame, second, "a second capped frame's worth of debt is kept");
        Assert.AreEqual(1, third, "debt beyond that is dropped");
    }

    // After a reload starts at most one volley of debt remains, so the shots due during the reload don't burst out
    [Test]
    public void ReloadKeepsTheOneIntervalClamp()
    {
        FireCycle cycle = new FireCycle(3f);
        Assert.AreEqual(3, cycle.Tick(0.95f, true, 0.1f, 3f, 1f));
        Assert.IsTrue(cycle.IsReloading);

        Assert.AreEqual(2, cycle.Tick(1.05f, true, 0.1f, 3f, 1f), "one interval of debt plus the 0.05 s after the reload");
    }

    [Test]
    public void VolleyAgesTellHowLongAgoEachVolleyWasDue()
    {
        FireCycle cycle = new FireCycle(0f);
        int volleys = cycle.Tick(0.35f, true, 0.1f, 0f, 0f);

        Assert.AreEqual(4, volleys);
        float[] expected = { 0.35f, 0.25f, 0.15f, 0.05f };
        for (int i = 0; i < volleys; i++)
            Assert.AreEqual(expected[i], cycle.VolleyAge(i), Tolerance, "age of volley " + i);
    }

    [Test]
    public void CopyStateFromKeepsCooldownReloadAndClampsTheMagazine()
    {
        FireCycle old = new FireCycle(5f);
        Assert.AreEqual(1, old.Tick(0.01f, true, 0.5f, 5f, 2f));

        FireCycle swapped = new FireCycle(2f);
        swapped.CopyStateFrom(old);
        Assert.AreEqual(2f, swapped.Magazine, "4 rounds left, clamped to the new magazine of 2");
        Assert.AreEqual(0, swapped.Tick(0.1f, true, 0.5f, 2f, 2f), "no free volley: the old cooldown continues");
        Assert.AreEqual(1, swapped.Tick(0.4f, true, 0.5f, 2f, 2f));

        FireCycle reloading = new FireCycle(1f);
        Assert.AreEqual(1, reloading.Tick(0.01f, true, 0.1f, 1f, 3f));
        FireCycle afterReload = new FireCycle(4f);
        afterReload.CopyStateFrom(reloading);
        Assert.IsTrue(afterReload.IsReloading, "a reload in progress carries over");
        Assert.AreEqual(0, afterReload.Tick(1f, true, 0.1f, 4f, 3f));
    }
}

public class LeadTests
{
    // A bolt fired age seconds ago (late volley of a long frame) starts that far along its flight; leading from
    // where the enemy was back then makes it meet the enemy
    [Test]
    public void AgeCompensatedLeadMeetsTheEnemy()
    {
        Vector3 from = Vector3.zero;
        Vector3 enemyNow = new Vector3(4f, 0f, 3f);
        Vector3 velocity = new Vector3(-1.5f, 0f, 2f);
        const float speed = 8f;
        const float age = 0.4f;

        Vector3 aim = AimUtility.PredictIntercept(from, enemyNow - velocity * age, velocity, speed);
        Assert.Less(ClosestApproach(from, (aim - from).normalized, speed, age, enemyNow, velocity), 1e-3f);

        Vector3 naive = AimUtility.PredictIntercept(from, enemyNow, velocity, speed);
        Assert.Greater(ClosestApproach(from, (naive - from).normalized, speed, age, enemyNow, velocity), 0.1f,
            "leading from the current position over-leads a late bolt");
    }

    private static float ClosestApproach(Vector3 from, Vector3 direction, float speed, float age, Vector3 enemyNow, Vector3 velocity)
    {
        float best = float.MaxValue;
        for (float t = 0f; t < 3f; t += 0.0005f)
        {
            Vector3 bolt = from + direction * speed * (age + t);
            Vector3 enemy = enemyNow + velocity * t;
            best = Mathf.Min(best, Vector3.Distance(bolt, enemy));
        }
        return best;
    }
}

public class WaypointsTests
{
    private static readonly MethodInfo Awake = typeof(Waypoints).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo List = typeof(Waypoints).GetField("waypoints", BindingFlags.Instance | BindingFlags.NonPublic);

    // Beginner Level 01 serializes the whole path; Awake must not append it a second time, however often it runs
    [Test]
    public void AwakeAddsNoDuplicatesAndIsIdempotent()
    {
        GameObject root = new GameObject("Waypoints");
        try
        {
            Waypoints waypoints = root.AddComponent<Waypoints>();
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                GameObject child = new GameObject("Waypoint " + i);
                child.tag = "Waypoint";
                child.transform.SetParent(root.transform);
                children.Add(child.transform);
            }

            // Two serialized, two only tagged
            List.SetValue(waypoints, new List<Transform> { children[0], children[1] });
            Awake.Invoke(waypoints, null);
            Awake.Invoke(waypoints, null);

            CollectionAssert.AreEqual(children, waypoints.WaypointsArray);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
