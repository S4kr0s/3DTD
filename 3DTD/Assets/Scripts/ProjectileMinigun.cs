using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectileMinigun : Projectile
{
    [SerializeField] private CapsuleCollider capsuleCollider;
    private Vector3 direction;
    private Vector3 cachedPosition;

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if(lifetime <= 0)
            Die();


        direction = transform.forward * maxSpeed * Time.deltaTime;
        cachedPosition = transform.position;
        transform.position += direction;

        if (Physics.SphereCast(cachedPosition, capsuleCollider.radius, direction, out RaycastHit hit, (lifetime / 2) * maxSpeed * Time.deltaTime))
        {
            this.gameObject.transform.position = hit.point;

            if (hit.collider.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
            {
                enemy.TakeDamage(damage, DamageType.PROJECTILE);

                Penetration--;

                if (Penetration <= 0)
                {
                    Die();
                    this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
                }
            }
            else
            {
                if (hit.collider.gameObject.layer == 2 || hit.collider.gameObject.layer == 9 || hit.collider.gameObject.layer == 0)
                    return;

                Die();
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            enemy.TakeDamage(damage, DamageType.PROJECTILE);

            Penetration--;

            if (Penetration <= 0)
            {
                Die();
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
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
