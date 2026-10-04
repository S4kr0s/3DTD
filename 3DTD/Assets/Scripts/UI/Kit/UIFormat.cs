using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

// Number and stat formatting shared by HUD, panels and menus
public static class UIFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Tabular figures: every digit gets the same advance so counters don't jitter while they change.
    // TMP has no 'tnum' feature, so the digits go into fixed <mspace> slots sized per font family.
    public static string Tabular(int value)
    {
        return Tabular(value.ToString(Invariant));
    }

    public static string Tabular(string digits)
    {
        UITheme theme = UITheme.Current;
        return Monospace(digits, theme != null ? theme.tabularDigitWidth : 0.66f);
    }

    // Tabular figures for Manrope labels (prices)
    public static string TabularLabel(int value)
    {
        UITheme theme = UITheme.Current;
        return Monospace(value.ToString(Invariant), theme != null ? theme.tabularLabelDigitWidth : 0.6f);
    }

    private static string Monospace(string digits, float em)
    {
        return "<mspace=" + em.ToString("0.###", Invariant) + "em>" + digits + "</mspace>";
    }

    private static float cachedOpenTagEm = float.NaN;
    private static string cachedOpenTag;

    // Tabular(value) for counters that change every frame: no strings are built (TMP still copies the text
    // into a string in the Editor, but not in builds)
    public static void SetTabular(TMP_Text text, int value, StringBuilder buffer)
    {
        UITheme theme = UITheme.Current;
        float em = theme != null ? theme.tabularDigitWidth : 0.66f;
        if (em != cachedOpenTagEm)
        {
            cachedOpenTagEm = em;
            cachedOpenTag = "<mspace=" + em.ToString("0.###", Invariant) + "em>";
        }

        buffer.Clear();
        buffer.Append(cachedOpenTag);
        AppendDigits(buffer, value);
        buffer.Append("</mspace>");
        text.SetText(buffer);
    }

    // StringBuilder.Append(int) formats through a temporary string on Mono
    private static void AppendDigits(StringBuilder buffer, int value)
    {
        if (value == 0)
        {
            buffer.Append('0');
            return;
        }
        long remaining = value;
        if (remaining < 0)
        {
            buffer.Append('-');
            remaining = -remaining;
        }
        int start = buffer.Length;
        while (remaining > 0)
        {
            buffer.Append((char)('0' + (int)(remaining % 10)));
            remaining /= 10;
        }
        for (int left = start, right = buffer.Length - 1; left < right; left++, right--)
        {
            char swap = buffer[left];
            buffer[left] = buffer[right];
            buffer[right] = swap;
        }
    }

    // Up to one decimal, trailing ".0" dropped: 3 / 3.5 / 12
    public static string Short(float value)
    {
        float rounded = Mathf.Round(value * 10f) / 10f;
        if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.001f)
            return Mathf.RoundToInt(rounded).ToString(Invariant);
        return rounded.ToString("0.0", Invariant);
    }

    public static string Signed(int value)
    {
        return (value >= 0 ? "+" : "−") + Mathf.Abs(value).ToString(Invariant);
    }

    public static string Percent(float fraction)
    {
        return Mathf.RoundToInt(fraction * 100f).ToString(Invariant) + "%";
    }

    public static string SignedPercent(float percent)
    {
        int rounded = Mathf.RoundToInt(percent);
        return (rounded >= 0 ? "+" : "−") + Mathf.Abs(rounded).ToString(Invariant) + "%";
    }

    public static string TwoDigits(int value)
    {
        return value.ToString("00", Invariant);
    }

    public static string Roman(int value)
    {
        switch (value)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            default: return value.ToString(Invariant);
        }
    }
}
