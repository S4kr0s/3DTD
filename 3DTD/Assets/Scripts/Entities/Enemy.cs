using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Enemy : MonoBehaviour
{
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

    private float MovementSpeed
    {
        get { return data.MovementSpeed * movementSpeedModifierPercent; }
    }


    public Shape CurrentShape 
    { 
        get { return currentShape; } 

        set 
        { 
            currentShape = value;
            OnShapeChanged?.Invoke(CurrentShape);
            OnShapeOrColorChanged?.Invoke(CurrentShape, CurrentColor);
        }
    }

    public EnemyColor CurrentColor 
    { 
        get { return currentColor; } 

        set 
        {
            currentColor = value; 
            OnShapeOrColorChanged?.Invoke(CurrentShape, CurrentColor); 
        } 
    }

    public float CurrentHealth => currentHealth;
    public int Id => ((int)CurrentShape * 10) + (int)CurrentColor;

    public event Action<float> OnHealthUpdated;
    public event Action<GameObject> OnDeath;
    public event Action<Shape, EnemyColor> OnShapeOrColorChanged;
    public event Action<Shape> OnShapeChanged;

    private Quaternion randomRotation;

    private float _overflowDamage;

    private bool animationCanPlay = false;

    private Tower lastTowerDamagedFrom;

    private void Start()
    {
        OnShapeOrColorChanged += HandleShapeOrColorChanged;
        OnShapeChanged += HandleShapeChanged;
        //waypoints = GameObject.FindGameObjectWithTag("WaypointManager").GetComponent<Waypoints>();
        currentHealth = data.Health; 
        randomRotation = Random.rotation;
        CurrentShape = data.StartShape;
        CurrentColor = data.StartColor;
        animationCanPlay = true;
        if (data.StartShape == Shape.BOSS)
            specialEnemy = true;
    }

    private void UpdateAllStats(EnemyData data)
    {
        currentHealth = data.Health;
    }

    private void Update()
    {
        float abs = Mathf.Abs(Quaternion.Dot(transform.rotation, randomRotation));

        if(abs >= 0.990f)
            randomRotation = Random.rotation;

        transform.rotation = Quaternion.Slerp(transform.rotation, randomRotation, speedRandomRotation * Time.deltaTime);

        Vector3 moveTowards = Vector3.MoveTowards(transform.position, waypoints.WaypointsArray[waypointIndex].position, MovementSpeed * Time.deltaTime);
        distanceTraveled += Vector3.Distance(transform.position, moveTowards);
        transform.position = moveTowards;

        if (Vector3.Distance(transform.position, waypoints.WaypointsArray[waypointIndex].position) < 0.1f)
            if (waypointIndex < waypoints.WaypointsArray.Count - 1)
                waypointIndex++;
    }

    public void TakeDamage(float damage, DamageType damageType, Tower tower)
    {
        if (tower != null)
            lastTowerDamagedFrom = tower;
        else
            lastTowerDamagedFrom = null;

        if (canTakeDamage)
            HandleDamageTaken(damage);

        /* DamageTypes rework?
        if (damageType == DamageType.ALL || damageType == data.DamageType)
        {
            // Do armor calculations etc. here in the future.
            HandleDamageTaken(damage);
        }
        */
    }

    public void ApplySlowness(float percentageAmount, float durationInSeconds)
    {
        StartCoroutine(Slowness(percentageAmount, durationInSeconds));
    }

    private IEnumerator Slowness(float percentageAmount, float durationInSeconds)
    {
        movementSpeedModifierPercent += percentageAmount;
        yield return new WaitForSeconds(durationInSeconds);
        movementSpeedModifierPercent -= percentageAmount;
    }

    public void DestroyWholeEnemy()
    {
        OnDeath?.Invoke(this.gameObject);
        Destroy(this.gameObject);
    }

    private void HandleDamageTaken(float damage)
    {
        currentHealth -= damage;
        _overflowDamage = currentHealth * -1;

        if (lastTowerDamagedFrom != null)
            lastTowerDamagedFrom.HandleDamageDealt(damage - _overflowDamage);

        OnHealthUpdated?.Invoke(currentHealth);
        if (currentHealth <= 0)
        {
            if (!specialEnemy)
                HandleDeathOfSingleShape();
            else
                HandleDeathOfSpecialEnemy();
        }
    }

    private void HandleDeathOfSingleShape()
    {
        GameManager.Instance.Money++;
        int id = (int)CurrentShape + (int)CurrentColor;

        if(id == 0)
        {
            OnDeath?.Invoke(this.gameObject);
            HandleDeathAnimation(id);
            Destroy(this.gameObject);
        }
        else
        {
            HandleDeathAnimation(id);
            if ((int)CurrentColor == 0)
            {
                CurrentColor = EnemyColor.BLACK;
                CurrentShape--;
            }
            else
            {
                CurrentColor--;
            }
        }
    }

    private void HandleDeathAnimation(int id)
    {
        if (animationCanPlay)
        {
            GameObject go = Instantiate(allPossibleShapes[id], transform.position, transform.rotation, null);
            go.GetComponent<EnemyShape>().DeathAnimation();
        }
    }

    private void HandleDeathOfSpecialEnemy()
    {
        OnDeath?.Invoke(this.gameObject);
        HandleDeathAnimation(0);
        Destroy(this.gameObject);
    }


    private void HandleShapeChanged(Shape shape)
    {
        int id = ((int)CurrentShape * 10) + (int)CurrentColor;
        if (id != 0)
        {
            HandleDeathAnimation(id);
        }
    }

    private void HandleShapeOrColorChanged(Shape shape, EnemyColor color)
    {
        foreach (GameObject item in allPossibleShapes)
        {
            item.SetActive(false);
        }

        //Debug.Log(((int)shape * 10) + (int)color);
        data = allPossibleEnemyData[((int)shape * 10) + (int)color];
        UpdateAllStats(data);

        allPossibleShapes[((int)CurrentShape * 10) + (int)CurrentColor].SetActive(true);

        if (_overflowDamage > 0)
            TakeDamage(_overflowDamage, DamageType.ALL, lastTowerDamagedFrom);
    }
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