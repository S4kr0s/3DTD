using UnityEngine;

// Sniper look of an upgrade module: tracer lines and flash size on top of the muzzle/impact slots.
// Layered by priority: every set field (prefab not null, number above 0) overrides the lower upgrades'.
public class SniperVisualUpgrade : VisualUpgrade
{
    [Tooltip("Line prefab of the main shot's tracer")]
    public GameObject tracer;
    [Tooltip("Tracer of the second shot (Double-Shot, at the strongest enemy)")]
    public GameObject secondTracer;
    [Tooltip("Tracer of the third shot (Triple-Shot, at the last enemy)")]
    public GameObject thirdTracer;
    [Tooltip("Multiplies the tracer prefab's line width")]
    public float tracerWidth;
    [Tooltip("Seconds the tracer stays")]
    public float tracerDuration;
    [Tooltip("Scale of the muzzle flash")]
    public float muzzleScale;
    [Tooltip("Scale of the impact effect")]
    public float impactScale;
}
