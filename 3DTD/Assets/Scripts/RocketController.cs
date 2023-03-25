using UnityEngine;

public class RocketController : MonoBehaviour
{
    public GameObject targetObject;
    public float maxSpeed = 10f;
    public float minSpeed = 5f;
    public float rotationSpeed = 2f;
    public float detectionRadius = 5f;
    public float avoidanceDistance = 2f;
    public float sphereRadius = 10f;
    public float maxDistanceFromTarget = 50f;
    public float forceMultiplier = 100f;
    public float randomizationFloat = 1f;

    private Rigidbody rb;
    private Vector3 initialPosition;
    private Vector3 targetPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialPosition = transform.position;
        targetPosition = initialPosition;
    }

    void Update()
    {
        // Check if there are any obstacles in the way
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius);
        foreach (Collider collider in colliders)
        {
            if (collider.gameObject != gameObject && collider.gameObject != targetObject && collider.tag == "Map")
            {
                // Try to fly around the obstacle
                Vector3 avoidanceDirection = (transform.position - collider.transform.position).normalized;
                Vector3 newTargetPosition = targetPosition + avoidanceDirection * avoidanceDistance;
                RaycastHit hit;
                if (Physics.Raycast(transform.position, newTargetPosition - transform.position, out hit, detectionRadius))
                {
                    // If the ray hits another obstacle, try to fly around that one as well
                    Vector3 obstacleDirection = (hit.transform.position - transform.position).normalized;
                    newTargetPosition = hit.point + obstacleDirection * avoidanceDistance;
                }
                targetPosition = newTargetPosition;
            }
        }

        // Clamp the target position to a maximum distance from the targetObject
        Vector3 directionToTarget = targetPosition - targetObject.transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        if (distanceToTarget > maxDistanceFromTarget)
        {
            targetPosition = targetObject.transform.position + directionToTarget.normalized * maxDistanceFromTarget;
        }

        // Move towards the target position using AddForce
        distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        float speed = Mathf.Lerp(maxSpeed, minSpeed, distanceToTarget / sphereRadius);
        Vector3 force = (targetPosition - transform.position).normalized * speed * forceMultiplier;
        rb.AddForce(force, ForceMode.Force);

        // Rotate towards the direction of travel
        if (rb.velocity.magnitude > 0)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(rb.velocity.normalized), rotationSpeed * Time.deltaTime);
        }

        // If we have reached the target position, choose a new random target position on the opposite side of the sphere
        if (distanceToTarget < 0.1f)
        {
            Vector3 directionToOppositeSide = -directionToTarget;
            Vector3 randomOffset = (Random.insideUnitSphere * randomizationFloat) * sphereRadius;
            targetPosition = targetObject.transform.position + directionToOppositeSide.normalized * sphereRadius + randomOffset;
        }
    }
}