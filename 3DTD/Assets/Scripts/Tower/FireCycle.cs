using UnityEngine;

// Frame-rate independent fire timer shared by the action strategies.
// Leftover time carries over between volleys, so a tower fires 1 / interval volleys per second
// at any FPS and game speed, and can fire several volleys in one long frame.
//
// Magazine rules (unchanged from the original strategies): with AMMO > 0 the tower fires AMMO volleys
// FIRERATE apart, then reloads for RELOAD_SPEED seconds; the next volley follows FIRERATE after the reload.
// With AMMO <= 0 the tower fires continuously.
public class FireCycle
{
    public const int MaxVolleysPerFrame = 8;

    public bool IsReloading => reloading;
    public float Magazine => magazine;

    private float cooldown;
    private float magazine;
    private bool reloading;
    private float reloadTimer;

    public FireCycle(float ammo)
    {
        Refill(ammo);
    }

    public void Refill(float ammo)
    {
        magazine = ammo;
        reloading = false;
        reloadTimer = 0f;
    }

    // Advances the timer and returns how many volleys to fire this frame
    public int Tick(float deltaTime, bool hasTarget, float interval, float ammo, float reloadTime)
    {
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
        while (cooldown <= 0f && volleys < MaxVolleysPerFrame)
        {
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
                        magazine = ammo;
                    else
                        break;
                }
            }
        }

        // Never carry more than one volley of debt into the next frame
        if (cooldown < -interval)
            cooldown = -interval;

        return volleys;
    }
}
