using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Base stat with a 10-segment meter, normalised against the highest value of that stat across all towers
public class StatMeter : MonoBehaviour
{
    public const int Segments = 10;

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text value;
    [SerializeField] private BevelGraphic[] segments = new BevelGraphic[Segments];
    [SerializeField] private TooltipTrigger tooltip;

    public void Set(TowerStatKind kind, float raw, float maxDisplay)
    {
        UITheme theme = UITheme.Current;
        icon.sprite = theme.Icon(TowerStatInfo.Icon(kind));
        icon.color = TowerStatInfo.IconTint(kind);
        label.text = TowerStatInfo.Label(kind);
        value.text = TowerStatInfo.Format(kind, raw);
        if (tooltip != null)
            tooltip.SetText(TowerStatInfo.Label(kind), TowerStatInfo.Description(kind) + "\nThe bar compares it with the strongest tower.");

        float display = TowerStatInfo.Display(kind, raw);
        int filled = display > 0f && maxDisplay > 0f ? Mathf.Clamp(Mathf.CeilToInt(display / maxDisplay * Segments - 0.001f), 1, Segments) : 0;
        for (int i = 0; i < segments.Length; i++)
            segments[i].SetStyle(i < filled ? BevelStyle.MeterOn : BevelStyle.MeterOff);
    }
}
