using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;

public class ProjectileBasic : Projectile
{
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


        target = null;
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
            if (Penetration > 0)
            {
                enemy.TakeDamage(damage, DamageType.PROJECTILE);
                target = null;
                Penetration--;
            }

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
