using UnityEngine;

public class SpaceshipController : MonoBehaviour
{

    public GameObject centerObject; // The center point of the sphere
    public float maxSpeed = 10.0f; // Maximum speed of the spaceship
    public float acceleration = 5.0f; // Acceleration of the spaceship
    public float rotationSpeed = 2.0f; // Rotation speed of the spaceship
    public float sphereRadius = 10.0f; // Radius of the sphere
    public LayerMask obstacleMask; // Layer mask for obstacle detection

    private Vector3 velocity = Vector3.zero; // Velocity of the spaceship
    private Vector3 targetPosition = Vector3.zero; // Random position on the sphere

    void Start()
    {
        // Set the initial random position of the spaceship on the sphere
        targetPosition = GetRandomPositionOnSphere();
        transform.position = targetPosition;
    }

    void Update()
    {
        // Check if the spaceship has reached the target position
        if (Vector3.Distance(transform.position, targetPosition) < 1.0f)
        {
            // Set a new random target position on the sphere
            targetPosition = GetRandomPositionOnSphere();
        }

        // Calculate the direction towards the target position
        Vector3 targetDirection = targetPosition - transform.position;

        // Calculate the rotation towards the target direction
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        // Apply rotation to the spaceship
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        // Check for obstacles in front of the spaceship
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 10.0f, obstacleMask))
        {
            // If an obstacle is detected, steer away from it
            Vector3 avoidDirection = Vector3.Reflect(transform.forward, hit.normal);
            targetRotation = Quaternion.LookRotation(avoidDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
        else
        {
            // If no obstacle is detected, accelerate towards the target position
            velocity += transform.forward * acceleration * Time.deltaTime;
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
            transform.position += velocity * Time.deltaTime;
        }

        // Point the spaceship in the direction of its velocity
        transform.LookAt(transform.position + velocity);
    }

    // Returns a random position on the surface of the sphere
    private Vector3 GetRandomPositionOnSphere()
    {
        Vector3 randomDirection = Random.insideUnitSphere.normalized;
        return randomDirection * sphereRadius + centerObject.transform.position;
    }
}