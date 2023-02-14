using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Enemy : MonoBehaviour
{
    [SerializeField] public EnemyData data;
    [SerializeField] private float currentHealth;
    [SerializeField] private Waypoints waypoints;
    private int waypointIndex = 0;

    [SerializeField] private float distanceTraveled = 0f;
    public float DistanceTraveled => distanceTraveled;

    [SerializeField] private float speedRandomRotation = 2f;
    [SerializeField] private Shape currentShape;
    [SerializeField] private EnemyColor currentColor;
    [SerializeField] private GameObject[] allPossibleShapes;
    [SerializeField] private EnemyData[] allPossibleEnemyData;

    public Shape CurrentShape 
    { 
        get { return currentShape; } 

        set 
        { 
            currentShape = value;
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

    public EnemyData Data => data;
    public float CurrentHealth => currentHealth;
    public int Id => ((int)CurrentShape * 10) + (int)CurrentColor;

    public event Action<float> OnHealthUpdated;
    public event Action<GameObject> OnDeath;
    public event Action<Shape, EnemyColor> OnShapeOrColorChanged;

    private Quaternion randomRotation;

    private void Start()
    {
        OnShapeOrColorChanged += HandleShapeOrColorChanged;
        waypoints = GameObject.FindGameObjectWithTag("WaypointManager").GetComponent<Waypoints>();
        currentHealth = data.Health; 
        randomRotation = Random.rotation;
        CurrentShape = data.StartShape;
        CurrentColor = data.StartColor;
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

        Vector3 moveTowards = Vector3.MoveTowards(transform.position, waypoints.WaypointsArray[waypointIndex].position, data.MovementSpeed * Time.deltaTime);
        distanceTraveled += Vector3.Distance(transform.position, moveTowards);
        transform.position = moveTowards;

        if (Vector3.Distance(transform.position, waypoints.WaypointsArray[waypointIndex].position) < 0.1f)
            if (waypointIndex < waypoints.WaypointsArray.Count - 1)
                waypointIndex++;
    }

    public void TakeDamage(float damage, DamageType damageType)
    {
        if (damageType == DamageType.ALL || damageType == data.DamageType)
        {
            // Do armor calculations etc. here in the future.
            HandleDamageTaken(damage);
        }
    }

    public void DestroyWholeEnemy()
    {
        OnDeath?.Invoke(this.gameObject);
        Destroy(this.gameObject);
    }

    private void HandleDamageTaken(float damage)
    {
        currentHealth -= damage;
        GameManager.Instance.Money++;
        OnHealthUpdated?.Invoke(currentHealth);
        if (currentHealth <= 0)
            HandleDeathOfSingleShape();
    }

    private void HandleDeathOfSingleShape()
    {
        int id = (int)CurrentShape + (int)CurrentColor;

        if(id == 0)
        {
            OnDeath?.Invoke(this.gameObject);
            Destroy(this.gameObject);
        }
        else
        {
            if((int)CurrentColor == 0)
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

    private void HandleShapeOrColorChanged(Shape shape, EnemyColor color)
    {
        foreach (GameObject item in allPossibleShapes)
        {
            item.SetActive(false);
        }

        Debug.Log(((int)shape * 10) + (int)color);
        data = allPossibleEnemyData[((int)shape * 10) + (int)color];
        UpdateAllStats(data);

        allPossibleShapes[((int)CurrentShape * 10) + (int)CurrentColor].SetActive(true);
    }
}

public enum Shape
{
    TETRAHEDRON = 0,
    OCTAHEDRON = 1,
    ICOSAHEDRON = 2
}

public enum EnemyColor
{
    RED = 0,
    ORANGE = 1,
    YELLOW = 2,
    GREEN = 3,
    CYAN = 4,
    BLUE = 5,
    PURPLE = 6,
    PINK = 7,
    WHITE = 8,
    BLACK = 9
}