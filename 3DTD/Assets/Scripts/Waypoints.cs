using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoints : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints;

    public List<Transform> WaypointsArray => waypoints;

    // Appends the tagged children that the serialized list doesn't hold yet. Some scenes serialize the whole
    // path, so adding every child again would make enemies walk it a second time; skipping known ones also makes
    // repeated calls (the UI builder invokes Awake by reflection) harmless.
    private void Awake()
    {
        if (waypoints == null)
            waypoints = new List<Transform>();

        foreach (Transform transform in GetComponentsInChildren<Transform>())
        {
            if (transform.gameObject.CompareTag("Waypoint") && !waypoints.Contains(transform))
            {
                waypoints.Add(transform);
            }
        }
    }
}
