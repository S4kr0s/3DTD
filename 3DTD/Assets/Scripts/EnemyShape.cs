using UnityEngine;

// One shape (layer) of the enemy prefab. When a layer pops, DeathEffectRenderer draws a growing, dissolving copy
// of the shape: its mesh and Layer*Animation material, grown by the shape's Animation clip (ExpandingShape.anim).
// Every pop plays, at any game speed (there used to be a cap of 64 and no pops above 2x).
public class EnemyShape : MonoBehaviour
{
    public static void SpawnDeathEffect(GameObject source, int id, Vector3 position, Quaternion rotation)
    {
        if (source == null)
            return;
        PerfCounters.DeathEffectsRequested++;
        if (DeathEffectRenderer.Play(source, id, position, rotation))
            PerfCounters.DeathEffectsPlayed++;
    }
}
