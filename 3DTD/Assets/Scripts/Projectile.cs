using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] public TowerData towerData;
    [SerializeField] public GameObject target;
    [SerializeField] public int penetration;
    [SerializeField] public float maxSpeed;
    [SerializeField] public float lifetime;
    public event Action<GameObject> OnProjectileDeath;

    private void Awake()
    {
        OnProjectileDeath += Projectile_OnProjectileDeath;
    }

    private void Projectile_OnProjectileDeath(GameObject obj)
    {
        // Play particles for death etc.
        Destroy(obj);
    }

    public GameObject Target { get { return target; } set { target = value; } }
    public int Penetration { get { return penetration; } set { penetration = value; } }



    protected void Die()
    {
        OnProjectileDeath?.Invoke(this.gameObject);
    }
}
