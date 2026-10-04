using UnityEngine;
using UnityEngine.UI;

// Easy, Medium and Hard as three dots (glowing when earned, hollow ring when not), a thin divider,
// then the Impossible crystal (glowing when earned, dashed hexagon outline when not).
public class MedalRow : MonoBehaviour
{
    [SerializeField] private Image[] dots = new Image[3];
    [SerializeField] private Image impossible;

    public void Set(int medalMask)
    {
        UITheme theme = UITheme.Current;
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
                continue;
            bool earned = (medalMask & (1 << i)) != 0;
            dots[i].sprite = earned ? theme.medalDot : theme.medalRing;
        }
        if (impossible != null)
        {
            bool earned = (medalMask & (1 << 3)) != 0;
            impossible.sprite = theme.Icon(earned ? "fx_medal_impossible" : "fx_medal_impossible_empty");
        }
    }
}
