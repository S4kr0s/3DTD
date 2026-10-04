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
    [SerializeField] private int cannonPoolPerStarfighter = 24;
    [SerializeField] private int ordnancePoolPerStarfighter = 12;
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
        starfighter.Setup(this, launchPoint, cannonPoolPerStarfighter, ordnancePoolPerStarfighter, cannonProjectile, ordnanceProjectile);
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

        Enemy untargeted = tower.Targetter.GetAllEnemiesInRadius()
            .Where(enemy => !IsTargetedByOtherStarfighter(enemy, requester))
            .OrderBy(enemy => Vector3.Distance(enemy.transform.position, requester.transform.position))
            .FirstOrDefault();

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

    public void FireCannon(ProjectilePoolManager pool, Action<GameObject> onProjectileDeath, Transform muzzle, Enemy target)
    {
        GameObject _projectile = pool.GetPooledProjectile();
        if (_projectile == null)
            return;

        _projectile.SetActive(false);
        _projectile.transform.position = muzzle.position;
        _projectile.transform.rotation = muzzle.rotation;
        _projectile.transform.localScale = Vector3.one * tower.StatsManager.GetStatValue(Stat.StatType.SIZE);

        Projectile projectileComponent = _projectile.GetComponent<Projectile>();
        projectileComponent.Target = target.gameObject;
        projectileComponent.lifetime = tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME);
        projectileComponent.damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);
        projectileComponent.penetration = ((int)tower.StatsManager.GetStatValue(Stat.StatType.PIERCING));
        projectileComponent.maxSpeed = tower.StatsManager.GetStatValue(Stat.StatType.SPEED);
        projectileComponent.accuracy = tower.StatsManager.GetStatValue(Stat.StatType.ACCURACY);
        projectileComponent.tower = tower;
        if (projectileComponent.Collider != null)
            projectileComponent.Collider.enabled = true;
        projectileComponent.OnProjectileDeath += onProjectileDeath;
        _projectile.SetActive(true);

        _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
    }

    // Missiles and bombs: slower, homing, exploding projectiles
    public void FireOrdnance(ProjectilePoolManager pool, Action<GameObject> onProjectileDeath, Transform muzzle, Enemy target)
    {
        GameObject _projectile = pool.GetPooledProjectile();
        if (_projectile == null)
            return;

        _projectile.SetActive(false);
        _projectile.transform.position = muzzle.position;
        _projectile.transform.rotation = muzzle.rotation;
        _projectile.transform.localScale = Vector3.one * ordnanceSize;

        ProjectileBomb projectileComponent = _projectile.GetComponent<ProjectileBomb>();
        projectileComponent.Target = target.gameObject;
        projectileComponent.aimAtTarget = true;
        projectileComponent.doClustering = false;
        projectileComponent.lifetime = ordnanceLifetime;
        projectileComponent.damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE) * ordnanceDamageMultiplier;
        projectileComponent.penetration = 1;
        projectileComponent.maxSpeed = ordnanceSpeed;
        projectileComponent.accuracy = 1f;
        projectileComponent.radius = tower.StatsManager.GetStatValue(Stat.StatType.RADIUS);
        projectileComponent.tower = tower;
        if (projectileComponent.Collider != null)
            projectileComponent.Collider.enabled = true;
        projectileComponent.OnProjectileDeath += onProjectileDeath;
        _projectile.SetActive(true);

        _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
    }

    #endregion

    public override bool CanShoot(GameObject enemy)
    {
        return enemy != null
            && enemy.TryGetComponent<Enemy>(out Enemy enemyComponent)
            && tower.Targetter.GetAllEnemiesInRadius().Contains(enemyComponent);
    }
}
