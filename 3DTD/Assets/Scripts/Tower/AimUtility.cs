using UnityEngine;

public static class AimUtility
{
    // Point where a projectile fired now from shooter at the given speed meets an enemy moving at a
    // constant velocity. Falls back to the enemy's current position when there is no solution.
    public static Vector3 PredictIntercept(Vector3 shooter, Vector3 target, Vector3 targetVelocity, float projectileSpeed)
    {
        if (projectileSpeed <= 0.001f || targetVelocity.sqrMagnitude < 0.0001f)
            return target;

        Vector3 toTarget = target - shooter;
        float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(toTarget, targetVelocity);
        float c = Vector3.Dot(toTarget, toTarget);

        float time;
        if (Mathf.Abs(a) < 0.0001f)
        {
            time = Mathf.Abs(b) > 0.0001f ? -c / b : -1f;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
                return target;

            float root = Mathf.Sqrt(discriminant);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);
            time = Mathf.Min(t1, t2);
            if (time < 0f)
                time = Mathf.Max(t1, t2);
        }

        return time > 0f ? target + targetVelocity * time : target;
    }
}
