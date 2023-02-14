using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.ModernUIPack;

public class BuildingUIText : MonoBehaviour
{
    private ButtonManager buttonManager;

    public Building Building;
    public string Name;
    public string Description;

    private void Awake()
    {
        buttonManager = this.gameObject.GetComponent<ButtonManager>();
    }

    private void Start()
    {
        ChangeText(false);
    }

    public void ChangeText(bool state)
    {
        if (buttonManager == null)
            return;

        if (state)
        {
            buttonManager.normalText.text = $"<-> {Name} <->";
            buttonManager.highlightedText.text = $"<-> {Name} <->";
        }
        else
        {
            buttonManager.normalText.text = $"{Name}";
            buttonManager.highlightedText.text = $"{Name}";
        }
    }

    public void SelectBuilding()
    {
        BuildingManager.Instance.ChangeSelectedBuilding(this);
    }
}
