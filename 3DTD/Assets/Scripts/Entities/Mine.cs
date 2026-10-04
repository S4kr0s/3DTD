using UnityEngine;

// A mine of the Mine Factory (MineFactoryActionStrategy). It flies an arc from the factory to its spot on the
// enemy path and hovers there until the factory detonates it. Contacts, damage and seeking are handled by the
// factory, which also pools the mines; the mine itself only moves and animates.
public class Mine : MonoBehaviour
{
    public enum MineState { INACTIVE, LAUNCHING, ARMED }

    [SerializeField] private Transform visual;
    [Tooltip("Degrees per second while hovering; the mine spins faster while it flies")]
    [SerializeField] private float spinSpeed = 70f;
    [SerializeField] private float flightSpinMultiplier = 6f;
    [SerializeField] private float bobAmplitude = 0.04f;
    [SerializeField] private float bobFrequency = 1.5f;
    [Tooltip("Extra scale the mine pulses with while it re-arms after a detonation")]
    [SerializeField] private float rearmPulse = 0.3f;

    public MineState State => state;
    // Armed and not re-arming: an enemy touching it now sets it off
    public bool IsArmed => state == MineState.ARMED && rearmTimer <= 0f;
    // Where the mine hovers (or will land while it is still flying)
    public Vector3 Home => home;
    // Lane of the path the mine sits on, -1 for mines hovering above the factory
    public int Lane => lane;
    public Vector3 PathDirection => pathDirection;
    public int Charges { get { return charges; } set { charges = value; } }

    private MineState state = MineState.INACTIVE;
    private Vector3 start;
    private Vector3 home;
    private Vector3 up;
    private float arcHeight;
    private float flightTime;
    private float flightTimer;
    private float rearmTimer;
    private float rearmDuration;
    private float bobPhase;
    private float size = 1f;
    private int lane;
    private Vector3 pathDirection;
    private int charges;

    public void Launch(Vector3 from, Vector3 to, Vector3 up, float speed, float minFlightTime, float size, int lane, Vector3 pathDirection, int charges)
    {
        start = from;
        home = to;
        this.up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        this.size = size;
        this.lane = lane;
        this.pathDirection = pathDirection;
        this.charges = charges;

        float distance = Vector3.Distance(from, to);
        flightTime = Mathf.Max(minFlightTime, distance / Mathf.Max(0.1f, speed));
        flightTimer = 0f;
        // Lob the mine "upwards" (away from the face the factory sits on) before it drops onto its spot
        arcHeight = Mathf.Clamp(distance * 0.5f, 0.6f, 2.5f);
        rearmTimer = 0f;
        bobPhase = Random.value * Mathf.PI * 2f;

        transform.SetPositionAndRotation(from, Random.rotation);
        transform.localScale = Vector3.one * size;
        state = MineState.LAUNCHING;
        gameObject.SetActive(true);
    }

    public void Tick(float deltaTime)
    {
        switch (state)
        {
            case MineState.LAUNCHING:
                flightTimer += deltaTime;
                float t = Mathf.Clamp01(flightTimer / flightTime);
                transform.position = Vector3.Lerp(start, home, t) + up * (arcHeight * 4f * t * (1f - t));
                if (t >= 1f)
                    state = MineState.ARMED;
                break;

            case MineState.ARMED:
                bobPhase += deltaTime * bobFrequency * Mathf.PI * 2f;
                transform.position = home + up * (Mathf.Sin(bobPhase) * bobAmplitude * size);
                if (rearmTimer > 0f)
                    rearmTimer -= deltaTime;
                break;

            default:
                return;
        }

        if (visual != null)
        {
            float spin = spinSpeed * (state == MineState.LAUNCHING ? flightSpinMultiplier : 1f);
            visual.Rotate(Vector3.up, spin * deltaTime, Space.Self);
        }

        float pulse = 1f;
        if (rearmTimer > 0f && rearmDuration > 0f)
            pulse += rearmPulse * Mathf.Sin(Mathf.Clamp01(rearmTimer / rearmDuration) * Mathf.PI);
        transform.localScale = Vector3.one * (size * pulse);
    }

    // Seeking mines drift towards enemies; the factory moves their hover point
    public void MoveHome(Vector3 newHome)
    {
        home = newHome;
    }

    // A mine with charges left stays where it is and can't go off again for a moment
    public void Rearm(float duration)
    {
        rearmTimer = duration;
        rearmDuration = duration;
    }

    public void Deactivate()
    {
        state = MineState.INACTIVE;
        gameObject.SetActive(false);
    }
}
