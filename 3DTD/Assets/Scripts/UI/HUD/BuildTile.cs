using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Build rail tile: tower icon, hotkey badge and price. Unaffordable tiles are dimmed with a red price;
// the selected tile gets the orange rim and inner glow.
public class BuildTile : MonoBehaviour
{
    [SerializeField] private BevelButton button;
    [SerializeField] private Image image;
    [SerializeField] private Graphic selectedGlow;
    [SerializeField] private BevelGraphic hotkeyBadge;
    [SerializeField] private TMP_Text hotkey;
    [SerializeField] private PriceLabel price;

    public event Action<BuildTile> Clicked;
    public event Action<BuildTile, bool> HoverChanged;

    public int Index { get; private set; }
    public GameObject Prefab { get; private set; }
    public Building Building { get; private set; }
    public RectTransform Rect => (RectTransform)button.transform;

    private bool selected;
    private bool subscribed;
    // Money changes every frame in late waves; the tile only restyles when affordability flips
    private int shownAffordability = -1;

    private void Awake()
    {
        button.Clicked += _ => Clicked?.Invoke(this);
        button.HoverChanged += (_, hovered) => HoverChanged?.Invoke(this, hovered);
    }

    private void OnEnable()
    {
        if (!subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            subscribed = true;
        }
        RefreshAffordability();
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
        subscribed = false;
    }

    public void Setup(int index, GameObject prefab)
    {
        Index = index;
        Prefab = prefab;
        Building = prefab != null ? prefab.GetComponent<Building>() : null;
        image.sprite = Building != null ? Building.UISprite : null;
        image.enabled = image.sprite != null;
        hotkey.text = BuildingManager.HotkeyLabel(index);
        price.SetPrice(Cost);
        RefreshAffordability();
    }

    public int Cost => Building != null ? GameManager.PriceOf(Building.Cost) : 0;

    public void SetSelected(bool value)
    {
        selected = value;
        button.SetOn(value);
        if (selectedGlow != null)
            selectedGlow.gameObject.SetActive(value);
        UITheme theme = UITheme.Current;
        hotkeyBadge.SetStyle(value ? BevelStyle.DotOn : BevelStyle.DarkBadge);
        hotkey.color = value ? theme.textOnAccent : theme.textMuted;
        price.SetAffordableColor(value ? theme.accentText : theme.text);
        RefreshAffordability();
    }

    private void HandleMoneyChanged(int money)
    {
        if ((price.Affordable ? 1 : 0) != shownAffordability)
            RefreshAffordability();
    }

    private void RefreshAffordability()
    {
        bool affordable = price.Affordable;
        shownAffordability = affordable ? 1 : 0;
        button.SetStyles(affordable ? BevelStyle.Tile : BevelStyle.TileDim, affordable ? BevelStyle.Tile : BevelStyle.TileDim,
            BevelStyle.TileSelected, BevelStyle.TileDim, BevelStyle.TileSelected);
        Color color = image.color;
        color.a = affordable || selected ? 1f : 0.38f;
        image.color = color;
    }
}
