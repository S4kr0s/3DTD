using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectileBomb : Projectile
{
    [SerializeField] private SphereCollider sphereCollider;
    [SerializeField] public bool aimAtTarget = false;
    [SerializeField] public float radius = 1f;

    [Header("Cluster-Rocket Settings")]
    [SerializeField] public bool doClustering = false;
    [SerializeField] private GameObject clusterProjectilePrefab;
    [SerializeField] private Transform[] clusterProjectileFirePoints;
    [SerializeField] private float clusterLifetime = 0.5f;
    [Tooltip("Share of the rocket's damage and blast radius each bomblet gets")]
    [SerializeField] private float clusterDamageShare = 0.5f;
    [SerializeField] private float clusterRadiusShare = 0.6f;

    private bool updateDisabled = false;

    // Read by ProjectileArchetype; towers fire this prefab through ProjectileSystem
    public GameObject ClusterProjectilePrefab => clusterProjectilePrefab;
    public Transform[] ClusterProjectileFirePoints => clusterProjectileFirePoints;
    public float ClusterDamageShare => clusterDamageShare;
    public float ClusterRadiusShare => clusterRadiusShare;

    // Shared by all bombs; explosions are resolved one at a time on the main thread
    private static readonly Collider[] overlapBuffer = new Collider[128];

    private void OnEnable()
    {
        if (target != null)
            transform.LookAt(target.transform, Vector3.right);

        // Calculate the maximum inaccuracy angle based on the accuracy variable
        // For example, if accuracy is 1, maxAngle is 0; if accuracy is 0.1, maxAngle could be 10 degrees.
        float maxAngle = (1 - accuracy) * 25; // Adjust the multiplier (10 in this case) as needed for your game

        // Generate a random rotation within the inaccuracy range in all three dimensions
        Quaternion randomRotation = Quaternion.Euler(
            Random.Range(-maxAngle, maxAngle), // Pitch
            Random.Range(-maxAngle, maxAngle), // Yaw
            Random.Range(-maxAngle, maxAngle)  // Roll
        );

        // Apply the random rotation to the projectile
        transform.rotation = transform.rotation * randomRotation;

        if (!aimAtTarget)
            target = null;

        updateDisabled = false;
    }

    private void Update()
    {
        if (UpdateFade() || updateDisabled)
            return;

        // The target died or leaked; keep flying straight
        if (target != null && !target.activeInHierarchy)
            target = null;

        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
        {
            ExplosionTrigger();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollidedWithoutDeath();
            updateDisabled = true;
        }

        if (target != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.transform.position, maxSpeed * Time.deltaTime);
            transform.LookAt(target.transform, Vector3.right);
        }
        else
        {
            transform.position += transform.forward * maxSpeed * Time.deltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            Penetration--;
            ExplosionTrigger();

            if (Penetration <= 0)
            {
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
            }
            else
            {
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollidedWithoutDeath();
            }
        }
        else
        {
            if (collision.gameObject.layer == 2 || collision.gameObject.layer == 9 || collision.gameObject.layer == 0)
                return;

            ExplosionTrigger();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
        }
    }

    private void ExplosionTrigger()
    {
        DamageInArea();
        Die();
        if (doClustering)
        {
            Clustering();
            doClustering = false;
        }
    }

    private void Clustering()
    {
        int i = 0;
        foreach (Transform item in clusterProjectileFirePoints)
        {
            Clusterbomb clusterBomb = Instantiate(clusterProjectilePrefab, item.position, item.rotation, null).GetComponent<Clusterbomb>();
            clusterBomb.tower = this.tower;
            // Bomblets used to be hardcoded to 1 damage and radius 1; they now scale with the tower's stats
            clusterBomb.damage = damage * clusterDamageShare;
            clusterBomb.radius = radius * clusterRadiusShare;
            i++;
        }
    }

    private void DamageInArea()
    {
        int count = Physics.OverlapSphereNonAlloc(this.transform.position, radius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            if (overlapBuffer[i].gameObject.TryGetComponent<Enemy>(out Enemy enemy))
            {
                enemy.TakeDamage(damage, DamageType.EXPLOSIVE, this.tower);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
