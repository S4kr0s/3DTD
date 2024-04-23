using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;
public class ProjectileLaser : Projectile
{
    private bool shouldTargetAnother = true;

    private void Update()
    {
        Start:

        lifetime -= Time.deltaTime;

        if (lifetime <= 0)
            Die();

        if (target != null)
        {
            float distance = Vector3.Distance(this.gameObject.transform.position, target.transform.position);
            if (distance < 0.02f)
            {
                target = Targetter.GetFirstEnemyInGame(target);
            }

            if (target == null)
            {
                goto Start;
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, target.transform.position, maxSpeed * Time.deltaTime);
                transform.LookAt(target.transform, Vector3.right);
            }
        }
        else
        {
            target = Targetter.GetFirstEnemyInGame(target);

            transform.position += transform.forward * maxSpeed * Time.deltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Enemy>(out Enemy enemy))
        {
            enemy.TakeDamage(damage, DamageType.PROJECTILE, this.tower);

            Penetration--;

            if (Penetration <= 0)
            {
                Die();
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollided();
            }
            else
            {
                this.gameObject.GetComponent<PolygonProjectileScript>().HasCollidedWithoutDeath();
                target = Targetter.GetFirstEnemyInGame(target);
                Debug.Log(target);
                if (target == null)
                    shouldTargetAnother = false;
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
