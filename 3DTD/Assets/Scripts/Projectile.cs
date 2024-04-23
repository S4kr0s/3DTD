using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] public GameObject target;
    [SerializeField] public float damage;
    [SerializeField] public int penetration;
    [SerializeField] public float maxSpeed;
    [SerializeField] public float lifetime;
    [SerializeField] public float accuracy = 1f;
    [SerializeField] public Collider Collider;
    public event Action<GameObject> OnProjectileDeath;
    public Tower tower;

    private void Awake()
    {
        OnProjectileDeath += Projectile_OnProjectileDeath;
    }

    private void Projectile_OnProjectileDeath(GameObject obj)
    {
        StartCoroutine(FadeOutCoroutine());
    }

    public GameObject Target { get { return target; } set { target = value; } }
    public int Penetration { get { return penetration; } set { penetration = value; } }

    private IEnumerator FadeOutCoroutine()
    {
        if (Collider != null)
            Collider.enabled = false;

        float duration = .1f;
        float currentTime = 0f;
        Vector3 originalScale = transform.localScale; 

        while (currentTime < duration)
        {
            // Calculate the current time over the duration.
            currentTime += Time.deltaTime;
            // Calculate the current scale of the GameObject.
            float scale = Mathf.Lerp(1, 0, currentTime / duration);
            transform.localScale = originalScale * scale; 
            yield return null; 
        }

        transform.localScale = Vector3.zero;
        // Destroy(gameObject); 
        gameObject.SetActive(false); 
    }

    protected void Die()
    {
        OnProjectileDeath?.Invoke(this.gameObject);
    }
}
