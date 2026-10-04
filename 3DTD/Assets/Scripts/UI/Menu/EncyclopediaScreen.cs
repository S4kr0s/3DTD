using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Towers (redesign B6): a brochure spread. A thumbnail rail of every buildable entry, the left page with the
// hero render, number, name, cost and base stats as 10-segment meters, the right page with one card per
// upgrade path. The entries come from the build palette on GAME_SETUP, so the book always matches the game.
public class EncyclopediaScreen : MonoBehaviour
{
    [SerializeField] private GameManager palette;

    [Header("Rail")]
    [SerializeField] private RectTransform railParent;
    [SerializeField] private BevelButton thumbTemplate;

    [Header("Left page")]
    [SerializeField] private Image heroImage;
    [SerializeField] private TMP_Text number;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text cost;
    [SerializeField] private GameObject statsBlock;
    [SerializeField] private StatMeter[] meters = new StatMeter[8];
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text leftPageNumber;

    [Header("Right page")]
    [SerializeField] private RectTransform pathParent;
    [SerializeField] private PathCard pathTemplate;
    [SerializeField] private TMP_Text noPaths;
    [SerializeField] private TMP_Text rightPageNumber;

    private readonly List<GameObject> entries = new List<GameObject>();
    private readonly List<BevelButton> thumbs = new List<BevelButton>();
    private readonly List<PathCard> cards = new List<PathCard>();
    private readonly Dictionary<TowerStatKind, float> maxima = new Dictionary<TowerStatKind, float>();
    private int current = -1;

    private void Awake()
    {
        thumbTemplate.gameObject.SetActive(false);
        pathTemplate.gameObject.SetActive(false);
        if (palette == null)
            return;

        foreach (GameObject prefab in palette.Buildings)
        {
            if (prefab != null && prefab.GetComponent<Building>() != null)
                entries.Add(prefab);
        }
        ComputeMaxima();

        for (int i = 0; i < entries.Count; i++)
        {
            BevelButton thumb = Instantiate(thumbTemplate, railParent);
            thumb.gameObject.SetActive(true);
            Image image = thumb.GetComponentsInChildren<Image>(true)[0];
            Building building = entries[i].GetComponent<Building>();
            image.sprite = building.UISprite;
            TooltipTrigger tooltip = thumb.GetComponent<TooltipTrigger>();
            if (tooltip != null)
                tooltip.SetText(!string.IsNullOrEmpty(building.DisplayName) ? building.DisplayName : entries[i].name, "");
            int index = i;
            thumb.Clicked += _ => Show(index);
            thumbs.Add(thumb);
        }
    }

    private void OnEnable()
    {
        if (entries.Count > 0)
            Show(current < 0 ? FirstTower() : current);
    }

    private int FirstTower()
    {
        int index = entries.FindIndex(e => e.GetComponent<Tower>() != null);
        return Mathf.Max(0, index);
    }

    // Meters compare towers with each other: each stat is normalised against its highest value in the palette
    private void ComputeMaxima()
    {
        maxima.Clear();
        foreach (TowerStatKind kind in TowerStatInfo.Grid)
        {
            float max = 0f;
            foreach (GameObject entry in entries)
            {
                Tower tower = entry.GetComponent<Tower>();
                if (tower == null || tower.StatsManager == null)
                    continue;
                max = Mathf.Max(max, TowerStatInfo.Display(kind, TowerStatInfo.Base(tower.StatsManager.Config, kind)));
            }
            maxima[kind] = max;
        }
    }

    private void Show(int index)
    {
        current = index;
        for (int i = 0; i < thumbs.Count; i++)
            thumbs[i].SetOn(i == index);

        GameObject prefab = entries[index];
        Building building = prefab.GetComponent<Building>();
        Tower tower = prefab.GetComponent<Tower>();
        StatsScriptableObject config = tower != null && tower.StatsManager != null ? tower.StatsManager.Config : null;

        heroImage.sprite = building.UISprite;
        number.text = "No. " + UIFormat.TwoDigits(index + 1);
        title.text = building.DisplayName;
        cost.text = GameManager.PriceOf(building.Cost).ToString();
        leftPageNumber.text = "p. " + (index * 2 + 1);
        rightPageNumber.text = "p. " + (index * 2 + 2);

        statsBlock.SetActive(config != null);
        description.gameObject.SetActive(config == null);
        if (config != null)
        {
            for (int i = 0; i < meters.Length && i < TowerStatInfo.Grid.Length; i++)
            {
                TowerStatKind kind = TowerStatInfo.Grid[i];
                meters[i].Set(kind, TowerStatInfo.Base(config, kind), maxima[kind]);
            }
        }
        else
        {
            description.text = building.Description;
        }

        UpgradePath[] paths = tower != null ? tower.UpgradeManager.GetUpgradePaths() : new UpgradePath[0];
        int used = 0;
        for (int p = 0; p < paths.Length; p++)
        {
            if (paths[p] == null || paths[p].UpgradeModules == null || paths[p].UpgradeModules.Length == 0)
                continue;
            while (cards.Count <= used)
                cards.Add(Instantiate(pathTemplate, pathParent));
            cards[used].gameObject.SetActive(true);
            cards[used].Set(p, paths[p], config);
            used++;
        }
        for (int i = used; i < cards.Count; i++)
            cards[i].gameObject.SetActive(false);
        noPaths.gameObject.SetActive(used == 0);
        noPaths.text = tower != null ? "This tower has no upgrades." : "Building blocks have no upgrades.";
    }
}
