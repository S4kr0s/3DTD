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
        Refresh();
    }

    private void Refresh()
    {
        if (text == null)
            return;
        text.text = prefix + (tabular ? UIFormat.TabularLabel(price) : price.ToString());
        bool affordable = ignoreAffordability || !subscribed || Affordable;
        text.color = affordable ? affordableColor : UITheme.Current.cantAfford;
    }
}
