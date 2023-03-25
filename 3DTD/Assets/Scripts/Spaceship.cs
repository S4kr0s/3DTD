using UnityEngine;

public class Spaceship : MonoBehaviour
{
    [SerializeField] private float thrust = 10f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float sphereRadius = 10f;
    [SerializeField] private float targetRadius = 1f;
    [SerializeField] private Transform sphereCenter;
    [SerializeField] private float timeBetweenTargetChanges = 3f;
    [SerializeField] private float obstacleAvoidanceDistance = 0.5f;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float obstacleAvoidanceStrength = 2f;

    private Vector3 targetPoint;
    private Rigidbody rb;
    private float timeUntilTargetChange;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        SelectNewTargetPoint();
    }

    private void FixedUpdate()
    {
        // Calculate the direction to the target point
        Vector3 direction = targetPoint - transform.position;
        direction.Normalize();

        // Rotate the spaceship towards its velocity direction
        if (rb.velocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        // Apply thrust to move towards the target point
        rb.AddForce(direction * thrust * Time.fixedDeltaTime);

        // Check if we need to select a new target point
        if (timeUntilTargetChange <= 0f || Vector3.Distance(transform.position, targetPoint) < targetRadius)
        {
            SelectNewTargetPoint();
        }
        else
        {
            timeUntilTargetChange -= Time.fixedDeltaTime;
        }

        AvoidObstacles();

        // Raycast to check for obstacles
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, sphereRadius, LayerMask.GetMask("Map")))
        {
            // Calculate a new target point to avoid the obstacle
            Vector3 reflectDir = Vector3.Reflect(direction, hit.normal);
            Vector3 reflectPos = hit.point + reflectDir.normalized * targetRadius * 2f;
            targetPoint = reflectPos;

            // Draw a red line to the obstacle
            Debug.DrawLine(transform.position, hit.point, Color.red);

            // Draw a green line from the obstacle to the new target point
            Debug.DrawLine(hit.point, reflectPos, Color.green);
        }

        // Draw a yellow line to the target point
        Debug.DrawLine(transform.position, targetPoint, Color.yellow);
    }

    // Method for selecting a new target point
    private void SelectNewTargetPoint()
    {
        float u = Random.Range(0f, 1f);
        float v = Random.Range(0f, 1f);
        float theta = 2 * Mathf.PI * u;
        float phi = Mathf.Acos(2 * v - 1);
        float x = sphereCenter.transform.position.x + (sphereRadius * Mathf.Sin(phi) * Mathf.Cos(theta));
        float y = sphereCenter.transform.position.y + (sphereRadius * Mathf.Sin(phi) * Mathf.Sin(theta));
        float z = sphereCenter.transform.position.z + (sphereRadius * Mathf.Cos(phi));
        targetPoint = new Vector3(x, y, z);
        timeUntilTargetChange = timeBetweenTargetChanges;
    }

    private void AvoidObstacles()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, obstacleAvoidanceDistance, obstacleLayerMask);
        if (hitColliders.Length > 0)
        {
            Vector3 avoidDirection = Vector3.zero;
            foreach (Collider hitCollider in hitColliders)
            {
                avoidDirection += transform.position - hitCollider.transform.position;
            }
            targetPoint += avoidDirection.normalized * obstacleAvoidanceStrength;
            timeUntilTargetChange = timeBetweenTargetChanges;
        }
    }
}