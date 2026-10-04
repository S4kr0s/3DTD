using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Level select (redesign B3): game mode -> category tabs -> level cards. Picking a card opens the difficulty
// popup under it. The chip at the top right totals earned medals; category tabs show earned / possible.
// The cards sit in a masked horizontal ScrollRect (drag with mouse or finger, or the wheel); the popup
// follows the selected card while it scrolls.
public class LevelSelectScreen : MonoBehaviour
{
    [SerializeField] private SegmentedControl gameMode;
    [SerializeField] private SegmentedControl categoryTabs;
    [SerializeField] private TMP_Text[] categoryLabels = new TMP_Text[0];
    [SerializeField] private TMP_Text[] categoryCounts = new TMP_Text[0];
    [SerializeField] private GameObject[] categoryCountDots = new GameObject[0];
    [SerializeField] private TMP_Text impossibleTotal;
    [SerializeField] private TMP_Text medalTotal;
    [SerializeField] private RectTransform cardParent;
    [SerializeField] private LevelCard cardTemplate;
    [SerializeField] private ScrollRect cardScroll;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private DifficultyPopup popup;

    private readonly List<LevelCard> cards = new List<LevelCard>();
    private LevelCatalog catalog;
    private LevelCard selectedCard;

    private void Awake()
    {
        catalog = LevelCatalog.Instance;
        cardTemplate.gameObject.SetActive(false);
        categoryTabs.OnValueChanged += ShowCategory;
        popup.StartRequested += StartLevel;
        if (cardScroll != null)
            cardScroll.onValueChanged.AddListener(_ => FollowSelectedCard());
        // Classic is the only game mode so far; Prototype stays visible but disabled
        gameMode.SetValue(0, false);
        gameMode.SetItemInteractable(1, false);
    }

    private void OnEnable()
    {
        PlayerProgress.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProgress.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (catalog == null)
            return;

        int impossible = 0;
        int other = 0;
        for (int c = 0; c < catalog.categories.Count; c++)
        {
            int earned = 0;
            List<LevelCatalog.Level> levels = catalog.LevelsIn(catalog.categories[c].id);
            foreach (LevelCatalog.Level level in levels)
            {
                int medals = PlayerProgress.GetMedals(level.sceneName);
                earned += PlayerProgress.CountMedals(medals);
                // The chip shows Impossible crystals and Easy / Medium / Hard dots separately
                if ((medals & PlayerProgress.MedalBit(Difficulty.Impossible)) != 0)
                    impossible++;
                other += PlayerProgress.CountMedals(medals & 0b0111);
            }
            if (c < categoryLabels.Length)
                categoryLabels[c].text = catalog.categories[c].displayName;
            if (c < categoryCounts.Length)
                categoryCounts[c].text = levels.Count > 0 ? earned + " / " + levels.Count * 4 : "Soon";
            if (c < categoryCountDots.Length && categoryCountDots[c] != null)
                categoryCountDots[c].SetActive(levels.Count > 0);
        }
        impossibleTotal.text = UIFormat.Tabular(impossible);
        medalTotal.text = UIFormat.Tabular(other);

        int category = categoryTabs.Value;
        if (category < 0 || category >= catalog.categories.Count)
            category = CategoryOfFirstOpenLevel();
        ShowCategory(category);
    }

    // The category that holds the furthest unlocked level, so returning players land where they play
    private int CategoryOfFirstOpenLevel()
    {
        int index = 0;
        for (int i = 0; i < catalog.levels.Count; i++)
        {
            if (catalog.IsUnlocked(i))
                index = i;
        }
        string id = catalog.levels.Count > 0 ? catalog.levels[index].category : null;
        return Mathf.Max(0, catalog.categories.FindIndex(c => c.id == id));
    }

    private void ShowCategory(int category)
    {
        categoryTabs.SetValue(category, false);
        string id = catalog.categories[category].id;

        int used = 0;
        int numberInCategory = 0;
        for (int i = 0; i < catalog.levels.Count; i++)
        {
            if (catalog.levels[i].category != id)
                continue;
            numberInCategory++;
            if (used >= cards.Count)
            {
                LevelCard card = Instantiate(cardTemplate, cardParent);
                card.Clicked += Select;
                cards.Add(card);
            }
            cards[used].gameObject.SetActive(true);
            cards[used].Setup(catalog, i, numberInCategory);
            used++;
        }
        for (int i = used; i < cards.Count; i++)
            cards[i].gameObject.SetActive(false);
        if (emptyState != null)
            emptyState.SetActive(used == 0);

        // Pick the furthest unlocked level of this category
        LevelCard pick = null;
        for (int i = 0; i < used; i++)
        {
            if (cards[i].Unlocked)
                pick = cards[i];
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardParent);
        ScrollTo(pick);
        Select(pick);
    }

    // Scrolls the strip just far enough to show the card whole (back to the start without a card)
    private void ScrollTo(LevelCard card)
    {
        if (cardScroll == null)
            return;
        cardScroll.StopMovement();
        RectTransform viewport = cardScroll.viewport;
        float overflow = Mathf.Max(0f, cardParent.rect.width - viewport.rect.width);
        float offset = 0f;
        if (card != null)
        {
            HorizontalOrVerticalLayoutGroup layout = cardParent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            float margin = layout != null ? layout.padding.right : 0f;
            float right = card.Rect.offsetMax.x + margin;
            offset = Mathf.Clamp(right - viewport.rect.width, 0f, overflow);
        }
        Vector2 position = cardParent.anchoredPosition;
        position.x = -offset;
        cardParent.anchoredPosition = position;
    }

    private void FollowSelectedCard()
    {
        if (selectedCard == null || !popup.gameObject.activeSelf)
            return;
        popup.Follow(selectedCard.Rect, IsVisible(selectedCard.Rect));
    }

    // Whether the card's centre is inside the strip's viewport, i.e. the popup notch has something to point at
    private bool IsVisible(RectTransform card)
    {
        if (cardScroll == null)
            return true;
        RectTransform viewport = cardScroll.viewport;
        Vector2 center = viewport.InverseTransformPoint(card.TransformPoint(card.rect.center));
        return viewport.rect.Contains(center);
    }

    private void Select(LevelCard card)
    {
        selectedCard = card;
        foreach (LevelCard other in cards)
            other.SetSelected(other == card);
        if (card != null && card.Unlocked)
            popup.Show(card.Level, card.Rect);
        else
            popup.Hide();
    }

    private void StartLevel(Difficulty difficulty)
    {
        if (selectedCard == null || !LevelCatalog.IsDifficultyUnlocked(selectedCard.Level.sceneName, difficulty))
            return;
        // A new game replaces the autosave of the previous one
        SaveGame.Delete();
        GameSettings.SelectDifficulty(difficulty);
        SceneManager.LoadScene(selectedCard.Level.sceneName);
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeGameSpeed(1f);
    }
}
