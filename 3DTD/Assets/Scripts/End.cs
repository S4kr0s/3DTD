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
        if (other.CompareTag("Enemy") && other.TryGetComponent(out Enemy enemy))
            ReportExit(enemy);
    }

    // Also called by enemies that reached the last waypoint without touching the trigger (a long frame can carry
    // them past it); the leak handler ignores enemies that already left
    public void ReportExit(Enemy enemy)
    {
        OnEnemyReachedExit?.Invoke(enemy);
    }
}
