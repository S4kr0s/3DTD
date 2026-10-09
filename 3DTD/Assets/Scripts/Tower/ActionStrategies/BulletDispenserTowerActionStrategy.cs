using System.Collections.Generic;
using UnityEngine;

// The Bullet Dispenser never aims: every enabled barrel of its barrel ball fires a needle along its own direction
// whenever an enemy is in range. Its upgrades work with the space around it:
// - Ricochet (DispenserRicochetUpgrade): needles rebound off the inside of a dome a quarter beyond the range and
//   off the ground plane,
//   gain damage per rebound and can turn towards an enemy on every rebound.
// - Gravity well (DispenserGravityUpgrade): enemies in range are slowed, then also dragged off their path towards
//   the barrel ball; the singularity periodically collapses them all into a tight orbit around it.
public class BulletDispenserTowerActionStrategy : ActionStrategy
{
    private const float SingularityEffectLifetime = 3f;

    [SerializeField] private GameObject projectile;
    [Tooltip("Spark where a needle rebounds (fallback of VisualSlot.Bounce)")]
    [SerializeField] private GameObject bounceEffect;
    [Tooltip("Implosion when the singularity collapses (fallback of VisualSlot.Singularity)")]
    [SerializeField] private GameObject singularityEffect;

    [Tooltip("Centre of the barrel ball: the gravity well pulls towards it")]
    [SerializeField] private Transform core;
    [Tooltip("Gravity rings around the barrel ball, spun on their own axes (shown by the gravity path's modules)")]
    [SerializeField] private Transform[] gravityRings = new Transform[0];
    [SerializeField] private float ringSpinSpeed = 90f;
    [Tooltip("Shimmer of the ricochet dome, shown while the tower is selected and its needles rebound")]
    [SerializeField] private Renderer ricochetDome;
    [SerializeField] private float domeFadeSpeed = 4f;

    [Header("Ricochet (set by DispenserRicochetUpgrade)")]
    public int ricochetBounces;
    public float ricochetDamage;
    public bool ricochetSeek;

    [Header("Gravity well (set by DispenserGravityUpgrade)")]
    [Tooltip("How far enemies in range are dragged off their path (0 = no well)")]
    public float gravityPull;
    [Tooltip("Units per second the well drags enemies")]
    public float gravityPullSpeed;
    [Tooltip("Slow of the enemies in the well (0.3 = 30% slower)")]
    public float gravitySlow;
    [Tooltip("Seconds between collapses of the singularity (0 = none)")]
    public float singularityInterval;
    [Tooltip("Seconds a collapse holds the enemies")]
    public float singularityDuration;

    // Needles rebound a bit beyond the range, so they still reach enemies just outside it before they turn back
    public const float RicochetDomeScale = 1.25f;
    // Enemies stop this far from the barrel ball's centre (just outside the barrels)
    public const float WellHoldRadius = 0.55f;
    public const float SingularityHoldRadius = 0.45f;
    public const float SingularityPullSpeed = 8f;
    // A slow is refreshed every frame while the enemy is in the well and lasts this long after it left
    private const float WellSlowDuration = 0.25f;

    private FireCycle fireCycle;
    public override FireCycle Cycle => fireCycle;
    private Tower tower;
    private Enemy target;
    private VisualRef projectileVisual;
    private VisualRef muzzleVisual;
    private VisualRef flightVisual;
    private VisualRef impactVisual;
    private VisualRef bounceVisual;
    private VisualRef singularityVisual;
    private float singularityTimer;
    private float collapseLeft;
    private bool selected;
    private float domeIntensity = -1f;
    private MaterialPropertyBlock domeProperties;
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    // Seconds until the next collapse and the one in progress (tests, telemetry)
    public float SingularityTimer => singularityTimer;
    public bool Collapsing => collapseLeft > 0f;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(0f);
        if (ricochetDome != null)
        {
            // Scaled with the targetter (RANGE); the rebounds happen a quarter beyond it
            ricochetDome.transform.localScale = Vector3.one * RicochetDomeScale;
            ricochetDome.enabled = false;
        }
    }

    private void OnEnable()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChange;
    }

    private void OnDisable()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChange;
    }

    private void HandleSelectionChange(Selectable previous, Selectable current)
    {
        selected = current != null && tower != null && current.gameObject == tower.gameObject;
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        int volleys = fireCycle.Tick(Time.deltaTime, target != null, tower.StatsManager.GetFireInterval(), 0f, 0f);
        for (int i = 0; i < volleys; i++)
            FireBullets(fireCycle.VolleyAge(i));

        UpdateGravityWell(Time.deltaTime);
        SpinRings(Time.deltaTime);
        UpdateDome(Time.unscaledDeltaTime);
    }

    // Fades the dome shimmer in while the tower is selected and has rebounds, out otherwise (also while paused)
    private void UpdateDome(float deltaTime)
    {
        if (ricochetDome == null)
            return;
        float goal = selected && ricochetBounces > 0 ? 1f : 0f;
        if (domeIntensity == goal)
            return;
        domeIntensity = Mathf.MoveTowards(Mathf.Max(0f, domeIntensity), goal, domeFadeSpeed * deltaTime);
        if (domeProperties == null)
            domeProperties = new MaterialPropertyBlock();
        domeProperties.SetFloat(IntensityId, domeIntensity);
        ricochetDome.SetPropertyBlock(domeProperties);
        ricochetDome.enabled = domeIntensity > 0f;
    }

    private void FireBullets(float age)
    {
        GameObject prefab = projectileVisual.Get(tower, VisualSlot.Projectile, projectile);
        StatsManager stats = tower.StatsManager;
        ProjectileSystem.Shot shot = new ProjectileSystem.Shot
        {
            Scale = stats.GetStatValue(Stat.StatType.SIZE),
            Damage = stats.GetStatValue(Stat.StatType.DAMAGE),
            Speed = stats.GetStatValue(Stat.StatType.SPEED),
            Lifetime = stats.GetStatValue(Stat.StatType.LIFETIME),
            Accuracy = stats.GetStatValue(Stat.StatType.ACCURACY),
            Pierce = (int)stats.GetStatValue(Stat.StatType.PIERCING),
            Target = target,
            Tower = tower,
            Age = age,
            MuzzleEffect = muzzleVisual.Get(tower, VisualSlot.Muzzle, null),
            FlightEffect = flightVisual.Get(tower, VisualSlot.Flight, null),
            ImpactEffect = impactVisual.Get(tower, VisualSlot.Impact, null),
        };
        if (ricochetBounces > 0)
        {
            shot.Dome = RangeDome();
            shot.Dome.Radius *= RicochetDomeScale;
            shot.Bounces = ricochetBounces;
            shot.BounceDamage = ricochetDamage;
            shot.BounceSeek = ricochetSeek;
            shot.BounceEffect = bounceVisual.Get(tower, VisualSlot.Bounce, bounceEffect);
        }

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (!shootingPoint.IsReferenceEnabled)
                continue;

            Transform barrel = shootingPoint.transform;
            shot.Position = barrel.position;
            shot.Rotation = barrel.rotation;
            ProjectileSystem.Fire(prefab, shot);
        }
    }

    // The targetting half sphere: its centre, the face normal and the range radius
    public ProjectileSystem.Dome RangeDome()
    {
        return new ProjectileSystem.Dome
        {
            Center = tower.Targetter.transform.position,
            Up = tower.transform.forward,
            Radius = tower.GetWorldRangeRadius(tower.StatsManager.GetStatValue(Stat.StatType.RANGE)),
        };
    }

    public Vector3 WellPoint => core != null ? core.position : tower.transform.position;

    // Drags every enemy in range towards the barrel ball; the singularity charges while enemies are in range and
    // then holds all of them close for a moment
    private void UpdateGravityWell(float deltaTime)
    {
        if (gravityPull <= 0f && gravitySlow <= 0f)
            return;

        List<Enemy> enemies = tower.Targetter.GetAllEnemiesInRadius();
        if (singularityInterval > 0f)
        {
            if (collapseLeft > 0f)
            {
                collapseLeft -= deltaTime;
            }
            else if (enemies.Count > 0)
            {
                singularityTimer += deltaTime;
                if (singularityTimer >= singularityInterval)
                {
                    singularityTimer -= singularityInterval;
                    collapseLeft = singularityDuration;
                    GameObject effect = singularityVisual.Get(tower, VisualSlot.Singularity, singularityEffect);
                    EffectPlayer.Play(effect, WellPoint, tower.transform.rotation, 1f, SingularityEffectLifetime);
                }
            }
        }

        bool collapsing = collapseLeft > 0f;
        Vector3 well = WellPoint;
        // A collapse reaches every enemy in range, wherever it is on its path
        float reach = collapsing ? 2f * tower.GetWorldRangeRadius(tower.StatsManager.GetStatValue(Stat.StatType.RANGE)) : gravityPull;
        float hold = collapsing ? SingularityHoldRadius : WellHoldRadius;
        float speed = collapsing ? SingularityPullSpeed : gravityPullSpeed;
        bool pulls = gravityPull > 0f || collapsing;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (pulls)
                enemy.Pull(well, hold, reach, speed);
            if (gravitySlow > 0f)
                enemy.ApplySlowness(-gravitySlow, WellSlowDuration);
        }
    }

    private void SpinRings(float deltaTime)
    {
        if (gravityRings == null || gravityRings.Length == 0)
            return;
        float speed = ringSpinSpeed * (collapseLeft > 0f ? 4f : 1f) * deltaTime;
        for (int i = 0; i < gravityRings.Length; i++)
        {
            Transform ring = gravityRings[i];
            if (ring != null && ring.gameObject.activeInHierarchy)
                ring.Rotate(Vector3.forward, speed * (i % 2 == 0 ? 1f : -1.3f), Space.Self);
        }
    }

    public override bool CanShoot(GameObject enemy)
    {
        return true;
    }
}
