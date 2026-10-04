using System.Collections.Generic;
using PolygonArsenal;
using UnityEngine;

public enum ProjectileKind
{
    Round,      // ProjectileRound: Bullet Dispenser bullets
    Basic,      // ProjectileBasic: Laser/Core bolts, starfighter cannons
    Bomb,       // ProjectileBomb: rockets and starfighter ordnance (blast, optional homing and cluster bomblets)
    Cluster,    // Clusterbomb: bomblets of cluster rockets
}

// What ProjectileSystem needs to know about a projectile prefab, read once from the prefab itself: the
// prefabs stay the authoring source (behaviour component and its settings, collider = hit shape,
// PolygonProjectileScript = muzzle/flight/impact effects), the towers' stats set speed, damage and so on.
public sealed class ProjectileArchetype
{
    private static readonly Dictionary<GameObject, ProjectileArchetype> cache = new Dictionary<GameObject, ProjectileArchetype>();

    public readonly GameObject Prefab;
    public readonly ProjectileKind Kind;

    // Hit shape at scale 1: a capsule along the flight direction (a sphere when HalfLength is 0)
    public readonly float HitRadius;
    public readonly float HitHalfLength;

    public readonly GameObject MuzzleEffect;
    public readonly GameObject FlightEffect;
    public readonly GameObject ImpactEffect;

    // Bombs: the blast radius the prefab defaults to, and the bomblets of cluster rockets
    public readonly float DefaultBlastRadius;
    public readonly ProjectileArchetype ClusterBomblet;
    public readonly Vector3[] ClusterPositions;
    public readonly Quaternion[] ClusterRotations;
    public readonly float ClusterDamageShare;
    public readonly float ClusterRadiusShare;

    // Bomblets fly with their prefab's speed and lifetime
    public readonly float ClusterSpeed;
    public readonly float ClusterLifetime;

    public static ProjectileArchetype Get(GameObject prefab)
    {
        if (prefab == null)
            return null;
        if (!cache.TryGetValue(prefab, out ProjectileArchetype archetype))
        {
            archetype = new ProjectileArchetype(prefab);
            cache.Add(prefab, archetype);
        }
        return archetype;
    }

    // Whether the system can simulate this prefab; other projectiles (pulses, legacy towers) stay GameObjects
    public static bool IsSimulated(GameObject prefab)
    {
        return prefab != null && (prefab.GetComponent<ProjectileRound>() != null || prefab.GetComponent<ProjectileBasic>() != null
            || prefab.GetComponent<ProjectileBomb>() != null || prefab.GetComponent<Clusterbomb>() != null);
    }

    private ProjectileArchetype(GameObject prefab)
    {
        Prefab = prefab;

        if (prefab.TryGetComponent(out ProjectileBomb bomb))
        {
            Kind = ProjectileKind.Bomb;
            DefaultBlastRadius = bomb.radius;
            ClusterDamageShare = bomb.ClusterDamageShare;
            ClusterRadiusShare = bomb.ClusterRadiusShare;
            if (bomb.ClusterProjectilePrefab != null && bomb.ClusterProjectileFirePoints != null)
            {
                ClusterBomblet = Get(bomb.ClusterProjectilePrefab);
                List<Vector3> positions = new List<Vector3>();
                List<Quaternion> rotations = new List<Quaternion>();
                foreach (Transform point in bomb.ClusterProjectileFirePoints)
                {
                    if (point == null)
                        continue;
                    // Fire points are children of the rocket: relative to its root, before the rocket's own scale
                    positions.Add(prefab.transform.InverseTransformPoint(point.position));
                    rotations.Add(Quaternion.Inverse(prefab.transform.rotation) * point.rotation);
                }
                ClusterPositions = positions.ToArray();
                ClusterRotations = rotations.ToArray();
            }
        }
        else if (prefab.TryGetComponent(out Clusterbomb bomblet))
        {
            Kind = ProjectileKind.Cluster;
            ClusterSpeed = bomblet.speed;
            ClusterLifetime = bomblet.lifetime;
        }
        else if (prefab.GetComponent<ProjectileBasic>() != null)
        {
            Kind = ProjectileKind.Basic;
        }
        else
        {
            Kind = ProjectileKind.Round;
        }

        ReadHitShape(prefab, out HitRadius, out HitHalfLength);

        if (prefab.TryGetComponent(out PolygonProjectileScript visuals))
        {
            MuzzleEffect = visuals.muzzleParticle;
            FlightEffect = visuals.projectileParticle;
            ImpactEffect = visuals.impactParticle;
        }
    }

    // The enabled, non-trigger colliders on active objects, as one capsule along the prefab's forward axis.
    // The root's own scale doesn't count: towers set the projectile's scale from the SIZE stat.
    private static void ReadHitShape(GameObject prefab, out float radius, out float halfLength)
    {
        radius = 0f;
        halfLength = 0f;
        Transform root = prefab.transform;
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(false))
        {
            if (!collider.enabled || collider.isTrigger)
                continue;

            Vector3 scale = collider.transform == root ? Vector3.one : Div(collider.transform.lossyScale, root.lossyScale);
            Vector3 center = collider.transform == root ? Vector3.zero : root.InverseTransformPoint(collider.transform.position);
            float sideways = new Vector2(center.x, center.y).magnitude;

            if (collider is SphereCollider sphere)
            {
                float r = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                radius = Mathf.Max(radius, r + sideways);
                halfLength = Mathf.Max(halfLength, Mathf.Abs(center.z + sphere.center.z * scale.z));
            }
            else if (collider is CapsuleCollider capsule)
            {
                float r = capsule.radius * Mathf.Max(scale.x, scale.y);
                radius = Mathf.Max(radius, r + sideways);
                // Direction 2 is the Z axis, i.e. along the flight direction
                float axial = capsule.direction == 2 ? Mathf.Max(0f, capsule.height * 0.5f * scale.z - r) : 0f;
                halfLength = Mathf.Max(halfLength, axial + Mathf.Abs(center.z));
            }
            else
            {
                Bounds bounds = collider.bounds;
                radius = Mathf.Max(radius, Mathf.Max(bounds.extents.x, bounds.extents.y));
                halfLength = Mathf.Max(halfLength, bounds.extents.z);
            }
        }

        if (radius <= 0f)
            radius = 0.075f;
    }

    private static Vector3 Div(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z));
    }
}
