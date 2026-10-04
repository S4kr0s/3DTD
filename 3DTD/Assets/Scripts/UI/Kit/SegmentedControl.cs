using System;
using System.Collections.Generic;
using UnityEngine;

// One-of-n choice made of BevelButtons: segmented controls (the active item becomes a mini key-cap),
// primary tabs, category tabs and difficulty tiles all use it.
public class SegmentedControl : MonoBehaviour
{
    [SerializeField] private List<BevelButton> items = new List<BevelButton>();
    [SerializeField] private int value;
    [Tooltip("Clicking the active item again clears the selection (value -1)")]
    [SerializeField] private bool allowDeselect = false;

    public event Action<int> OnValueChanged;

    public int Value => value;
    public int Count => items.Count;
    public IReadOnlyList<BevelButton> Items => items;

    private void Awake()
    {
        for (int i = 0; i < items.Count; i++)
            Hook(items[i]);
        Refresh();
    }

    private void Hook(BevelButton item)
    {
        if (item != null)
            item.Clicked += HandleClicked;
    }

    public void SetItems(List<BevelButton> newItems)
    {
        foreach (BevelButton item in items)
            if (item != null) item.Clicked -= HandleClicked;
        items = newItems;
        foreach (BevelButton item in items)
            Hook(item);
        Refresh();
    }

    private void HandleClicked(BevelButton item)
    {
        int index = items.IndexOf(item);
        if (index < 0)
            return;
        if (index == value)
        {
            if (!allowDeselect)
                return;
            index = -1;
        }
        SetValue(index, true);
    }

    // Changes the active item; notify = false is for syncing the control to state set elsewhere
    public void SetValue(int newValue, bool notify)
    {
        if (newValue >= items.Count)
            newValue = items.Count - 1;
        bool changed = newValue != value;
        value = newValue;
        Refresh();
        if (changed && notify)
            OnValueChanged?.Invoke(value);
    }

    public void SetItemInteractable(int index, bool interactable)
    {
        if (index >= 0 && index < items.Count && items[index] != null)
            items[index].SetInteractable(interactable);
    }

    private void Refresh()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].SetOn(i == value);
        }
    }
}
