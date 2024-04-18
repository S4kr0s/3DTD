using UnityEngine;

public class SpaceshipAI : MonoBehaviour
{
    public float acceleration = 10f;
    public float rotationSpeed = 100f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void Update()
    {
        OrbitAroundPoint();
        AvoidCollision();

        // Make the spaceship look in the direction it's moving
        if (rb.velocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.velocity.normalized);
            rb.rotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }


    public Vector3 orbitPoint = Vector3.zero;
    public float orbitDistance = 50f;
    public float pathVariation = 5f;

    void OrbitAroundPoint()
    {
        Vector3 targetPosition = orbitPoint + (transform.position - orbitPoint).normalized * orbitDistance;
        // Add variation
        targetPosition += new Vector3(Random.Range(-pathVariation, pathVariation), Random.Range(-pathVariation, pathVariation), Random.Range(-pathVariation, pathVariation));

        Vector3 direction = (targetPosition - transform.position).normalized;
        rb.AddForce(direction * acceleration);
    }

    public LayerMask obstacleLayers;

    void AvoidCollision()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 100f, obstacleLayers))
        {
            Vector3 avoidDirection = Vector3.Reflect(transform.forward, hit.normal);
            rb.AddForce(avoidDirection * acceleration);
        }
    }

}
