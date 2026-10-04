using PolygonArsenal;
using UnityEngine;

// Bomblet of a cluster rocket. ProjectileSystem simulates bomblets and reads speed and lifetime from this
// prefab; the behaviour below only runs for a bomblet that is instantiated as a GameObject.
public class Clusterbomb : MonoBehaviour
{
    public float lifetime = 1f;
    public float speed = 1f;
    public float damage = 1f;
    public float radius = 1f;
    public Tower tower;

    private static readonly Collider[] overlapBuffer = new Collider[128];

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
        {
            DamageInArea();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
            Destroy(this.gameObject);
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            DamageInArea();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
            Destroy(this.gameObject);
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
}
