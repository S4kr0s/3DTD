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

    [SerializeField] public EnemyData data;
    [SerializeField] private float currentHealth;
    [SerializeField] public Waypoints waypoints;
    private int waypointIndex = 0;

    [SerializeField] private float distanceTraveled = 0f;
    public float DistanceTraveled => distanceTraveled;

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

    // Direction the enemy is currently moving along its path (zero once it reached the last waypoint)
    public Vector3 PathDirection
    {
        get
        {
            if (waypoints == null || waypoints.WaypointsArray.Count == 0)
                return Vector3.zero;

            Vector3 toWaypoint = waypoints.WaypointsArray[waypointIndex].position - transform.position;
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

    private int spawnId;
    private int shieldHits;
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
        data = enemyData;
        waypoints = path;
        traits = enemyTraits;
        waypointIndex = 0;
        distanceTraveled = 0f;
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
    }

    // Moves along the path. The Spawner also calls this to place enemies that were due earlier in the frame.
    public void Move(float deltaTime)
    {
        if (waypoints == null || waypoints.WaypointsArray.Count == 0)
            return;

        Vector3 waypoint = waypoints.WaypointsArray[waypointIndex].position;
        Vector3 moveTowards = Vector3.MoveTowards(transform.position, waypoint, MovementSpeed * deltaTime);
        distanceTraveled += (moveTowards - transform.position).magnitude;
        transform.position = moveTowards;

        if ((transform.position - waypoint).sqrMagnitude < 0.01f && waypointIndex < waypoints.WaypointsArray.Count - 1)
            waypointIndex++;
    }

    public bool HasTrait(EnemyTrait trait)
    {
        return (traits & trait) != 0;
    }

    private void Update()
    {
        if (!isAlive)
            return;

        float deltaTime = Time.deltaTime;

        float abs = Mathf.Abs(Quaternion.Dot(transform.rotation, randomRotation));
        if (abs >= 0.990f)
            randomRotation = Random.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, randomRotation, speedRandomRotation * deltaTime);

        Move(deltaTime);

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

    public void TakeDamage(float damage, DamageType damageType, Tower tower)
    {
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

    private void PopLayer()
    {
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

        if (allPossibleShapes != null)
        {
            for (int i = 0; i < allPossibleShapes.Length; i++)
            {
                if (allPossibleShapes[i] != null)
                    allPossibleShapes[i].SetActive(i == id);
            }
        }

        if (shapeChanged)
        {
            OnShapeChanged?.Invoke(currentShape);
            if (id != 0)
                HandleDeathAnimation(id);
        }
        OnShapeOrColorChanged?.Invoke(currentShape, currentColor);
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
        // Subscribers re-register on the next life
        OnDeath = null;
        OnHealthUpdated = null;
        OnShapeOrColorChanged = null;
        OnShapeChanged = null;

        if (Spawner.Instance != null)
            Spawner.Instance.ReleaseEnemy(this.gameObject);
        else
            Destroy(this.gameObject);
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
