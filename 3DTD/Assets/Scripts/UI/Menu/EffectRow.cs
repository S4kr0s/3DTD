using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Effect line of a meta-upgrade detail card: icon, name, scope and value ("Fire rate  all turrets  +10%")
public class EffectRow : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text scope;
    [SerializeField] private TMP_Text value;

    public void Set(MetaUpgradeTree.Effect effect)
    {
        UITheme theme = UITheme.Current;
        Describe(effect, out string iconName, out string name, out string text);

        icon.sprite = theme.Icon(iconName);
        bool faceted = iconName.StartsWith("fx_");
        icon.color = faceted ? Color.white : theme.accentText;
        icon.rectTransform.localScale = Vector3.one * (faceted ? theme.facetSpriteScale : 1f);
        title.text = name;
        scope.text = effect.scope;
        value.text = text;
    }

    // "Fire rate", "+10%" and its icon; also used by the skill nodes' hover text
    public static void Describe(MetaUpgradeTree.Effect effect, out string iconName, out string name, out string text)
    {
        switch (effect.type)
        {
            case MetaEffectType.TowerStatPercent:
                TowerStatKind? kind = TowerStatInfo.FromStatType(effect.stat);
                iconName = kind.HasValue ? TowerStatInfo.Icon(kind.Value) : "ic_rotate";
                name = kind.HasValue ? TowerStatInfo.Label(kind.Value) : effect.stat == Stat.StatType.RELOAD_SPEED ? "Reload time" : effect.stat.ToString();
                text = UIFormat.SignedPercent(effect.value);
                break;
            case MetaEffectType.StartMoney:
                iconName = "fx_gem_scrap"; name = "Start scrap"; text = UIFormat.Signed(Mathf.RoundToInt(effect.value));
                break;
            case MetaEffectType.IncomePercent:
                iconName = "fx_gem_scrap"; name = "Scrap per pop"; text = UIFormat.SignedPercent(effect.value);
                break;
            case MetaEffectType.WaveBonus:
                iconName = "fx_gem_wave"; name = "Wave bonus"; text = UIFormat.Signed(Mathf.RoundToInt(effect.value));
                break;
            case MetaEffectType.StartLives:
                iconName = "fx_gem_hull"; name = "Hull"; text = UIFormat.Signed(Mathf.RoundToInt(effect.value));
                break;
            case MetaEffectType.PricePercent:
                iconName = "ic_coin"; name = "Prices"; text = UIFormat.SignedPercent(-effect.value);
                break;
            default:
                iconName = "ic_recycle"; name = "Sell refund"; text = UIFormat.SignedPercent(effect.value);
                break;
        }
    }
}
