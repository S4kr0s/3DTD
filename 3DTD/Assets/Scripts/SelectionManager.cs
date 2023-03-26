using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    [SerializeField] private static Selectable currentlySelected;

    public static Selectable CurrentlySelected
    {
        get
        {
            return currentlySelected;
        }

        set
        {
            OnSelectionChange?.Invoke(currentlySelected, value);
            currentlySelected = value;
        }
    }

    public static event Action<Selectable, Selectable> OnSelectionChange;

    private static SelectionManager instance;
    public static SelectionManager Instance { get { return instance; } }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }

    private void Start()
    {
        OnSelectionChange += HandleSelectionChange;
    }

    private void HandleSelectionChange(Selectable oldSelection, Selectable newSelection)
    {
        if (oldSelection == newSelection)
            return;

        if(oldSelection != null)
            oldSelection.Deselect();
        newSelection.Select();

        if (oldSelection != null)
            if (oldSelection.gameObject.TryGetComponent(out Tower oldTower))
                oldTower.OnTowerDestroyed += HandleSelectedTowerIsDestroyed;
        if (newSelection.gameObject.TryGetComponent<Tower>(out Tower newTower))
            newTower.OnTowerDestroyed += HandleSelectedTowerIsDestroyed;
    }

    private void HandleSelectedTowerIsDestroyed(Tower tower)
    {
        tower.gameObject.GetComponent<Selectable>().Deselect();
    }

    public void ChangeCurrentTargettingBehaviour(int targetBehaviour)
    {
        CurrentlySelected.TryGetComponent<Tower>(out Tower tower);

        if (tower != null)
        {
            tower.ChangeTargettingBehaviour((TargetBehaviour)targetBehaviour);
        }
    }

    public void SellCurrentTower()
    {
        CurrentlySelected.SellThisTower();
    }

    private void Update()
    {
        //Debug.Log(currentlySelected);
    }
}
