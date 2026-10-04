using EPOOutline;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutlineSwitch : MonoBehaviour
{
    [SerializeField] private Selectable thisSelect;
    [SerializeField] private Outlinable outlinable;

    private void Awake()
    {
        thisSelect = GetComponentInParent<Selectable>();
        outlinable = GetComponent<Outlinable>();
        SetOutline(outlinable, outlinable != null && outlinable.OutlineParameters.Enabled);
    }

    // The outline plugin draws every enabled Outlinable each frame (a hidden one in clear colour), plus
    // full-screen passes whenever any is registered; only the component's own enabled flag takes it out
    public static void SetOutline(Outlinable outlinable, bool on)
    {
        if (outlinable == null)
            return;
        outlinable.OutlineParameters.Enabled = on;
        if (outlinable.enabled != on)
            outlinable.enabled = on;
    }

    private void OnEnable()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChange;
    }

    private void OnDisable()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChange;
    }

    private void HandleSelectionChange(Selectable old, Selectable newSelect)
    {
        SetOutline(outlinable, newSelect == thisSelect);
    }
}
