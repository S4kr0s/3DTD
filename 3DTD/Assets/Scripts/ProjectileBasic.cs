using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectileBasic : Projectile
{
    private void Start()
    {
        Penetration = (int)towerData.BasePenetration;
    }

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
            enemy.TakeDamage(1f, DamageType.PROJECTILE);
            target = null;
            Penetration--;

            if (Penetration <= 0)
            {
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

            Die();
            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
        }
    }
}
