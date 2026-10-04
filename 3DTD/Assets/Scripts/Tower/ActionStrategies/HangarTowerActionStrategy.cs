using PolygonArsenal;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Launches Starfighters out of the hangar. The fighters fly and aim on their own,
// this strategy owns the squadron, the loadout and turns tower stats into projectiles.
//
// Stat usage:
//   AMOUNT       = number of Starfighters
//   RANGE        = radius of the sphere the fighters patrol and engage in
//   DAMAGE       = damage per cannon bolt (ordnance deals ordnanceDamageMultiplier times this)
//   FIRERATE     = seconds between cannon shots
//   AMMO         = cannon shots before the cannons have to cool down
//   RELOAD_SPEED = cool down time in seconds
//   RADIUS       = blast radius of missiles and bombs
//   ACCURACY, PIERCING, LIFETIME, SPEED, SIZE apply to cannon bolts
public class HangarTowerActionStrategy : ActionStrategy
{
    public Tower Tower => tower;
    public IReadOnlyList<Starfighter> Starfighters => starfighters;
    public Vector3 Center => tower.transform.position;
    public Vector3 TowerUp => tower.transform.forward;
    public float Range => Mathf.Max(1f, tower.StatsManager.GetStatValue(Stat.StatType.RANGE));

    [Header("Squadron")]
    [SerializeField] private Starfighter starfighterPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private int maxStarfighters = 6;
    [SerializeField] private float timeBetweenLaunches = 1.25f;

    [Header("Hangar Doors")]
    [SerializeField] private Transform[] hangarDoors;
    [SerializeField] private Vector3[] hangarDoorOpenOffsets;
    [SerializeField] private float hangarDoorSpeed = 3f;
    [SerializeField] private float hangarDoorOpenTime = 1.5f;

    [Header("Weapons")]
    [SerializeField] private GameObject cannonProjectile;
    [SerializeField] private GameObject ordnanceProjectile;
    [SerializeField] private float ordnanceDamageMultiplier = 3f;
    [SerializeField] private float ordnanceSize = 0.6f;
    [SerializeField] private float ordnanceSpeed = 7f;
    [SerializeField] private float ordnanceLifetime = 3f;

    [Header("Loadout (changed by upgrades)")]
    [SerializeField] public bool twinLinkedCannons = false;
    [SerializeField] public int missilesPerRun = 0;
    [SerializeField] public bool carpetBombing = false;
    [SerializeField] public float flightSpeedMultiplier = 1f;
    [SerializeField] public float turnRateMultiplier = 1f;

    private Tower tower;
    private readonly List<Starfighter> starfighters = new List<Starfighter>();
    private float launchCooldown = 0f;
    private float doorOpenTimer = 0f;
    private Vector3[] hangarDoorClosedPositions;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;

        if (hangarDoors != null)
            hangarDoorClosedPositions = hangarDoors.Select(door => door.localPosition).ToArray();
    }

    public override void ExecuteAction()
    {
        starfighters.RemoveAll(starfighter => starfighter == null);

        int wantedStarfighters = Mathf.Clamp(Mathf.RoundToInt(tower.StatsManager.GetStatValue(Stat.StatType.AMOUNT)), 0, maxStarfighters);

        launchCooldown -= Time.deltaTime;
        if (starfighters.Count < wantedStarfighters)
        {
            // Open the doors first, the fighter leaves once they are (mostly) open
            doorOpenTimer = hangarDoorOpenTime;
            if (launchCooldown <= 0f && AreDoorsOpen())
            {
                LaunchStarfighter();
                launchCooldown = timeBetweenLaunches;
            }
        }

        AnimateDoors();
    }

    private void LaunchStarfighter()
    {
        Starfighter starfighter = Instantiate(starfighterPrefab, launchPoint.position, launchPoint.rotation, tower.transform);
        starfighter.name = "Starfighter " + (starfighters.Count + 1);
        starfighter.Setup(this, launchPoint);
        starfighters.Add(starfighter);
    }

    private bool AreDoorsOpen()
    {
        if (hangarDoors == null || hangarDoors.Length == 0)
            return true;

        for (int i = 0; i < hangarDoors.Length; i++)
        {
            Vector3 openPosition = hangarDoorClosedPositions[i] + hangarDoorOpenOffsets[i];
            if (Vector3.Distance(hangarDoors[i].localPosition, openPosition) > hangarDoorOpenOffsets[i].magnitude * 0.2f)
                return false;
        }
        return true;
    }

    private void AnimateDoors()
    {
        if (hangarDoors == null)
            return;

        doorOpenTimer -= Time.deltaTime;
        bool open = doorOpenTimer > 0f;

        for (int i = 0; i < hangarDoors.Length; i++)
        {
            Vector3 target = hangarDoorClosedPositions[i] + (open ? hangarDoorOpenOffsets[i] : Vector3.zero);
            hangarDoors[i].localPosition = Vector3.MoveTowards(hangarDoors[i].localPosition, target, hangarDoorSpeed * Time.deltaTime);
        }
    }

    #region Targeting

    public bool HasEnemiesInRange()
    {
        return tower.Targetter.GetEnemy(tower.TargetBehaviour) != null;
    }

    // Picks the enemy for the next attack run. Squadron members spread out over different enemies when possible.
    public Enemy AcquireTarget(Starfighter requester)
    {
        Enemy preferred = tower.Targetter.GetEnemy(tower.TargetBehaviour);
        if (preferred == null || !IsTargetedByOtherStarfighter(preferred, requester))
            return preferred;

        Enemy untargeted = null;
        float bestDistance = float.MaxValue;
        Vector3 from = requester.transform.position;
        foreach (Enemy enemy in tower.Targetter.GetAllEnemiesInRadius())
        {
            if (IsTargetedByOtherStarfighter(enemy, requester))
                continue;
            float distance = (enemy.transform.position - from).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                untargeted = enemy;
            }
        }

        return untargeted != null ? untargeted : preferred;
    }

    private bool IsTargetedByOtherStarfighter(Enemy enemy, Starfighter requester)
    {
        foreach (Starfighter starfighter in starfighters)
        {
            if (starfighter != null && starfighter != requester && starfighter.Target == enemy)
                return true;
        }
        return false;
    }

    public List<Enemy> GetEnemiesInRange()
    {
        return tower.Targetter.GetAllEnemiesInRadius();
    }

    #endregion

    #region Weapons

    // Cannon bolts look at their target when they leave the muzzle, then fly straight (ProjectileBasic)
    public void FireCannon(Transform muzzle, Enemy target, float age)
    {
        StatsManager stats = tower.StatsManager;
        ProjectileSystem.Fire(cannonProjectile, new ProjectileSystem.Shot
        {
            Position = muzzle.position,
            Rotation = muzzle.rotation,
            Scale = stats.GetStatValue(Stat.StatType.SIZE),
            Damage = stats.GetStatValue(Stat.StatType.DAMAGE),
            Speed = stats.GetStatValue(Stat.StatType.SPEED),
            Lifetime = stats.GetStatValue(Stat.StatType.LIFETIME),
            Accuracy = stats.GetStatValue(Stat.StatType.ACCURACY),
            Pierce = (int)stats.GetStatValue(Stat.StatType.PIERCING),
            Target = target,
            Tower = tower,
            Age = age,
        });
    }

    // Missiles and bombs: slower, homing, exploding projectiles
    public void FireOrdnance(Transform muzzle, Enemy target)
    {
        StatsManager stats = tower.StatsManager;
        ProjectileSystem.Fire(ordnanceProjectile, new ProjectileSystem.Shot
        {
            Position = muzzle.position,
            Rotation = muzzle.rotation,
            Scale = ordnanceSize,
            Damage = stats.GetStatValue(Stat.StatType.DAMAGE) * ordnanceDamageMultiplier,
            Speed = ordnanceSpeed,
            Lifetime = ordnanceLifetime,
            Accuracy = 1f,
            Pierce = 1,
            BlastRadius = stats.GetStatValue(Stat.StatType.RADIUS),
            Target = target,
            Homing = true,
            Tower = tower,
        });
    }

    #endregion

    public override bool CanShoot(GameObject enemy)
    {
        return enemy != null && enemy.TryGetComponent<Enemy>(out Enemy enemyComponent) && IsInRange(enemyComponent);
    }

    // Asked by every fighter every frame
    public bool IsInRange(Enemy enemy)
    {
        return tower.Targetter.Contains(enemy);
    }
}
