using TMPro;
using UnityEngine;

// A price that turns "can't afford" red while it is above the current scrap, updated live as scrap changes
public class PriceLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private bool tabular = true;
    [SerializeField] private string prefix = "";

    private int price;
    private bool subscribed;
    private bool ignoreAffordability;
    private int shownPrice = int.MinValue;
    private string shownPrefix;
    private int shownAffordability = -1;

    public int Price => price;
    public bool Affordable => GameManager.Instance == null || GameManager.Instance.Money >= price;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
        subscribed = false;
    }

    private void Subscribe()
    {
        if (!subscribed && GameManager.Instance != null && !GameManager.Instance.IsMainMenu)
        {
            GameManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            subscribed = true;
        }
    }

    public void SetPrice(int value)
    {
        price = value;
        Subscribe();
        Refresh();
    }

    public void SetAffordableColor(Color color)
    {
        affordableColor = color;
        Refresh();
    }

    // For prices that can't be paid yet anyway (later upgrade tiers): always the given colour, never red
    public void SetIgnoreAffordability(bool ignore)
    {
        ignoreAffordability = ignore;
        Refresh();
    }

    private void HandleMoneyChanged(int money)
    {
        // Money changes every frame in late waves; only a flip in affordability changes this label
        if ((IsShownAffordable() ? 1 : 0) != shownAffordability)
            Refresh();
    }

    private bool IsShownAffordable()
    {
        return ignoreAffordability || !subscribed || Affordable;
    }

    private void Refresh()
    {
        if (text == null)
            return;
        if (price != shownPrice || prefix != shownPrefix)
        {
            shownPrice = price;
            shownPrefix = prefix;
            text.text = prefix + (tabular ? UIFormat.TabularLabel(price) : price.ToString());
        }
        bool affordable = IsShownAffordable();
        shownAffordability = affordable ? 1 : 0;
        text.color = affordable ? affordableColor : UITheme.Current.cantAfford;
    }
}
