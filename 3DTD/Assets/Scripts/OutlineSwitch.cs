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
        if (newSelect == thisSelect)
            outlinable.OutlineParameters.Enabled = true;
        else
            outlinable.OutlineParameters.Enabled = false;
    }
}
