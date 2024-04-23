using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonArsenal;
using UnityEngine.UIElements;

public class ProjectilePulse : Projectile
{
    [SerializeField] private SphereCollider sphereCollider;
    private float origLifetime = 0;

    private void OnEnable()
    {
        sphereCollider.radius = 0.5f;
        sphereCollider.transform.localScale = new Vector3(1, 1, 1);
    }

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
            enemy.TakeDamage(damage, DamageType.EXPLOSIVE, this.tower);
        }
    }
}
