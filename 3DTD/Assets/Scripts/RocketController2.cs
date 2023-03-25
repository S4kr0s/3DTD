using UnityEngine;

public class RocketController2 : MonoBehaviour
{
    public static Vector3 RandomPointOnSphere(float radius)
    {
        // Generate two random numbers between 0 and 1
        float rand1 = Random.Range(0f, 1f);
        float rand2 = Random.Range(0f, 1f);

        // Calculate the angles for the random point on the sphere
        float theta = 2f * Mathf.PI * rand1;
        float phi = Mathf.Acos(2f * rand2 - 1f);

        // Calculate the x, y, z coordinates for the random point on the sphere
        float x = radius * Mathf.Sin(phi) * Mathf.Cos(theta);
        float y = radius * Mathf.Sin(phi) * Mathf.Sin(theta);
        float z = radius * Mathf.Cos(phi);

        // Return the vector representing the random point on the sphere
        return new Vector3(x, y, z);
    }
}
