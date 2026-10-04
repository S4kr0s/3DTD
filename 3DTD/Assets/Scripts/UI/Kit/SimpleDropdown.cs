using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Chamfered dropdown: a field showing the current value and a chevron, opening a scrollable list on its own
// overlay canvas so it draws above the other rows. Clicking outside the list closes it.
public class SimpleDropdown : MonoBehaviour
{
    [SerializeField] private BevelButton field;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform popup;
    [SerializeField] private RectTransform list;
    [SerializeField] private BevelButton itemTemplate;
    [SerializeField] private BevelButton blocker;
    [SerializeField] private float itemHeight = 36f;
    [SerializeField] private int visibleItems = 6;

    public event Action<int> OnValueChanged;

    private readonly List<string> options = new List<string>();
    private readonly List<BevelButton> items = new List<BevelButton>();
    private int value;

    public int Value => value;

    private void Awake()
    {
        field.Clicked += _ => SetOpen(!popup.gameObject.activeSelf);
        blocker.Clicked += _ => SetOpen(false);
        itemTemplate.gameObject.SetActive(false);
        SetOpen(false);
    }

    private void OnDisable()
    {
        SetOpen(false);
    }

    public void SetOptions(IList<string> labels, int selected)
    {
        options.Clear();
        options.AddRange(labels);
        while (items.Count < options.Count)
        {
            BevelButton item = Instantiate(itemTemplate, list);
            int index = items.Count;
            item.Clicked += _ => Choose(index);
            items.Add(item);
        }
        for (int i = 0; i < items.Count; i++)
        {
            bool used = i < options.Count;
            items[i].gameObject.SetActive(used);
            if (used)
                items[i].GetComponentInChildren<TMP_Text>(true).text = options[i];
        }
        SetValue(selected, false);
    }

    public void SetValue(int newValue, bool notify)
    {
        if (options.Count == 0)
            return;
        value = Mathf.Clamp(newValue, 0, options.Count - 1);
        label.text = options[value];
        for (int i = 0; i < items.Count; i++)
            items[i].SetOn(i == value);
        if (notify)
            OnValueChanged?.Invoke(value);
    }

    private void Choose(int index)
    {
        SetOpen(false);
        SetValue(index, true);
    }

    private void SetOpen(bool open)
    {
        if (popup == null)
            return;
        popup.gameObject.SetActive(open);
        blocker.gameObject.SetActive(open);
        if (open)
        {
            float height = Mathf.Min(options.Count, visibleItems) * itemHeight + 8f;
            popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
