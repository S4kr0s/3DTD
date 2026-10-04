using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small chamfered chip: "Damage 3 > 4" (before -> after) or a tier name ("III  Devastator Mines")
public class StatChip : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text text;

    public void SetChange(Sprite sprite, Color tint, string label, string before, string after)
    {
        UITheme theme = UITheme.Current;
        icon.gameObject.SetActive(true);
        icon.sprite = sprite;
        icon.color = tint;
        text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(theme.textMuted) + ">" + label + "</color>  "
            + "<color=#" + ColorUtility.ToHtmlStringRGB(theme.textMuted) + ">" + before + "</color>"
            + "  <color=#" + ColorUtility.ToHtmlStringRGB(theme.accent) + ">›</color>  <b>" + after + "</b>";
    }

    public void SetTier(string tier, string moduleName)
    {
        UITheme theme = UITheme.Current;
        icon.gameObject.SetActive(false);
        text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(theme.accentText) + ">" + tier + "</color>  " + moduleName;
    }
}
