using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPreview : MonoBehaviour
{
    [SerializeField] private GameObject projectileParticle;
    public GameObject[] trailParticles;
    [SerializeField] private Waypoints waypoints;
    private int waypointIndex = 0;
    [SerializeField] private float distanceTraveled = 0f;
    public float DistanceTraveled => distanceTraveled;
    private Quaternion randomRotation;
    [SerializeField] private float speedRandomRotation = 2f;

    private void Start()
    {
        randomRotation = Random.rotation;
        projectileParticle = Instantiate(projectileParticle, transform.position, transform.rotation) as GameObject;
        projectileParticle.transform.parent = transform;
    }

    private void Update()
    {
        float abs = Mathf.Abs(Quaternion.Dot(transform.rotation, randomRotation));

        if (abs >= 0.990f)
            randomRotation = Random.rotation;

        transform.rotation = Quaternion.Slerp(transform.rotation, randomRotation, speedRandomRotation * Time.deltaTime);

        Vector3 moveTowards = Vector3.MoveTowards(transform.position, waypoints.WaypointsArray[waypointIndex].position, 5 * Time.deltaTime);
        distanceTraveled += Vector3.Distance(transform.position, moveTowards);
        transform.position = moveTowards;

        if (Vector3.Distance(transform.position, waypoints.WaypointsArray[waypointIndex].position) < 0.1f)
            if (waypointIndex < waypoints.WaypointsArray.Count - 1)
                waypointIndex++;
    }

    private void FixedUpdate()
    {
        foreach (GameObject trail in trailParticles)
        {
            GameObject curTrail = transform.Find(projectileParticle.name + "/" + trail.name).gameObject;
            curTrail.transform.parent = null;
            Destroy(curTrail, 3f);
        }
        Destroy(projectileParticle, 3f);
    }
}
