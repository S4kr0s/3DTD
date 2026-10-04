using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Seconds the projectile shrinks to nothing after it died, before it goes back to its pool
    public const float FadeDuration = 0.1f;

    [SerializeField] public GameObject target;
    [SerializeField] public float damage;
    [SerializeField] public int penetration;
    [SerializeField] public float maxSpeed;
    [SerializeField] public float lifetime;
    [SerializeField] public float accuracy = 1f;
    [SerializeField] public Collider Collider;
    public event Action<GameObject> OnProjectileDeath;
    public Tower tower;

    // The pool that created this projectile; it returns itself there once the fade is over
    public ProjectilePoolManager Pool { get; set; }
    public bool IsDying => dying;

    public GameObject Target { get { return target; } set { target = value; } }
    public int Penetration { get { return penetration; } set { penetration = value; } }

    private bool dying;
    private float fadeTime;
    private Vector3 fadeStartScale;

    // Starts the fade; calling it again while fading does nothing
    protected void Die()
    {
        if (dying)
            return;

        dying = true;
        fadeTime = 0f;
        fadeStartScale = transform.localScale;
        if (Collider != null)
            Collider.enabled = false;
        OnProjectileDeath?.Invoke(this.gameObject);
    }

    // Subclasses call this first in Update. Shrinks a dying projectile; returns true once it is gone
    // (deactivated and back in its pool), in which case the caller must stop.
    protected bool UpdateFade()
    {
        if (!dying)
            return false;

        fadeTime += Time.deltaTime;
        if (fadeTime < FadeDuration)
        {
            transform.localScale = fadeStartScale * Mathf.Lerp(1f, 0f, fadeTime / FadeDuration);
            return false;
        }

        transform.localScale = Vector3.zero;
        dying = false;
        gameObject.SetActive(false);
        if (Pool != null)
            Pool.Release(gameObject);
        return true;
    }
}
