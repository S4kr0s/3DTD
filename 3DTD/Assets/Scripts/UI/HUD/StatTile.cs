using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One cell of a stat grid: icon and value on top, caption below
public class StatTile : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text value;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private TooltipTrigger tooltip;

    public void Set(TowerStatKind kind, string valueText)
    {
        UITheme theme = UITheme.Current;
        if (icon != null)
        {
            icon.sprite = theme.Icon(TowerStatInfo.Icon(kind));
            icon.color = TowerStatInfo.IconTint(kind);
        }
        if (caption != null)
            caption.text = TowerStatInfo.Label(kind);
        if (tooltip != null)
            tooltip.SetText(TowerStatInfo.Label(kind), TowerStatInfo.Description(kind));
        SetValue(valueText);
    }

    public void SetValue(string valueText)
    {
        if (value != null && value.text != valueText)
            value.text = valueText;
    }
}
