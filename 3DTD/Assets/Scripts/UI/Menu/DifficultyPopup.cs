using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup under the picked level card: four difficulty tiles and the Start CTA, with a notch pointing at the
// card. Locked difficulties show their unlock condition.
public class DifficultyPopup : MonoBehaviour
{
    [Serializable]
    public class Tile
    {
        public BevelButton button;
        public Image[] dots = new Image[0];
        public Image crystal;
        public TMP_Text title;
        public Image statusIcon;
        public TMP_Text status;
        public TooltipTrigger tooltip;
    }

    [SerializeField] private RectTransform plate;
    [SerializeField] private RectTransform notch;
    [SerializeField] private TMP_Text levelCaption;
    [SerializeField] private SegmentedControl tileGroup;
    [SerializeField] private Tile[] tiles = new Tile[4];
    [SerializeField] private BevelButton startButton;
    [SerializeField] private float gap = 14f;
    [Tooltip("Indexed by Difficulty, for the tiles' hover text")]
    [SerializeField] private DifficultyProfile[] profiles = new DifficultyProfile[0];

    public event Action<Difficulty> StartRequested;

    private string sceneName;

    public Difficulty Selected => (Difficulty)Mathf.Max(0, tileGroup.Value);

    private void Awake()
    {
        startButton.Clicked += _ => StartRequested?.Invoke(Selected);
        tileGroup.OnValueChanged += _ => RefreshTiles();
        for (int i = 0; i < tiles.Length; i++)
        {
            int index = i;
            if (tiles[i].tooltip != null)
                tiles[i].tooltip.Provider = () => TooltipText((Difficulty)index);
        }
    }

    private (string title, string body) TooltipText(Difficulty difficulty)
    {
        DifficultyProfile profile = null;
        foreach (DifficultyProfile candidate in profiles)
        {
            if (candidate != null && candidate.difficulty == difficulty)
                profile = candidate;
        }
        List<string> lines = new List<string>();
        if (profile != null)
        {
            lines.Add("Hull " + profile.lives + " · prices ×" + profile.priceMultiplier.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("Enemies: speed ×" + profile.enemySpeedMultiplier.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)
                + ", health ×" + profile.layerHealthMultiplier.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("Win after wave " + profile.winRound + ".");
        }
        if (sceneName != null)
        {
            if (!LevelCatalog.IsDifficultyUnlocked(sceneName, difficulty))
                lines.Add("Locked: clear Hard on this level first.");
            else if (PlayerProgress.HasMedal(sceneName, difficulty))
                lines.Add("Medal earned.");
            else
                lines.Add("Win to earn its medal" + (difficulty == Difficulty.Impossible ? " (a crystal)." : "."));
        }
        return (difficulty.ToString(), string.Join("\n", lines));
    }

    public void Show(LevelCatalog.Level level, RectTransform card)
    {
        sceneName = level.sceneName;
        levelCaption.text = level.displayName;
        gameObject.SetActive(true);

        int preferred = Mathf.Clamp((int)GameSettings.SelectedDifficulty, 0, 3);
        if (!LevelCatalog.IsDifficultyUnlocked(sceneName, (Difficulty)preferred))
            preferred = (int)Difficulty.Hard;
        tileGroup.SetValue(preferred, false);
        RefreshTiles();
        Place(card);
        notch.gameObject.SetActive(true);
    }

    // Keeps the popup under its card while the card strip scrolls; the notch hides once the card has left the view
    public void Follow(RectTransform card, bool cardVisible)
    {
        Place(card);
        notch.gameObject.SetActive(cardVisible);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RefreshTiles()
    {
        UITheme theme = UITheme.Current;
        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            Difficulty difficulty = (Difficulty)i;
            bool unlocked = LevelCatalog.IsDifficultyUnlocked(sceneName, difficulty);
            bool earned = PlayerProgress.HasMedal(sceneName, difficulty);
            bool selected = tileGroup.Value == i;

            tile.button.SetInteractable(unlocked);
            tile.title.color = unlocked ? theme.text : theme.textLocked;
            foreach (Image dot in tile.dots)
            {
                dot.sprite = earned ? theme.medalDot : theme.dotPlain;
                dot.color = earned ? Color.white : (selected ? theme.iconTint : new Color(0.81f, 0.77f, 1f, 0.45f));
            }
            if (tile.crystal != null)
                tile.crystal.sprite = theme.Icon(earned ? "fx_medal_impossible" : "fx_medal_impossible_empty");

            if (!unlocked)
            {
                tile.statusIcon.gameObject.SetActive(true);
                tile.statusIcon.sprite = theme.Icon("ic_lock");
                tile.statusIcon.color = theme.textDim;
                tile.status.text = "Clear Hard";
                tile.status.color = theme.textDim;
            }
            else if (earned)
            {
                tile.statusIcon.gameObject.SetActive(true);
                tile.statusIcon.sprite = theme.Icon("ic_check");
                tile.statusIcon.color = theme.accentText;
                tile.status.text = "Medal";
                tile.status.color = theme.accentText;
            }
            else
            {
                tile.statusIcon.gameObject.SetActive(false);
                tile.status.text = "No medal yet";
                tile.status.color = theme.textMuted;
            }
        }
    }

    // Below the card, centred on it but inside the content plate; the notch points up at the card's centre
    private void Place(RectTransform card)
    {
        RectTransform space = (RectTransform)plate.parent;
        Vector3[] corners = new Vector3[4];
        card.GetWorldCorners(corners);
        Vector2 min = space.InverseTransformPoint(corners[0]);
        Vector2 max = space.InverseTransformPoint(corners[2]);
        float centerX = (min.x + max.x) * 0.5f;

        Rect bounds = space.rect;
        Vector2 size = plate.rect.size;
        float x = Mathf.Clamp(centerX - size.x * 0.5f, bounds.xMin + 20f, bounds.xMax - 20f - size.x);
        float y = min.y - gap - size.y;

        plate.pivot = Vector2.zero;
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.localPosition = new Vector3(x, y, 0f);

        notch.pivot = new Vector2(0f, 0.5f);
        notch.anchorMin = notch.anchorMax = new Vector2(0.5f, 0.5f);
        notch.localPosition = new Vector3(centerX, y + size.y - 1f, 0f);
        notch.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }
}
