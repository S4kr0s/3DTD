using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

// EditMode tests for the performance overhaul (see docs/HANDOFF.md)
public class EnemyShapeTests
{
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/Default Enemy.prefab";

    private static T Field<T>(Enemy enemy, string name)
    {
        return (T)typeof(Enemy).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
    }

    // SetLayer only toggles the outgoing and incoming shape; every id must still show exactly its own shape,
    // including Icosahedron Black (49), whose child the boss slot (50) shares
    [Test]
    public void EveryLayerShowsExactlyItsShape()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            Enemy enemy = instance.GetComponent<Enemy>();
            GameObject[] shapes = Field<GameObject[]>(enemy, "allPossibleShapes");
            EnemyData[] data = Field<EnemyData[]>(enemy, "allPossibleEnemyData");
            HashSet<GameObject> distinct = new HashSet<GameObject>(shapes);

            // Forwards, backwards and jumping between ids, like pops, regrowth and pooled reuse do
            List<int> order = new List<int>();
            for (int id = 0; id < data.Length; id++)
                order.Add(id);
            for (int id = data.Length - 1; id >= 0; id--)
                order.Add(id);
            order.AddRange(new[] { 49, 50, 49, 0, 50, 21, 49 });

            foreach (int id in order)
            {
                if (data[id] == null)
                    continue;
                enemy.Initialize(data[id], null, EnemyTrait.None);
                Assert.AreEqual(id, enemy.Id, "enemy data " + data[id].name + " starts at its own id");
                Assert.IsTrue(shapes[id].activeSelf, "shape of id " + id + " is visible");
                int active = 0;
                foreach (GameObject shape in distinct)
                {
                    if (shape != null && shape.activeSelf)
                        active++;
                }
                Assert.AreEqual(1, active, "exactly one shape is visible for id " + id);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}

public class AllocationFreeTextTests
{
    // The scrap counter is rebuilt in a StringBuilder every frame; it must read exactly like UIFormat.Tabular
    [Test]
    public void SetTabularMatchesTabular()
    {
        GameObject host = new GameObject("text", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            TMP_Text text = host.GetComponent<TextMeshProUGUI>();
            StringBuilder buffer = new StringBuilder();
            foreach (int value in new[] { 0, 7, 350, 1000000, -12, int.MaxValue, int.MinValue + 1 })
            {
                UIFormat.SetTabular(text, value, buffer);
                Assert.AreEqual(UIFormat.Tabular(value), text.text, "value " + value);
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
