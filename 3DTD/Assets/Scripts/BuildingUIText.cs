using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine.UI;

public class BuildingUIText : MonoBehaviour
{
    private ButtonManager buttonManager;

    public Building Building;
    public Sprite sprite;
    public string Name;
    public string Cost;
    public string Description;

    public TextMeshProUGUI nameNormal;
    public TextMeshProUGUI nameHighlighted;
    public TextMeshProUGUI costNormal;
    public TextMeshProUGUI costHighlighted;
    public Image imageNormal;
    public Image imageHighlighted;

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

        nameNormal.text = Name; nameHighlighted.text = Name;
        costNormal.text = Cost; costHighlighted.text = Cost;
        imageNormal.sprite = sprite;
        imageHighlighted.sprite = sprite;
        /*
        if (state)
        {
            buttonManager.buttonText = 
            buttonManager.normalText.text = $"<-> {Name} <->";
            buttonManager.highlightedText.text = $"<-> {Name} <->";
            buttonManager.buttonText2 = 
        }
        else
        {
            buttonManager.normalText.text = $"{Name}";
            buttonManager.highlightedText.text = $"{Name}";
        }
        */
    }

    public void SelectBuilding()
    {
        BuildingManager.Instance.ChangeSelectedBuilding(this);
    }
}
