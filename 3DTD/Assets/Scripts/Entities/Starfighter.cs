using System.Collections.Generic;
using UnityEngine;

// A small fighter launched by the HangarTowerActionStrategy.
// It patrols a sphere around its hangar and, as soon as enemies are in range, flies strafing runs
// slightly above the enemy path: approach -> strafe over the target while firing -> break away -> repeat.
public class Starfighter : MonoBehaviour
{
    private enum FlightState { LAUNCHING, PATROLLING, APPROACHING, STRAFING, BREAKING_AWAY }

    public Enemy Target => target;

    [Header("Flight")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float attackSpeed = 5.5f;
    [SerializeField] private float acceleration = 5f;
    [Tooltip("Degrees per second")]
    [SerializeField] private float patrolTurnRate = 120f;
    [Tooltip("Degrees per second")]
    [SerializeField] private float attackTurnRate = 200f;
    [SerializeField] private float maxBankAngle = 65f;
    [Tooltip("Degrees of bank per degree/second of turning")]
    [SerializeField] private float bankPerTurnRate = 0.4f;
    [SerializeField] private float bankResponsiveness = 6f;

    [Header("Patrol")]
    [Tooltip("Patrol sphere radius as a fraction of the tower range")]
    [Range(0.2f, 1f)]
    [SerializeField] private float patrolRadiusFactor = 0.7f;
    [SerializeField] private float patrolPointReachedDistance = 0.6f;
    [SerializeField] private float maxTimePerPatrolPoint = 4f;
    [SerializeField] private float launchDistance = 1.2f;

    [Header("Attack Runs")]
    [Tooltip("Height above the enemies while strafing")]
    [SerializeField] private float strafeAltitude = 1.1f;
    [SerializeField] private float runEntryDistance = 3.5f;
    [SerializeField] private float runOvershootDistance = 2f;
    [SerializeField] private float maxApproachTime = 6f;
    [SerializeField] private float maxStrafeTime = 3f;
    [SerializeField] private float breakAwayClimb = 2.5f;
    [SerializeField] private float fireConeAngle = 30f;
    [SerializeField] private float fireDistance = 5f;
    [SerializeField] private float missileLaunchInterval = 0.2f;
    [SerializeField] private float bombDropInterval = 0.3f;
    [Tooltip("Enemies closer than this to the fighter get bombed during carpet bombing")]
    [SerializeField] private float bombDropRadius = 2.5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float avoidanceRadius = 0.15f;
    [SerializeField] private float avoidanceLookAhead = 1.5f;
    [SerializeField] private float separationDistance = 0.6f;
    [Tooltip("Fighters are pulled back once they get further than range * leash from the hangar")]
    [SerializeField] private float leashFactor = 1.3f;

    [Header("References")]
    [SerializeField] private Transform[] cannonMuzzles;
    [SerializeField] private Transform ordnanceBay;
    [SerializeField] private GameObject[] missilePodVisuals;

    private HangarTowerActionStrategy strategy;
    private Transform launchPoint;
    private ProjectilePoolManager cannonPool;
    private ProjectilePoolManager ordnancePool;

    private FlightState state;
    private float stateTimer;
    private Vector3 heading;
    private float speed;
    private float bank;
    private float noiseSeed;

    private Vector3 patrolPoint;
    private Enemy target;
    private bool headOnRun;
    private Vector3 runDirection;
    private Vector3 runOffset;
    private Vector3 breakAwayPoint;

    private float shotCooldown;
    private float magazine;
    private float reloadTimer;
    private int nextMuzzle;
    private int pendingMissiles;
    private float missileTimer;
    private float bombTimer;
    private bool missilePodsVisible;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];
    private readonly Collider[] colliderBuffer = new Collider[8];

    public void Setup(HangarTowerActionStrategy strategy, Transform launchPoint, int cannonPoolSize, int ordnancePoolSize, GameObject cannonProjectile, GameObject ordnanceProjectile)
    {
        this.strategy = strategy;
        this.launchPoint = launchPoint;

        cannonPool = gameObject.AddComponent<ProjectilePoolManager>();
        cannonPool.Setup(cannonProjectile, cannonPoolSize);
        ordnancePool = gameObject.AddComponent<ProjectilePoolManager>();
        ordnancePool.Setup(ordnanceProjectile, ordnancePoolSize);

        heading = launchPoint.forward;
        speed = patrolSpeed * 0.5f;
        magazine = GetStat(Stat.StatType.AMMO);
        noiseSeed = Random.value * 100f;
        SetMissilePodsVisible(false);
        SetState(FlightState.LAUNCHING);
    }

    private void Update()
    {
        if (strategy == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        stateTimer += deltaTime;
        UpdateCannonCooldown(deltaTime);

        if (missilePodsVisible != strategy.missilesPerRun > 0)
            SetMissilePodsVisible(strategy.missilesPerRun > 0);

        switch (state)
        {
            case FlightState.LAUNCHING:
                UpdateLaunching(deltaTime);
                break;
            case FlightState.PATROLLING:
                UpdatePatrolling(deltaTime);
                break;
            case FlightState.APPROACHING:
                UpdateApproaching(deltaTime);
                break;
            case FlightState.STRAFING:
                UpdateStrafing(deltaTime);
                break;
            case FlightState.BREAKING_AWAY:
                UpdateBreakingAway(deltaTime);
                break;
        }
    }

    private void SetState(FlightState newState)
    {
        state = newState;
        stateTimer = 0f;
    }

    #region States

    private void UpdateLaunching(float deltaTime)
    {
        // Leave the hangar through the door and climb away from the building
        Vector3 exitPoint = launchPoint.position + launchPoint.forward * launchDistance + strategy.TowerUp * 0.4f;
        Fly(exitPoint - transform.position, patrolSpeed, patrolTurnRate, deltaTime);

        if (Vector3.Distance(transform.position, exitPoint) < 0.3f || stateTimer > 3f)
            EnterPatrol();
    }

    private void EnterPatrol()
    {
        target = null;
        PickPatrolPoint();
        SetState(FlightState.PATROLLING);
    }

    private void UpdatePatrolling(float deltaTime)
    {
        if (strategy.HasEnemiesInRange() && TryStartAttackRun())
            return;

        if (Vector3.Distance(transform.position, patrolPoint) < patrolPointReachedDistance || stateTimer > maxTimePerPatrolPoint)
        {
            PickPatrolPoint();
            stateTimer = 0f;
        }

        // Vary the cruising speed a little so the squadron doesn't fly in lockstep
        float cruiseSpeed = patrolSpeed * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 0.3f, noiseSeed));
        Fly(patrolPoint - transform.position, cruiseSpeed, patrolTurnRate, deltaTime);
    }

    private bool TryStartAttackRun()
    {
        target = strategy.AcquireTarget(this);
        if (target == null)
            return false;

        Vector3 pathDirection = GetPathDirection(target);
        // Fly head-on against the enemy stream when in front of it, chase it from behind otherwise
        headOnRun = Vector3.Dot(transform.position - target.transform.position, pathDirection) >= 0f;
        UpdateRunGeometry();
        SetState(FlightState.APPROACHING);
        return true;
    }

    private void UpdateApproaching(float deltaTime)
    {
        if (!IsValidTarget(target))
        {
            if (!TryStartAttackRun())
                EnterPatrol();
            return;
        }

        UpdateRunGeometry();

        Vector3 targetPosition = target.transform.position;
        Vector3 entryPoint = ClampToSphere(targetPosition + runOffset * strafeAltitude * 1.3f - runDirection * runEntryDistance, strategy.Range + 1f);
        Fly(entryPoint - transform.position, attackSpeed, attackTurnRate, deltaTime);
        TryFireCannons();

        Vector3 toTarget = targetPosition - transform.position;
        float alongRun = Vector3.Dot(toTarget, runDirection);
        float lateral = Vector3.ProjectOnPlane(toTarget, runDirection).magnitude;
        bool linedUp = Vector3.Dot(heading, runDirection) > 0.6f && alongRun > 0.5f && lateral < strafeAltitude + 1.5f;

        // Points inside the turning circle can't be hit exactly, close enough is good enough
        float turnRadius = speed / (attackTurnRate * strategy.turnRateMultiplier * Mathf.Deg2Rad);
        bool reachedEntry = Vector3.Distance(transform.position, entryPoint) < Mathf.Max(0.8f, turnRadius);

        if (reachedEntry || linedUp)
            EnterStrafe();
        else if (stateTimer > maxApproachTime && !TryStartAttackRun())
            EnterPatrol();
    }

    private void EnterStrafe()
    {
        pendingMissiles = strategy.missilesPerRun;
        missileTimer = 0f;
        bombTimer = 0f;
        SetState(FlightState.STRAFING);
    }

    private void UpdateStrafing(float deltaTime)
    {
        if (!IsValidTarget(target))
        {
            target = FindEnemyInFireCone();
            if (target == null)
            {
                EnterBreakAway();
                return;
            }
        }

        UpdateRunGeometry();

        Vector3 targetPosition = target.transform.position;
        float alongRun = Vector3.Dot(targetPosition - transform.position, runDirection);

        Vector3 desiredDirection;
        if (alongRun > 0.3f)
            // Aim slightly past the point above the target, so the fighter sweeps over it
            desiredDirection = targetPosition + runOffset * strafeAltitude + runDirection - transform.position;
        else
            // Target passed below: level off and keep going for a bit
            desiredDirection = runDirection + runOffset * 0.15f;

        Fly(desiredDirection, attackSpeed, attackTurnRate, deltaTime);

        TryFireCannons();
        LaunchPendingMissiles(deltaTime);
        if (strategy.carpetBombing)
            DropBombs(deltaTime);

        if (alongRun < -runOvershootDistance || stateTimer > maxStrafeTime)
            EnterBreakAway();
    }

    private void EnterBreakAway()
    {
        target = null;

        // Pull up and veer off to one side, staying inside the patrol sphere
        Vector3 lateral = Vector3.Cross(runDirection, runOffset).normalized * Random.Range(-1.5f, 1.5f);
        breakAwayPoint = ClampToSphere(transform.position + runDirection * 2f + runOffset * breakAwayClimb + lateral, strategy.Range);

        if (IsPointBlocked(breakAwayPoint, 0.4f))
            breakAwayPoint = strategy.Center + strategy.TowerUp * strategy.Range * 0.5f;

        SetState(FlightState.BREAKING_AWAY);
    }

    private void UpdateBreakingAway(float deltaTime)
    {
        Fly(breakAwayPoint - transform.position, attackSpeed, attackTurnRate * 0.8f, deltaTime);
        TryFireCannons();

        if (Vector3.Distance(transform.position, breakAwayPoint) < 0.8f || stateTimer > 3f)
        {
            if (!strategy.HasEnemiesInRange() || !TryStartAttackRun())
                EnterPatrol();
        }
    }

    #endregion

    #region Navigation

    private void PickPatrolPoint()
    {
        float radius = strategy.Range * patrolRadiusFactor;
        Vector3 towerUp = strategy.TowerUp;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 direction = Random.onUnitSphere;
            // Mostly patrol the open side of the block the hangar is built on
            if (Vector3.Dot(direction, towerUp) < -0.2f)
                direction = Vector3.Reflect(direction, towerUp);

            Vector3 point = strategy.Center + direction * radius * Random.Range(0.4f, 1f);

            // Early attempts insist on a longer, unobstructed leg, later ones take what they get
            bool picky = attempt < 8;
            if (picky && Vector3.Distance(point, transform.position) < radius * 0.5f)
                continue;
            if (IsPointBlocked(point, 0.4f))
                continue;
            if (picky && Physics.Linecast(transform.position, point, out RaycastHit hit, obstacleLayerMask, QueryTriggerInteraction.Ignore) && IsObstacle(hit.collider))
                continue;

            patrolPoint = point;
            return;
        }

        patrolPoint = strategy.Center + towerUp * radius * 0.5f;
    }

    private void UpdateRunGeometry()
    {
        Vector3 pathDirection = GetPathDirection(target);
        runDirection = headOnRun ? -pathDirection : pathDirection;

        // "Above" the path is world up; for vertical path segments fall back to the side the tower is on
        runOffset = Vector3.ProjectOnPlane(Vector3.up, pathDirection);
        if (runOffset.sqrMagnitude < 0.01f)
            runOffset = Vector3.ProjectOnPlane(strategy.Center - target.transform.position, pathDirection);
        if (runOffset.sqrMagnitude < 0.01f)
            runOffset = Vector3.Cross(pathDirection, Vector3.right);
        runOffset.Normalize();
    }

    private Vector3 GetPathDirection(Enemy enemy)
    {
        Vector3 pathDirection = enemy.PathDirection;
        if (pathDirection.sqrMagnitude > 0.01f)
            return pathDirection;

        // Enemy is at the end of its path, just fly across it
        Vector3 across = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
        return across.sqrMagnitude > 0.01f ? across.normalized : Vector3.forward;
    }

    private Vector3 ClampToSphere(Vector3 point, float radius)
    {
        Vector3 offset = point - strategy.Center;
        return offset.magnitude > radius ? strategy.Center + offset.normalized * radius : point;
    }

    private void Fly(Vector3 desiredDirection, float targetSpeed, float turnRate, float deltaTime)
    {
        Vector3 desired = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : heading;

        // Don't stray too far from the hangar
        Vector3 fromCenter = transform.position - strategy.Center;
        float leash = strategy.Range * leashFactor;
        if (fromCenter.magnitude > leash)
            desired = Vector3.Slerp(desired, -fromCenter.normalized, Mathf.Clamp01((fromCenter.magnitude - leash) / 2f));

        desired = SafeDirection(desired + GetSeparation(), heading);
        desired = SafeDirection(AvoidObstacles(desired, out float urgency), heading);

        float rate = turnRate * strategy.turnRateMultiplier * (1f + urgency * 2f);
        Vector3 newHeading = SafeDirection(Vector3.RotateTowards(heading, desired, rate * Mathf.Deg2Rad * deltaTime, 0f), heading);

        // Bank into turns like an aircraft (turning right dips the right wing)
        Vector3 referenceUp = GetReferenceUp(newHeading);
        float yawRate = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(heading, referenceUp),
            Vector3.ProjectOnPlane(newHeading, referenceUp),
            referenceUp) / deltaTime;
        float targetBank = Mathf.Clamp(-yawRate * bankPerTurnRate, -maxBankAngle, maxBankAngle);
        bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-bankResponsiveness * deltaTime));
        heading = newHeading;

        // Throttle back for sharp turns (tighter turning circle) and close to obstacles
        float alignment = Vector3.Dot(heading, desired);
        float turnThrottle = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(-0.2f, 0.8f, alignment));
        float maxSpeed = targetSpeed * strategy.flightSpeedMultiplier * turnThrottle * (1f - urgency * 0.4f);
        speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * strategy.flightSpeedMultiplier * deltaTime);

        transform.position += heading * speed * deltaTime;
        transform.rotation = Quaternion.LookRotation(heading, referenceUp) * Quaternion.Euler(0f, 0f, bank);

        PushOutOfObstacles();
    }

    // Normalized direction, or the fallback when the input is degenerate
    private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
    {
        float sqrMagnitude = direction.sqrMagnitude;
        if (sqrMagnitude < 0.000001f || float.IsNaN(sqrMagnitude))
            return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector3.forward;
        return direction / Mathf.Sqrt(sqrMagnitude);
    }

    private Vector3 GetReferenceUp(Vector3 forward)
    {
        // Avoid the LookRotation singularity when flying straight up or down
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.95f)
            return Vector3.up;

        Vector3 up = Vector3.ProjectOnPlane(transform.up, forward);
        return up.sqrMagnitude > 0.001f ? up.normalized : Vector3.forward;
    }

    private Vector3 GetSeparation()
    {
        Vector3 push = Vector3.zero;
        foreach (Starfighter other in strategy.Starfighters)
        {
            if (other == null || other == this)
                continue;

            Vector3 away = transform.position - other.transform.position;
            float distance = away.magnitude;
            if (distance < separationDistance && distance > 0.001f)
                push += away / distance * (1f - distance / separationDistance);
        }
        // Capped below 1 so it can never fully cancel the desired direction (e.g. a wingman right behind its leader)
        return Vector3.ClampMagnitude(push * 1.5f, 0.8f);
    }

    private Vector3 AvoidObstacles(Vector3 desired, out float urgency)
    {
        urgency = 0f;
        float lookAhead = avoidanceLookAhead + speed * 0.3f;

        // Probe where we are going, then where we want to turn to
        if (!CastForObstacle(heading, lookAhead, out RaycastHit hit) && !CastForObstacle(desired, lookAhead * 0.6f, out hit))
            return desired;

        urgency = 1f - Mathf.Clamp01(hit.distance / lookAhead);

        // Slide along the obstacle and push away from it the closer it gets
        Vector3 slide = Vector3.ProjectOnPlane(desired, hit.normal);
        if (slide.sqrMagnitude < 0.01f)
            slide = Vector3.ProjectOnPlane(heading, hit.normal);
        if (slide.sqrMagnitude < 0.01f)
            slide = Vector3.ProjectOnPlane(strategy.TowerUp, hit.normal);

        return (slide.normalized + hit.normal * (0.5f + urgency * 2f)).normalized;
    }

    private bool CastForObstacle(Vector3 direction, float distance, out RaycastHit closestHit)
    {
        closestHit = default;
        int hitCount = Physics.SphereCastNonAlloc(transform.position, avoidanceRadius, direction, hitBuffer, distance, obstacleLayerMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (!IsObstacle(hit.collider))
                continue;

            if (hit.distance <= 0f)
                // Overlapping at the start of the cast: unity reports no useful normal
                hit.normal = -direction;

            if (!found || hit.distance < closestHit.distance)
            {
                closestHit = hit;
                found = true;
            }
        }
        return found;
    }

    private void PushOutOfObstacles()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, avoidanceRadius, colliderBuffer, obstacleLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = colliderBuffer[i];
            if (!IsObstacle(obstacle))
                continue;

            // ClosestPoint doesn't support concave mesh colliders
            bool supportsClosestPoint = !(obstacle is MeshCollider meshCollider) || meshCollider.convex;
            Vector3 closest = supportsClosestPoint ? obstacle.ClosestPoint(transform.position) : obstacle.bounds.ClosestPoint(transform.position);

            Vector3 away = transform.position - closest;
            if (away.sqrMagnitude < 0.0001f)
                away = transform.position - obstacle.bounds.center;

            if (away.sqrMagnitude < 0.0001f)
                continue;

            transform.position += away.normalized * Mathf.Max(0f, avoidanceRadius - away.magnitude);
            heading = SafeDirection(Vector3.RotateTowards(heading, away.normalized, 30f * Mathf.Deg2Rad, 0f), heading);
        }
    }

    private bool IsPointBlocked(Vector3 point, float clearance)
    {
        int count = Physics.OverlapSphereNonAlloc(point, clearance, colliderBuffer, obstacleLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (IsObstacle(colliderBuffer[i]))
                return true;
        }
        return false;
    }

    private bool IsObstacle(Collider other)
    {
        if (other.GetComponentInParent<Enemy>() != null || other.GetComponentInParent<Projectile>() != null)
            return false;

        // The fighter starts inside its own hangar
        if (state == FlightState.LAUNCHING && other.transform.IsChildOf(strategy.Tower.transform))
            return false;

        return true;
    }

    #endregion

    #region Weapons

    private float GetStat(Stat.StatType type)
    {
        return strategy.Tower.StatsManager.GetStatValue(type);
    }

    private bool IsValidTarget(Enemy enemy)
    {
        return enemy != null && enemy.IsAlive && strategy.IsInRange(enemy);
    }

    private Enemy FindEnemyInFireCone()
    {
        if (IsInFireCone(target))
            return target;

        Enemy best = null;
        float bestDistance = float.MaxValue;
        foreach (Enemy enemy in strategy.GetEnemiesInRange())
        {
            float distance = Vector3.Distance(enemy.transform.position, transform.position);
            if (distance < bestDistance && IsInFireCone(enemy))
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    private bool IsInFireCone(Enemy enemy)
    {
        if (enemy == null || !enemy.IsAlive)
            return false;

        Vector3 toEnemy = enemy.transform.position - transform.position;
        return toEnemy.magnitude <= fireDistance && Vector3.Angle(transform.forward, toEnemy) <= fireConeAngle;
    }

    private void UpdateCannonCooldown(float deltaTime)
    {
        // At most one shot can be banked, so fighters don't unload a burst after patrolling
        shotCooldown = Mathf.Max(shotCooldown - deltaTime, -strategy.Tower.StatsManager.GetFireInterval());

        if (reloadTimer > 0f)
        {
            reloadTimer -= deltaTime;
            if (reloadTimer <= 0f)
                magazine = GetStat(Stat.StatType.AMMO);
        }
    }

    private void TryFireCannons()
    {
        if (reloadTimer > 0f || shotCooldown > 0f || cannonMuzzles.Length == 0)
            return;

        Enemy shotTarget = FindEnemyInFireCone();
        if (shotTarget == null)
            return;

        float interval = strategy.Tower.StatsManager.GetFireInterval();
        int volleys = 0;

        // Leftover time carries over, so the cannon rate doesn't depend on the frame rate
        while (shotCooldown <= 0f && reloadTimer <= 0f && volleys < FireCycle.MaxVolleysPerFrame)
        {
            volleys++;
            shotCooldown += interval;

            if (strategy.twinLinkedCannons)
            {
                foreach (Transform muzzle in cannonMuzzles)
                    strategy.FireCannon(cannonPool, muzzle, shotTarget);
            }
            else
            {
                nextMuzzle = (nextMuzzle + 1) % cannonMuzzles.Length;
                strategy.FireCannon(cannonPool, cannonMuzzles[nextMuzzle], shotTarget);
            }

            magazine--;
            if (magazine <= 0f)
            {
                // Cannons overheat after a burst
                float reloadSpeed = GetStat(Stat.StatType.RELOAD_SPEED);
                if (reloadSpeed > 0f)
                    reloadTimer = reloadSpeed;
                else
                    magazine = GetStat(Stat.StatType.AMMO);
            }
        }
    }

    private void LaunchPendingMissiles(float deltaTime)
    {
        missileTimer -= deltaTime;
        if (pendingMissiles <= 0 || missileTimer > 0f)
            return;

        // A barrage spreads over the target and the enemies closest to it
        List<Enemy> enemies = strategy.GetEnemiesInRange();
        if (enemies.Count == 0)
            return;

        enemies.Sort((a, b) =>
            Vector3.Distance(a.transform.position, target.transform.position)
            .CompareTo(Vector3.Distance(b.transform.position, target.transform.position)));

        int missileIndex = strategy.missilesPerRun - pendingMissiles;
        strategy.FireOrdnance(ordnancePool, ordnanceBay, enemies[missileIndex % enemies.Count]);

        pendingMissiles--;
        missileTimer = missileLaunchInterval;
    }

    private void DropBombs(float deltaTime)
    {
        bombTimer -= deltaTime;
        if (bombTimer > 0f)
            return;

        Enemy closest = null;
        float closestDistance = bombDropRadius;
        foreach (Enemy enemy in strategy.GetEnemiesInRange())
        {
            float distance = Vector3.Distance(enemy.transform.position, transform.position);
            if (distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        if (closest == null)
            return;

        strategy.FireOrdnance(ordnancePool, ordnanceBay, closest);
        bombTimer = bombDropInterval;
    }

    private void SetMissilePodsVisible(bool visible)
    {
        missilePodsVisible = visible;
        foreach (GameObject pod in missilePodVisuals)
        {
            if (pod != null)
                pod.SetActive(visible);
        }
    }

    #endregion
}
