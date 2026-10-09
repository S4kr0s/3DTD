using UnityEngine;

// Frame-rate independent fire timer shared by the action strategies.
// Leftover time carries over between volleys, so a tower fires 1 / interval volleys per second
// at any FPS and game speed, and can fire several volleys in one long frame.
//
// Magazine rules (unchanged from the original strategies): with AMMO > 0 the tower fires AMMO volleys
// FIRERATE apart, then reloads for RELOAD_SPEED seconds; the next volley follows FIRERATE after the reload.
// With AMMO <= 0 the tower fires continuously.
//
// VolleyAge(i) tells how long ago (game time) the i-th volley of this frame was due. Projectiles start that far
// along their way, so the stream of shots looks and hits the same at any frame rate or game speed.
public class FireCycle
{
    public const int MaxVolleysPerFrame = 8;

    private readonly float[] volleyAges = new float[MaxVolleysPerFrame];

    // Seconds since the i-th volley of the last Tick was due, between 0 and that frame's deltaTime
    public float VolleyAge(int i)
    {
        return i >= 0 && i < MaxVolleysPerFrame ? volleyAges[i] : 0f;
    }

    public bool IsReloading => reloading;
    public float Magazine => magazine;

    private float cooldown;
    private float magazine;
    private bool reloading;
    private float reloadTimer;
    // AMMO at the last Refill: the magazine size CopyStateFrom clamps to
    private float capacity;

    public FireCycle(float ammo)
    {
        Refill(ammo);
    }

    public void Refill(float ammo)
    {
        capacity = ammo;
        magazine = ammo;
        reloading = false;
        reloadTimer = 0f;
    }

    // Takes over another cycle's cooldown, reload and magazine (clamped to this cycle's capacity), so swapping a
    // tower's strategy doesn't hand it a free volley or skip a reload
    public void CopyStateFrom(FireCycle other)
    {
        if (other == null || other == this)
            return;

        cooldown = other.cooldown;
        reloading = other.reloading;
        reloadTimer = other.reloadTimer;
        magazine = capacity > 0f ? Mathf.Min(other.magazine, capacity) : other.magazine;
    }

    // Advances the timer and returns how many volleys to fire this frame
    public int Tick(float deltaTime, bool hasTarget, float interval, float ammo, float reloadTime)
    {
        float frameTime = deltaTime;
        interval = Mathf.Max(StatsManager.MinFireInterval, interval);
        bool usesMagazine = ammo > 0f;

        if (reloading)
        {
            reloadTimer -= deltaTime;
            if (reloadTimer > 0f)
                return 0;

            magazine = ammo;
            reloading = false;
            deltaTime = -reloadTimer;   // time left over in this frame after the reload finished
        }
        else if (usesMagazine && magazine <= 0f)
        {
            // AMMO was raised by an upgrade while the tower had no magazine yet
            magazine = ammo;
        }

        cooldown -= deltaTime;

        if (!hasTarget)
        {
            // Don't bank shots while idle
            if (cooldown < 0f)
                cooldown = 0f;
            return 0;
        }

        int volleys = 0;
        bool reloadStarted = false;
        while (cooldown <= 0f && volleys < MaxVolleysPerFrame)
        {
            // The volley was due -cooldown seconds before the end of this frame
            volleyAges[volleys] = Mathf.Clamp(-cooldown, 0f, frameTime);
            volleys++;
            cooldown += interval;

            if (usesMagazine)
            {
                magazine--;
                if (magazine <= 0f)
                {
                    reloading = reloadTime > 0f;
                    reloadTimer = reloadTime;
                    if (!reloading)
                    {
                        magazine = ammo;
                    }
                    else
                    {
                        reloadStarted = true;
                        break;
                    }
                }
            }
        }

        // A frame that hit the volley cap carries its remaining debt (up to one more capped frame) into the next
        // frames, so long frames don't drop volleys. After a reload starts, at most one volley of debt is kept, so
        // the shots due during the reload don't burst out when it ends.
        float maxDebt = volleys >= MaxVolleysPerFrame && !reloadStarted ? MaxVolleysPerFrame * interval : interval;
        if (cooldown < -maxDebt)
            cooldown = -maxDebt;

        return volleys;
    }
}
