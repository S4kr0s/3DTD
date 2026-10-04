using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One upgrade module in the tower panel's 3 x 3 grid (a row per path, tiers 1-3 left to right).
// Owned modules are orange, the path's next module shows its price and a scrap / price progress bar and
// is the only clickable one, later tiers are dim, and tiers the cross-path limits rule out show a lock.
public class UpgradeCell : MonoBehaviour
{
    public enum State
    {
        Owned,
        Next,
        Later,
        Blocked,
    }

    [SerializeField] private BevelButton button;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject priceGroup;
    [SerializeField] private PriceLabel price;
    [SerializeField] private GameObject statusGroup;
    [SerializeField] private Image statusIcon;
    [SerializeField] private TMP_Text status;
    [SerializeField] private RectTransform progressTrack;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TooltipTrigger tooltip;

    public event Action<UpgradeCell> Clicked;

    public UpgradeModule Module { get; private set; }
    public State CurrentState { get; private set; }
    public int Tier { get; private set; }
    public BevelButton Button => button;

    private void Awake()
    {
        button.Clicked += _ => Clicked?.Invoke(this);
        if (tooltip != null)
            tooltip.Provider = TooltipText;
    }

    public void Bind(UpgradeModule module, int tier, State state)
    {
        Module = module;
        Tier = tier;
        CurrentState = state;
        UITheme theme = UITheme.Current;

        title.text = module != null ? module.Name : "—";
        icon.sprite = theme.Icon(TowerStatInfo.ModuleIcon(module));

        bool showPrice = state == State.Next || state == State.Later;
        priceGroup.SetActive(showPrice);
        statusGroup.SetActive(!showPrice);
        progressTrack.gameObject.SetActive(state == State.Next);
        if (showPrice && module != null)
        {
            price.SetPrice(GameManager.PriceOf(module.Price));
            price.SetIgnoreAffordability(state == State.Later);
            price.SetAffordableColor(state == State.Next ? theme.text : theme.textDim);
        }
        if (state == State.Owned)
        {
            status.text = "Owned";
            statusIcon.sprite = theme.Icon("ic_check");
            statusIcon.color = status.color = theme.accentText;
        }
        else if (state == State.Blocked)
        {
            status.text = "Locked";
            statusIcon.sprite = theme.Icon("ic_lock");
            statusIcon.color = status.color = theme.textDim;
        }

        title.color = state == State.Owned || state == State.Next ? theme.text : state == State.Later ? theme.textMuted : theme.textLocked;
        icon.color = state == State.Blocked ? theme.textLocked : (state == State.Owned ? theme.accentText : BevelStyles.Hex("#E7E2FF"));

        switch (state)
        {
            case State.Owned:
                button.SetStyles(BevelStyle.UpgradeOwned, BevelStyle.UpgradeOwned, BevelStyle.UpgradeOwned, BevelStyle.UpgradeOwned, BevelStyle.UpgradeOwned);
                break;
            case State.Next:
                button.SetStyles(BevelStyle.Row, BevelStyle.RowAccent, BevelStyle.RowAccent, BevelStyle.RowDisabled, BevelStyle.Row);
                break;
            default:
                button.SetStyles(BevelStyle.RowDisabled, BevelStyle.RowDisabled, BevelStyle.RowDisabled, BevelStyle.RowDisabled, BevelStyle.RowDisabled);
                break;
        }
        button.SetInteractable(state == State.Next);
        UpdateProgress();
        if (tooltip != null)
            tooltip.Refresh();
    }

    // Current scrap / cost, clamped to 100 %
    public void UpdateProgress()
    {
        if (CurrentState != State.Next || Module == null || progressFill == null || GameManager.Instance == null)
            return;
        int cost = Mathf.Max(1, GameManager.PriceOf(Module.Price));
        float fraction = Mathf.Clamp01(GameManager.Instance.Money / (float)cost);
        // Setting anchors dirties the canvas layout; skip it while the bar is full or unchanged
        if (Mathf.Approximately(progressFill.anchorMax.x, fraction) && progressFill.anchorMin == Vector2.zero)
            return;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(fraction, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
    }

    private (string title, string body) TooltipText()
    {
        if (Module == null)
            return ("", "");
        string description = string.IsNullOrEmpty(Module.Description) ? "" : Module.Description.Trim() + "\n\n";
        return (Module.Name, description + StateLine());
    }

    private string StateLine()
    {
        UITheme theme = UITheme.Current;
        string accent = "#" + ColorUtility.ToHtmlStringRGB(theme.accentText);
        string dim = "#" + ColorUtility.ToHtmlStringRGB(theme.textMuted);
        int cost = GameManager.PriceOf(Module.Price);
        switch (CurrentState)
        {
            case State.Owned:
                return "<color=" + accent + ">Tier " + Tier + " · owned</color>";
            case State.Next:
            {
                int money = GameManager.Instance != null ? GameManager.Instance.Money : 0;
                return money >= cost
                    ? "<color=" + accent + ">Click to buy for " + cost + " scrap</color>"
                    : "<color=" + dim + ">Costs " + cost + " scrap · " + (cost - money) + " more needed</color>";
            }
            case State.Later:
                return "<color=" + dim + ">Costs " + cost + " scrap · buy tier " + (Tier - 1) + " first</color>";
            default:
                return "<color=" + dim + ">" + BlockedReason(Tier) + "</color>";
        }
    }

    // The cross-path limits of UpgradeManager.CheckPathBlocking
    public static string BlockedReason(int tier)
    {
        switch (tier)
        {
            case 1: return "Locked: only two paths can be upgraded.";
            case 2: return "Locked: only two paths can reach tier 2.";
            default: return "Locked: only one path can reach tier 3.";
        }
    }
}
