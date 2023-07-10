using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class End : MonoBehaviour
{
    public event Action<Enemy> OnEnemyReachedExit;

    private static End instance;
    public static End Instance { get { return instance; } }

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            Enemy enemy = other.gameObject.GetComponent<Enemy>();
            OnEnemyReachedExit?.Invoke(enemy);
        }
    }
}
