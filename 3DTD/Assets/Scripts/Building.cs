using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public string DisplayName { get { return displayName; } }
    public string Description { get { return description; } }
    public Sprite UISprite { get { return uiSprite; } }
    public virtual int Cost { get { return cost; } }
    // Money actually spent on this building: purchase price (after difficulty) plus bought upgrades
    public int Invested { get { return invested; } }
    // Index in GameManager.Buildings when the player built this; -1 for buildings that came with the map
    public int PaletteIndex { get { return paletteIndex; } }
    public bool PlacedByPlayer { get { return paletteIndex >= 0; } }

    [Header("Information")]
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private Sprite uiSprite;
    [SerializeField] private int cost;

    private int invested = 0;
    private int paletteIndex = -1;

    public void AddInvestment(int amount)
    {
        invested += amount;
    }

    public void MarkPlacedByPlayer(int index)
    {
        paletteIndex = index;
    }
}
