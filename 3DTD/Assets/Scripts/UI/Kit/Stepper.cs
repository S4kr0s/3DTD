using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Previous / next arrows around a label, with one dot per option showing the current index.
public class Stepper : MonoBehaviour
{
    [SerializeField] private BevelButton previousButton;
    [SerializeField] private BevelButton nextButton;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform dotParent;
    [SerializeField] private BevelGraphic dotTemplate;
    [SerializeField] private float activeDotWidth = 16f;
    [SerializeField] private float dotWidth = 6f;

    public event Action<int> OnIndexChanged;

    private readonly List<string> options = new List<string>();
    private readonly List<BevelGraphic> dots = new List<BevelGraphic>();
    private int index;

    public int Index => index;

    private void Awake()
    {
        if (previousButton != null)
            previousButton.Clicked += _ => Step(-1);
        if (nextButton != null)
            nextButton.Clicked += _ => Step(1);
        if (dotTemplate != null)
            dotTemplate.gameObject.SetActive(false);
    }

    public void SetOptions(IList<string> labels, int selected)
    {
        options.Clear();
        options.AddRange(labels);

        while (dots.Count < options.Count && dotTemplate != null)
        {
            BevelGraphic dot = Instantiate(dotTemplate, dotParent);
            dot.gameObject.SetActive(true);
            dots.Add(dot);
        }
        for (int i = 0; i < dots.Count; i++)
            dots[i].gameObject.SetActive(i < options.Count);

        SetIndex(selected, false);
    }

    public void SetIndex(int newIndex, bool notify)
    {
        if (options.Count == 0)
            return;
        index = (newIndex % options.Count + options.Count) % options.Count;
        if (label != null)
            label.text = options[index];
        for (int i = 0; i < options.Count && i < dots.Count; i++)
        {
            bool active = i == index;
            dots[i].SetStyle(active ? BevelStyle.DotOn : BevelStyle.DotOff);
            dots[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, active ? activeDotWidth : dotWidth);
        }
        if (notify)
            OnIndexChanged?.Invoke(index);
    }

    private void Step(int direction)
    {
        SetIndex(index + direction, true);
    }
}
