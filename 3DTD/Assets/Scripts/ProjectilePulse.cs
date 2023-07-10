using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectilePulse : Projectile
{
    [SerializeField] private SphereCollider sphereCollider;
    private float origLifetime = 0;

    private void Start()
    {
        origLifetime = lifetime;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
            Die();
    }

    private void FixedUpdate()
    {
        Grow();
    }

    private void Grow()
    {
        Vector3 scale = new Vector3()
        {
            x = Mathf.Lerp(0, 5, (lifetime / origLifetime)),
            y = 1,
            z = Mathf.Lerp(0, 5, (lifetime / origLifetime))
        };

        sphereCollider.transform.localScale = scale;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            Penetration--;
            enemy.TakeDamage(damage, DamageType.EXPLOSIVE);

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

            this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
        }
    }
}
