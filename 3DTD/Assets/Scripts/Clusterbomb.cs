using PolygonArsenal;
using UnityEngine;

public class Clusterbomb : MonoBehaviour
{
    public float lifetime = 1f;
    public float speed = 1f;
    public float damage = 1f;
    public float radius = 1f;
    public Tower tower;

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
        Collider[] hitColliders = Physics.OverlapSphere(this.transform.position, radius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
            {
                enemy.TakeDamage(damage, DamageType.EXPLOSIVE, this.tower);
            }
        }
    }
}
