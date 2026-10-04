using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Level card in the level select: path thumbnail, number badge, name, medal count and medal row.
// States: mastered (all four medals), available, selected and locked (dimmed path, lock hexagon, unlock hint).
public class LevelCard : MonoBehaviour
{
    [SerializeField] private BevelButton button;
    [SerializeField] private PathGraphic thumbnail;
    [SerializeField] private BevelGraphic numberBadge;
    [SerializeField] private TMP_Text number;
    [SerializeField] private GameObject masteredTag;
    [SerializeField] private GameObject lockBadge;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text medalCount;
    [SerializeField] private MedalRow medalRow;
    [SerializeField] private GameObject unlockHint;
    [SerializeField] private TMP_Text unlockHintText;
    [SerializeField] private TooltipTrigger tooltip;

    public event Action<LevelCard> Clicked;

    public int CatalogIndex { get; private set; }
    public LevelCatalog.Level Level { get; private set; }
    public bool Unlocked { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        button.Clicked += _ => Clicked?.Invoke(this);
        if (tooltip != null)
            tooltip.Provider = TooltipText;
    }

    private string unlockHintCache;

    private (string title, string body) TooltipText()
    {
        if (Level == null)
            return ("", "");
        if (!Unlocked)
            return (Level.displayName, unlockHintCache + ".");
        List<string> earned = new List<string>();
        List<string> missing = new List<string>();
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Impossible })
            (PlayerProgress.HasMedal(Level.sceneName, difficulty) ? earned : missing).Add(difficulty.ToString());
        string body = earned.Count == 0 ? "No medals yet." : "Medals: " + string.Join(", ", earned) + ".";
        if (missing.Count > 0 && earned.Count > 0)
            body += "\nStill open: " + string.Join(", ", missing) + ".";
        return (Level.displayName, body + "\nClick to choose a difficulty.");
    }

    public void Setup(LevelCatalog catalog, int catalogIndex, int numberInCategory)
    {
        CatalogIndex = catalogIndex;
        Level = catalog.levels[catalogIndex];
        Unlocked = catalog.IsUnlocked(catalogIndex);
        int medals = PlayerProgress.GetMedals(Level.sceneName);
        bool mastered = medals == 0b1111;
        UITheme theme = UITheme.Current;

        number.text = UIFormat.TwoDigits(numberInCategory);
        title.text = Level.displayName;
        title.color = Unlocked ? theme.text : theme.textLocked;
        masteredTag.SetActive(mastered && Unlocked);
        lockBadge.SetActive(!Unlocked);
        medalRow.gameObject.SetActive(Unlocked);
        unlockHint.SetActive(!Unlocked);
        medalCount.text = Unlocked ? PlayerProgress.CountMedals(medals) + " / 4 medals" : "Locked";
        if (Unlocked)
            medalRow.Set(medals);
        else
            unlockHintText.text = catalog.UnlockHint(catalogIndex);
        unlockHintCache = Unlocked ? "" : catalog.UnlockHint(catalogIndex);

        DrawThumbnail(Unlocked);
        BevelStyle normal = !Unlocked ? BevelStyle.LevelCardLocked : mastered ? BevelStyle.LevelCardMastered : BevelStyle.LevelCard;
        button.SetStyles(normal, normal, normal, BevelStyle.LevelCardLocked, BevelStyle.LevelCardSelected);
        button.SetInteractable(Unlocked);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        button.SetOn(selected);
        UITheme theme = UITheme.Current;
        numberBadge.SetStyle(selected ? BevelStyle.KeyCapMini : BevelStyle.DarkBadge);
        number.color = selected ? theme.textOnAccent : theme.text;
    }

    // The level's lane paths as a line graph: dark casing, lilac line, green spawn and red exit
    private void DrawThumbnail(bool unlocked)
    {
        thumbnail.Clear();
        thumbnail.SetContentAspect(Level.thumbnailAspect);
        float alpha = unlocked ? 1f : 0.35f;
        Color casing = BevelStyles.Hex("#2F2A80", alpha);
        Color line = BevelStyles.Hex("#A99BFF", alpha);
        foreach (LevelCatalog.Lane lane in Level.lanes)
        {
            if (lane.points.Count < 2)
                continue;
            thumbnail.AddLine(lane.points, 9f, casing);
            thumbnail.AddLine(lane.points, 4.5f, line);
        }
        foreach (LevelCatalog.Lane lane in Level.lanes)
        {
            if (lane.points.Count < 2)
                continue;
            thumbnail.AddDot(lane.points[0], 4f, BevelStyles.Hex("#46E08A", alpha));
            thumbnail.AddDot(lane.points[lane.points.Count - 1], 4f, BevelStyles.Hex("#FF5A6E", alpha));
        }
    }
}
