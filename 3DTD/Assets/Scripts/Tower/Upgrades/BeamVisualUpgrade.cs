using UnityEngine;

// Beam Tower look of an upgrade module (TowerBeam). Layered by priority: every set field (prefab not null,
// number above 0, overrideColor) overrides the lower upgrades', so the paths combine: one sets the width,
// another the tick pulse, a third the end flare. The tick pulse and the hit sparks are the Muzzle/Impact slots.
public class BeamVisualUpgrade : VisualUpgrade
{
    [Tooltip("Prefab with the beam's LineRenderer")]
    public GameObject line;
    [Tooltip("Looping effect at the emitter")]
    public GameObject start;
    [Tooltip("Looping effect at the far end")]
    public GameObject end;
    [Tooltip("Multiplies the line prefab's width")]
    public float width;
    [Tooltip("Width wobble, share of the width (0.1 = +-10 %)")]
    public float flicker;
    [Tooltip("Extra width right after a tick, share of the width")]
    public float tickSwell;
    [Tooltip("Scale of the start/end effects")]
    public float endScale;
    public bool overrideColor;
    [GradientUsage(true)]
    public Gradient color = new Gradient();
}
