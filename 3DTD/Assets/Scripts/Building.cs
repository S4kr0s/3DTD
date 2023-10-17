using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public string DisplayName { get { return displayName; } }
    public string Description { get { return description; } }
    public Sprite UISprite { get { return uiSprite; } }
    public virtual int Cost { get { return cost; } }

    [Header("Information")]
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private Sprite uiSprite;
    [SerializeField] private int cost;
}
