using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.ModernUIPack;

public class HorizontalSelectorUIElement : MonoBehaviour
{
    [SerializeField] private HorizontalSelector horizontalSelector;

    public void ChangeTargetting()
    {
        SelectionManager.Instance.ChangeCurrentTargettingBehaviour(horizontalSelector.index);
    }
}
