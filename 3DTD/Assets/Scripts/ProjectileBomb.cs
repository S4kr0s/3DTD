using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectileBomb : Projectile
{
    [SerializeField] private SphereCollider sphereCollider;

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
            Die();

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
            DamageInArea();
            Die();

            if (Penetration <= 0)
            {
                DamageInArea();
                Die();
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

            DamageInArea();
            Die();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
        }
    }

    private void DamageInArea()
    {
        Collider[] hitColliders = Physics.OverlapSphere(this.transform.position, 0.75f);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
            {
                enemy.TakeDamage(damage, DamageType.EXPLOSIVE);
            }
        }
    }
}
