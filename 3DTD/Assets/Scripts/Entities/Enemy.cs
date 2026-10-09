using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Enemy : MonoBehaviour
{
    // Seconds without taking damage before a regenerating enemy starts to heal, and seconds per regrown layer
    public const float RegenDelay = 3f;
    public const float RegenInterval = 2.5f;
    // An armored enemy always takes at least this share of a hit
    public const float MinArmorDamageShare = 0.4f;
    // How fast an enemy a gravity well let go of drifts back onto its path (units per second)
    public const float PullRelaxSpeed = 2f;

    [SerializeField] public EnemyData data;
    [SerializeField] private float currentHealth;
    [SerializeField] public Waypoints waypoints;
    private int waypointIndex = 0;

    [SerializeField] private float distanceTraveled = 0f;
    public float DistanceTraveled => distanceTraveled;

    // Where this frame's Tick started and the last waypoint corner passed during it (HasTickCorner), so contact
    // tests can rebuild the exact path walked this frame instead of a straight line
    public Vector3 TickStartPosition { get; private set; }
    public Vector3 TickCorner { get; private set; }
    public bool HasTickCorner { get; private set; }
    private bool reachedPathEnd;

    // Gravity wells (Bullet Dispenser) drag the enemy off its path: it walks the path at pathPosition while its
    // body sits pullOffset away from it. The strongest pull of the frame wins; without one it drifts back.
    private Vector3 pathPosition;
    private Vector3 pullOffset;
    private Vector3 pullGoal;
    private float pullSpeed;
    private bool pulled;
    public Vector3 PullOffset => pullOffset;

    [SerializeField] private float speedRandomRotation = 2f;
    [SerializeField] private Shape currentShape;
    [SerializeField] private EnemyColor currentColor;
    [SerializeField] private GameObject[] allPossibleShapes;
    [SerializeField] private EnemyData[] allPossibleEnemyData;

    [SerializeField] private float movementSpeedModifierPercent = 1.0f;
    [SerializeField] private bool specialEnemy = false;
    [SerializeField] private bool canTakeDamage = true;

    [Header("Traits")]
    [Tooltip("Transparent material used for the armor, shield and regeneration overlays")]
    [SerializeField] private Material traitMaterial;
    [SerializeField] private EnemyTrait traits = EnemyTrait.None;

    private float MovementSpeed
    {
        get { return data.MovementSpeed * movementSpeedModifierPercent * speedMultiplier * (1f - slowStrength); }
    }

    public Shape CurrentShape
    {
        get { return currentShape; }
        set { SetLayer(value, currentColor); }
    }

    public EnemyColor CurrentColor
    {
        get { return currentColor; }
        set { SetLayer(currentShape, value); }
    }

    public float CurrentHealth => currentHealth;
    public int Id => ((int)CurrentShape * 10) + (int)CurrentColor;
    public EnemyTrait Traits => traits;
    public int ShieldHits => shieldHits;
    // False once the enemy died or leaked; pooled enemies stay referenced, so check this instead of null
    public bool IsAlive => isAlive;
    // Changes every time a pooled enemy starts a new life, so stale references to the old life can be told apart
    public int SpawnSerial => spawnSerial;
    // Index in Spawner.EnemiesAlive while the enemy is alive (-1 otherwise); maintained by the Spawner
    public int AliveIndex { get; set; } = -1;
    // Slot in ProjectileSystem's EnemyRegistry while alive (-1 otherwise)
    public int RegistrySlot => registrySlot;

    // Direction the enemy is currently moving along its path (zero once it reached the last waypoint)
    public Vector3 PathDirection
    {
        get
        {
            if (waypoints == null || waypoints.WaypointsArray.Count == 0)
                return Vector3.zero;

            Vector3 toWaypoint = waypoints.WaypointsArray[waypointIndex].position - pathPosition;
            return toWaypoint.sqrMagnitude > 0.0001f ? toWaypoint.normalized : Vector3.zero;
        }
    }

    // Current velocity, used by towers that lead their shots
    public Vector3 Velocity => PathDirection * MovementSpeed;

    public event Action<float> OnHealthUpdated;
    public event Action<GameObject> OnDeath;
    public event Action<Shape, EnemyColor> OnShapeOrColorChanged;
    public event Action<Shape> OnShapeChanged;

    private Quaternion randomRotation;
    private bool animationCanPlay = false;
    private bool isAlive = false;
    private bool initialized = false;
    private Tower lastTowerDamagedFrom;

    private static int nextSpawnSerial;
    private int spawnSerial;
    private int registrySlot = -1;
    private float hitRadius = -1f;
    private int spawnId;
    private int shieldHits;
    // The shape child that is currently shown (several ids can share one child, e.g. the boss)
    private GameObject activeShape;
    private bool shapesInitialized;
    private float timeSinceDamaged;
    private float regenTimer;
    private float speedMultiplier = 1f;
    private float layerHealthMultiplier = 1f;
    private float bossHealthMultiplier = 1f;
    private float armorBonus = 0f;

    private float slowStrength;
    private float slowTimer;

    private GameObject armorVisual;
    private GameObject shieldVisual;
    private GameObject regenVisual;
    private MaterialPropertyBlock traitPropertyBlock;

    private void Start()
    {
        // Enemies placed in a scene by hand; spawned enemies are set up through Initialize
        if (!initialized)
            Initialize(data, waypoints, traits);
    }

    // Prepares a fresh or pooled enemy for a new life
    public void Initialize(EnemyData enemyData, Waypoints path, EnemyTrait enemyTraits)
    {
        initialized = true;
        spawnSerial = ++nextSpawnSerial;
        data = enemyData;
        waypoints = path;
        traits = enemyTraits;
        waypointIndex = 0;
        distanceTraveled = 0f;
        reachedPathEnd = false;
        HasTickCorner = false;
        TickStartPosition = transform.position;
        pathPosition = transform.position;
        pullOffset = Vector3.zero;
        pulled = false;
        lastTowerDamagedFrom = null;
        slowStrength = 0f;
        slowTimer = 0f;
        timeSinceDamaged = 0f;
        regenTimer = 0f;
        randomRotation = Random.rotation;
        specialEnemy = data.StartShape == Shape.BOSS;

        DifficultyProfile profile = GameManager.Instance != null ? GameManager.Instance.Profile : null;
        speedMultiplier = profile != null ? profile.enemySpeedMultiplier : 1f;
        layerHealthMultiplier = profile != null ? profile.layerHealthMultiplier : 1f;
        bossHealthMultiplier = profile != null ? profile.bossHealthMultiplier : 1f;
        armorBonus = profile != null ? profile.armorBonus : 0f;

        int shieldBonus = profile != null ? profile.shieldBonus : 0;
        shieldHits = HasTrait(EnemyTrait.Shielded) ? BaseShieldHits(data.StartShape) + shieldBonus : 0;

        isAlive = true;
        animationCanPlay = false;   // no death animation for the layer the enemy spawns with
        SetLayer(data.StartShape, data.StartColor);
        spawnId = Id;
        animationCanPlay = true;

        UpdateTraitVisuals();
        Register();
    }

    // Projectile hit tests and blasts find enemies through the registry instead of physics
    private void Register()
    {
        if (hitRadius < 0f)
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            Vector3 scale = transform.lossyScale;
            hitRadius = sphere != null ? sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) : 0.375f;
        }
        ProjectileSystem system = ProjectileSystem.Instance;
        if (system == null)
            return;
        if (registrySlot >= 0)
            system.Enemies.Unregister(registrySlot);
        registrySlot = system.Enemies.Register(this, hitRadius);
    }

    private void Unregister()
    {
        if (registrySlot >= 0 && ProjectileSystem.Existing != null)
            ProjectileSystem.Existing.Enemies.Unregister(registrySlot);
        registrySlot = -1;
    }

    // Moves along the path. The Spawner also calls this to place enemies that were due earlier in the frame.
    // Movement left over at a waypoint carries on towards the next one, so corners don't cost distance and the
    // enemy covers the same path in the same game time at any frame rate or game speed.
    public void Move(float deltaTime)
    {
        pathPosition = Advance(pathPosition, deltaTime);
        transform.position = pathPosition + pullOffset;
        LeakIfPathEnded();
    }

    private Vector3 Advance(Vector3 position, float deltaTime)
    {
        if (waypoints == null || waypoints.WaypointsArray.Count == 0)
            return position;

        List<Transform> path = waypoints.WaypointsArray;
        float remaining = MovementSpeed * deltaTime;
        for (int guard = 0; guard <= path.Count && remaining > 0f; guard++)
        {
            Vector3 waypoint = path[waypointIndex].position;
            Vector3 toWaypoint = waypoint - position;
            float distance = toWaypoint.magnitude;
            if (distance > remaining)
            {
                position += toWaypoint * (remaining / distance);
                distanceTraveled += remaining;
                break;
            }

            position = waypoint;
            distanceTraveled += distance;
            remaining -= distance;
            TickCorner = waypoint + pullOffset;
            HasTickCorner = true;
            if (waypointIndex >= path.Count - 1)
            {
                reachedPathEnd = true;
                break;
            }
            waypointIndex++;
        }
        return position;
    }

    // Backstop for the End trigger: an enemy that walked the whole path leaks even if a long frame carried it past
    // the trigger without an overlap. End's handler ignores enemies the trigger already took.
    private void LeakIfPathEnded()
    {
        if (reachedPathEnd && isAlive && End.Instance != null)
            End.Instance.ReportExit(this);
    }

    public bool HasTrait(EnemyTrait trait)
    {
        return (traits & trait) != 0;
    }

    // Once per frame for every alive enemy, called by the Spawner (one loop instead of an Update per enemy).
    // Position and rotation are written together: each transform write also syncs the enemy's physics body.
    public void Tick(float deltaTime)
    {
        if (!isAlive)
            return;

        transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
        TickStartPosition = position;
        HasTickCorner = false;
        if (Mathf.Abs(Quaternion.Dot(rotation, randomRotation)) >= 0.990f)
            randomRotation = Random.rotation;
        rotation = Quaternion.Slerp(rotation, randomRotation, speedRandomRotation * deltaTime);
        pathPosition = Advance(pathPosition, deltaTime);
        UpdatePull(deltaTime);
        transform.SetPositionAndRotation(pathPosition + pullOffset, rotation);
        LeakIfPathEnded();
        if (!isAlive)
            return;

        if (slowTimer > 0f)
        {
            slowTimer -= deltaTime;
            if (slowTimer <= 0f)
                slowStrength = 0f;
        }

        if (HasTrait(EnemyTrait.Regenerating))
            UpdateRegeneration(deltaTime);
    }

    #region Damage

    private static readonly Unity.Profiling.ProfilerMarker DamageMarker = new Unity.Profiling.ProfilerMarker("3DTD.Enemy.TakeDamage");

    public void TakeDamage(float damage, DamageType damageType, Tower tower)
    {
        using var scope = DamageMarker.Auto();
        if (!isAlive || !canTakeDamage || damage <= 0f)
            return;

        lastTowerDamagedFrom = tower;
        timeSinceDamaged = 0f;
        regenTimer = 0f;

        // A shield swallows whole hits, no matter how hard they are
        if (shieldHits > 0)
        {
            shieldHits--;
            if (shieldHits == 0)
                UpdateTraitVisuals();
            return;
        }

        damage = ApplyArmor(damage, damageType);
        HandleDamageTaken(damage);
    }

    // Armor reduces PROJECTILE hits by a flat amount, EXPLOSIVE hits by three quarters of it, MAGIC ignores it
    private float ApplyArmor(float damage, DamageType damageType)
    {
        if (!HasTrait(EnemyTrait.Armored))
            return damage;

        float armor = ArmorValue(currentShape) + armorBonus;
        switch (damageType)
        {
            case DamageType.PROJECTILE:
                break;
            case DamageType.EXPLOSIVE:
                armor *= 0.75f;
                break;
            default:
                armor = 0f;
                break;
        }

        return Mathf.Max(damage - armor, damage * MinArmorDamageShare);
    }

    public static float ArmorValue(Shape shape)
    {
        // Difficulty profiles add DifficultyProfile.armorBonus on top
        return shape == Shape.BOSS ? 2f : 1f;
    }

    public static int BaseShieldHits(Shape shape)
    {
        return shape == Shape.BOSS ? 8 : 2 + (int)shape;
    }

    // Damage carries over from one layer to the next until it is used up or the enemy is gone
    private void HandleDamageTaken(float damage)
    {
        float remaining = damage;
        float dealt = 0f;

        while (remaining > 0f && isAlive)
        {
            if (remaining < currentHealth)
            {
                currentHealth -= remaining;
                dealt += remaining;
                break;
            }

            remaining -= currentHealth;
            dealt += currentHealth;
            currentHealth = 0f;
            PopLayer();
        }

        if (lastTowerDamagedFrom != null)
            lastTowerDamagedFrom.HandleDamageDealt(dealt);

        OnHealthUpdated?.Invoke(currentHealth);
    }

    private static readonly Unity.Profiling.ProfilerMarker PopMarker = new Unity.Profiling.ProfilerMarker("3DTD.Enemy.PopLayer");
    private static readonly Unity.Profiling.ProfilerMarker ShapeMarker = new Unity.Profiling.ProfilerMarker("3DTD.Enemy.ShowShape");

    private void PopLayer()
    {
        using var scope = PopMarker.Auto();
        PerfCounters.Pops++;
        if (EnemyPopSoundSpawn.Instance != null)
            EnemyPopSoundSpawn.Instance.PlayPopSound();

        if (specialEnemy)
        {
            HandleDeathAnimation(0);
            Die();
            return;
        }

        GameManager.Instance.AddIncome(1f);
        int id = Id;
        HandleDeathAnimation(id);

        if (id == 0)
        {
            Die();
            return;
        }

        if (currentColor == EnemyColor.RED)
            SetLayer(currentShape - 1, EnemyColor.BLACK);
        else
            SetLayer(currentShape, currentColor - 1);
    }

    #endregion

    #region Layers

    // Switches the visible shape and the layer data, and refills the layer's health
    private void SetLayer(Shape shape, EnemyColor color)
    {
        bool shapeChanged = shape != currentShape;
        currentShape = shape;
        currentColor = color;

        int id = Id;
        if (allPossibleEnemyData != null && id < allPossibleEnemyData.Length && allPossibleEnemyData[id] != null)
            data = allPossibleEnemyData[id];

        currentHealth = LayerHealth(data);

        ShowShape(id);

        if (shapeChanged)
        {
            OnShapeChanged?.Invoke(currentShape);
            if (id != 0)
                HandleDeathAnimation(id);
        }
        OnShapeOrColorChanged?.Invoke(currentShape, currentColor);
    }

    // Only the outgoing and the incoming shape change, instead of toggling all 51 children on every pop.
    // Compared by object, not index: the boss slot reuses the Icosahedron Black child.
    private void ShowShape(int id)
    {
        using var scope = ShapeMarker.Auto();
        if (allPossibleShapes == null)
            return;

        GameObject shape = id >= 0 && id < allPossibleShapes.Length ? allPossibleShapes[id] : null;
        if (!shapesInitialized)
        {
            shapesInitialized = true;
            foreach (GameObject candidate in allPossibleShapes)
            {
                if (candidate != null && candidate != shape)
                    candidate.SetActive(false);
            }
        }
        else if (activeShape != null && activeShape != shape)
        {
            activeShape.SetActive(false);
        }

        if (shape != null && !shape.activeSelf)
            shape.SetActive(true);
        activeShape = shape;
    }

    private float LayerHealth(EnemyData layerData)
    {
        float health = layerData.Health;
        if (specialEnemy)
            return health * bossHealthMultiplier;

        // 1-HP layers stay at 1 so difficulty never doubles the hits a tetrahedron needs
        return health >= 5f ? health * layerHealthMultiplier : health;
    }

    private void UpdateRegeneration(float deltaTime)
    {
        timeSinceDamaged += deltaTime;
        if (timeSinceDamaged < RegenDelay || Id >= spawnId)
            return;

        regenTimer += deltaTime;
        if (regenTimer < RegenInterval)
            return;

        regenTimer -= RegenInterval;
        if (currentColor == EnemyColor.BLACK)
            SetLayer(currentShape + 1, EnemyColor.RED);
        else
            SetLayer(currentShape, currentColor + 1);
    }

    #endregion

    #region Death and pooling

    private void HandleDeathAnimation(int id)
    {
        if (animationCanPlay && allPossibleShapes != null && id < allPossibleShapes.Length)
            EnemyShape.SpawnDeathEffect(allPossibleShapes[id], id, transform.position, transform.rotation);
    }

    // Killed by towers
    private void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;
        if (lastTowerDamagedFrom != null)
            lastTowerDamagedFrom.HandleKill();
        OnDeath?.Invoke(this.gameObject);
        Release();
    }

    // Reached the exit; no money, no pop
    public void DestroyWholeEnemy()
    {
        if (!isAlive)
            return;

        isAlive = false;
        OnDeath?.Invoke(this.gameObject);
        Release();
    }

    private void Release()
    {
        Unregister();
        // Subscribers re-register on the next life
        OnDeath = null;
        OnHealthUpdated = null;
        OnShapeOrColorChanged = null;
        OnShapeChanged = null;

        if (Spawner.Instance != null)
            Spawner.Instance.ReleaseEnemy(this);
        else
            Destroy(this.gameObject);
    }

    #endregion

    #region Pull

    // A gravity well at wellPoint drags the enemy towards it at speed (units per second), until it is holdRadius
    // from the well or maxOffset off its path. Bosses are too heavy to move.
    public void Pull(Vector3 wellPoint, float holdRadius, float maxOffset, float speed)
    {
        if (!isAlive || specialEnemy || maxOffset <= 0f || speed <= 0f)
            return;

        Vector3 toWell = wellPoint - pathPosition;
        float distance = toWell.magnitude;
        float reach = Mathf.Min(maxOffset, Mathf.Max(0f, distance - holdRadius));
        Vector3 goal = distance > 0.0001f ? toWell * (reach / distance) : Vector3.zero;
        if (!pulled || goal.sqrMagnitude > pullGoal.sqrMagnitude)
        {
            pullGoal = goal;
            pullSpeed = speed;
        }
        pulled = true;
    }

    // The pulls since the last tick move the body towards their goal; without one it drifts back to the path
    private void UpdatePull(float deltaTime)
    {
        if (!pulled && pullOffset == Vector3.zero)
            return;
        Vector3 goal = pulled ? pullGoal : Vector3.zero;
        float speed = pulled ? pullSpeed : PullRelaxSpeed;
        pullOffset = Vector3.MoveTowards(pullOffset, goal, speed * deltaTime);
        pulled = false;
    }

    #endregion

    #region Slow

    // percentageAmount < 0 slows (e.g. -0.5 = half speed). Slows don't stack: the strongest active one wins.
    public void ApplySlowness(float percentageAmount, float durationInSeconds)
    {
        float strength = Mathf.Clamp(-percentageAmount, 0f, 0.9f);
        if (strength <= 0f)
            return;

        if (strength > slowStrength || slowTimer <= 0f)
        {
            slowStrength = strength;
            slowTimer = durationInSeconds;
        }
        else if (Mathf.Approximately(strength, slowStrength))
        {
            slowTimer = Mathf.Max(slowTimer, durationInSeconds);
        }
    }

    #endregion

    #region Trait visuals

    private void UpdateTraitVisuals()
    {
        if (traitMaterial == null)
            return;

        SetVisual(ref armorVisual, HasTrait(EnemyTrait.Armored), 1.25f, new Color(0.55f, 0.6f, 0.65f, 0.55f));
        SetVisual(ref shieldVisual, HasTrait(EnemyTrait.Shielded) && shieldHits > 0, 1.7f, new Color(0.2f, 0.8f, 1f, 0.3f));
        SetVisual(ref regenVisual, HasTrait(EnemyTrait.Regenerating), 1.45f, new Color(0.2f, 1f, 0.35f, 0.25f));
    }

    private void SetVisual(ref GameObject visual, bool active, float scale, Color color)
    {
        if (!active)
        {
            if (visual != null)
                visual.SetActive(false);
            return;
        }

        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // The overlay must never block projectiles or trigger tower ranges
            DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Trait Overlay";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * scale;

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = traitMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (traitPropertyBlock == null)
                traitPropertyBlock = new MaterialPropertyBlock();
            traitPropertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(traitPropertyBlock);
        }

        visual.SetActive(true);
    }

    #endregion
}

[Flags]
public enum EnemyTrait
{
    None         = 0,
    Armored      = 1 << 0,
    Shielded     = 1 << 1,
    Regenerating = 1 << 2,
}

public enum Shape
{
    TETRAHEDRON     = 0,
    CUBE            = 1,
    OCTAHEDRON      = 2,
    DODECAHEDRON    = 3,
    ICOSAHEDRON     = 4,
    BOSS            = 5,
}

public enum EnemyColor
{
    RED     = 0,
    ORANGE  = 1,
    YELLOW  = 2,
    GREEN   = 3,
    CYAN    = 4,
    BLUE    = 5,
    PURPLE  = 6,
    PINK    = 7,
    WHITE   = 8,
    BLACK   = 9
}
