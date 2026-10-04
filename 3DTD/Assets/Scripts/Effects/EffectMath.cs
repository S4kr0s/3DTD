using Unity.Mathematics;

// Rotation helpers for the effect jobs (Burst can't call Quaternion.Euler / eulerAngles)
public static class EffectMath
{
    // rotation * Quaternion.Euler(euler), returned as Unity Euler angles in degrees (Z, then X, then Y)
    public static float3 ComposeEulerDegrees(quaternion rotation, float3 eulerDegrees)
    {
        quaternion combined = math.mul(rotation, quaternion.EulerZXY(math.radians(eulerDegrees)));
        return math.degrees(ToEulerZXY(combined));
    }

    // Inverse of quaternion.EulerZXY (Unity's convention): radians
    public static float3 ToEulerZXY(quaternion q)
    {
        float4 v = q.value;
        float x = math.asin(math.clamp(2f * (v.w * v.x - v.y * v.z), -1f, 1f));
        float y = math.atan2(2f * (v.x * v.z + v.w * v.y), 1f - 2f * (v.x * v.x + v.y * v.y));
        float z = math.atan2(2f * (v.x * v.y + v.w * v.z), 1f - 2f * (v.x * v.x + v.z * v.z));
        return new float3(x, y, z);
    }
}
