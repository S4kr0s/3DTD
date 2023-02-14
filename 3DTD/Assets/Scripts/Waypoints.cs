using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoints : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints;

    public List<Transform> WaypointsArray => waypoints;

    private void Awake()
    {
        foreach (Transform transform in GetComponentsInChildren<Transform>())
        {
            if(transform.gameObject.tag == "Waypoint")
            {
                waypoints.Add(transform);
            }
        }
    }
}
