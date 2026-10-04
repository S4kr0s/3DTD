using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Upgrades (redesign B5): one skill tree per category, top-down by tier with right-angle connectors.
// Selecting a node fills the detail card (effects, requirements, cost) with the Unlock CTA.
public class MetaUpgradesScreen : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private SegmentedControl tabs;
    [SerializeField] private TMP_Text[] tabLabels = new TMP_Text[4];
    [SerializeField] private Image[] tabIcons = new Image[4];
    [SerializeField] private TMP_Text researchText;

    [Header("Tree")]
    [SerializeField] private RectTransform treeArea;
    [SerializeField] private PathGraphic connectors;
    [SerializeField] private SkillNode nodeTemplate;
    [SerializeField] private SkillNode capstoneTemplate;
    [Tooltip("Node centres in the tree area, from its top-left corner")]
    [SerializeField] private float[] columnX = { 100f, 240f, 380f };
    [SerializeField] private float[] tierY = { 50f, 150f, 250f, 362f };

    [Header("Detail card")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailCaption;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private RectTransform effectList;
    [SerializeField] private EffectRow effectTemplate;
    [SerializeField] private GameObject requiresRow;
    [SerializeField] private TMP_Text requiresText;
    [SerializeField] private Image requiresIcon;
    [SerializeField] private GameObject costChip;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private BevelButton unlockButton;
    [SerializeField] private TMP_Text unlockLabel;

    [Header("Capstone card")]
    [SerializeField] private GameObject capstoneCard;
    [SerializeField] private TMP_Text capstoneTitle;
    [SerializeField] private TMP_Text capstoneHint;
    [SerializeField] private Image capstoneIcon;

    [Header("Progress")]
    [SerializeField] private TMP_Text treeCaption;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text progressText;

    private readonly List<SkillNode> nodes = new List<SkillNode>();
    private readonly List<EffectRow> effects = new List<EffectRow>();
    private MetaUpgradeTree data;
    private MetaUpgradeTree.SkillTree tree;
    private MetaUpgradeTree.Node selected;

    private void Awake()
    {
        data = MetaUpgradeTree.Instance;
        nodeTemplate.gameObject.SetActive(false);
        capstoneTemplate.gameObject.SetActive(false);
        effectTemplate.gameObject.SetActive(false);
        tabs.OnValueChanged += ShowTree;
        unlockButton.Clicked += _ => Unlock();
    }

    private void OnEnable()
    {
        if (data == null)
            return;
        UITheme theme = UITheme.Current;
        for (int i = 0; i < tabLabels.Length; i++)
        {
            bool used = i < data.trees.Count;
            if (i < tabs.Count)
                tabs.Items[i].gameObject.SetActive(used);
            if (!used)
                continue;
            tabLabels[i].text = data.trees[i].displayName;
            tabIcons[i].sprite = theme.Icon(data.trees[i].icon);
        }
        ShowTree(Mathf.Max(0, tabs.Value));
    }

    private void ShowTree(int index)
    {
        if (data == null || data.trees.Count == 0)
            return;
        index = Mathf.Clamp(index, 0, data.trees.Count - 1);
        tabs.SetValue(index, false);
        tree = data.trees[index];
        selected = null;

        foreach (SkillNode node in nodes)
            Destroy(node.gameObject);
        nodes.Clear();

        foreach (MetaUpgradeTree.Node node in tree.nodes)
        {
            SkillNode view = Instantiate(node.capstone ? capstoneTemplate : nodeTemplate, treeArea);
            view.gameObject.SetActive(true);
            view.Setup(node);
            view.Rect.anchorMin = view.Rect.anchorMax = new Vector2(0f, 1f);
            view.Rect.anchoredPosition = NodeCenter(node);
            view.Clicked += v => Select(v.Node);
            nodes.Add(view);
        }

        // Start on the first node that can be bought, else the first one
        MetaUpgradeTree.Node first = tree.nodes.Find(n => !PlayerProgress.OwnsNode(n.id) && MetaUpgradeTree.RequirementsMet(n));
        Select(first ?? (tree.nodes.Count > 0 ? tree.nodes[0] : null));
    }

    private Vector2 NodeCenter(MetaUpgradeTree.Node node)
    {
        float x = columnX[Mathf.Clamp(node.column, 0, columnX.Length - 1)];
        float y = tierY[Mathf.Clamp(node.tier - 1, 0, tierY.Length - 1)];
        return new Vector2(x, -y);
    }

    private void Select(MetaUpgradeTree.Node node)
    {
        selected = node;
        Refresh();
    }

    private void Refresh()
    {
        UITheme theme = UITheme.Current;
        researchText.text = UIFormat.Tabular(PlayerProgress.Research);
        foreach (SkillNode view in nodes)
            view.Refresh(view.Node == selected);
        DrawConnectors();

        int owned = tree.nodes.FindAll(n => PlayerProgress.OwnsNode(n.id)).Count;
        treeCaption.text = tree.displayName + " tree";
        progressText.text = owned + " / " + tree.nodes.Count;
        progressFill.anchorMax = new Vector2(tree.nodes.Count > 0 ? owned / (float)tree.nodes.Count : 0f, 1f);

        MetaUpgradeTree.Node capstone = tree.nodes.Find(n => n.capstone);
        capstoneCard.SetActive(capstone != null && capstone != selected);
        if (capstone != null)
        {
            capstoneTitle.text = capstone.displayName;
            capstoneHint.text = PlayerProgress.OwnsNode(capstone.id) ? "Owned" : "Needs all Tier " + UIFormat.Roman(capstone.tier - 1);
            capstoneIcon.sprite = theme.Icon(capstone.icon);
        }

        ShowDetail(selected);
    }

    private void ShowDetail(MetaUpgradeTree.Node node)
    {
        UITheme theme = UITheme.Current;
        if (node == null)
            return;

        bool owned = PlayerProgress.OwnsNode(node.id);
        bool requirementsMet = MetaUpgradeTree.RequirementsMet(node);
        detailIcon.sprite = theme.Icon(node.icon);
        detailCaption.text = (node.capstone ? "Capstone" : "Tier " + UIFormat.Roman(node.tier)) + " · " + tree.displayName;
        detailTitle.text = node.displayName;

        while (effects.Count < node.effects.Count)
            effects.Add(Instantiate(effectTemplate, effectList));
        for (int i = 0; i < effects.Count; i++)
        {
            bool used = i < node.effects.Count;
            effects[i].gameObject.SetActive(used);
            if (used)
                effects[i].Set(node.effects[i]);
        }

        requiresRow.SetActive(node.requires.Count > 0);
        if (node.requires.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (string id in node.requires)
            {
                MetaUpgradeTree.Node required = data.FindNode(id, out _);
                names.Add(required != null ? required.displayName : id);
            }
            requiresText.text = node.capstone && names.Count > 2 ? "All Tier " + UIFormat.Roman(node.tier - 1) + " nodes" : string.Join(", ", names);
            requiresText.color = requirementsMet ? theme.accentText : theme.textDim;
            requiresIcon.sprite = theme.Icon(requirementsMet ? "ic_check" : "ic_lock");
            requiresIcon.color = requiresText.color;
        }

        costChip.SetActive(!owned);
        costText.text = node.cost.ToString();
        costText.color = PlayerProgress.Research >= node.cost ? theme.text : theme.cantAfford;
        unlockLabel.text = owned ? "Owned" : "Unlock";
        unlockButton.SetInteractable(!owned && requirementsMet && PlayerProgress.Research >= node.cost);
    }

    // Owned path = solid orange, next unlockable = dashed lilac, the rest faint
    private void DrawConnectors()
    {
        connectors.Clear();
        Vector2 size = treeArea.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        List<Vector2> points = new List<Vector2>(4);
        foreach (MetaUpgradeTree.Node node in tree.nodes)
        {
            foreach (string id in node.requires)
            {
                MetaUpgradeTree.Node parent = tree.nodes.Find(n => n.id == id);
                if (parent == null)
                    continue;
                Vector2 from = NodeCenter(parent);
                Vector2 to = NodeCenter(node);
                float midY = (from.y + to.y) * 0.5f;
                points.Clear();
                points.Add(Normalise(from, size));
                points.Add(Normalise(new Vector2(from.x, midY), size));
                points.Add(Normalise(new Vector2(to.x, midY), size));
                points.Add(Normalise(to, size));

                bool parentOwned = PlayerProgress.OwnsNode(parent.id);
                bool childOwned = PlayerProgress.OwnsNode(node.id);
                if (parentOwned && childOwned)
                    connectors.AddLine(points, 3f, UITheme.Current.accent, 0f, 0f, false);
                else if (parentOwned)
                    connectors.AddLine(points, 1.6f, BevelStyles.Rgba(200, 195, 255, 0.6f), 5f, 4f, false);
                else
                    connectors.AddLine(points, 1.6f, BevelStyles.Rgba(200, 195, 255, 0.18f), 0f, 0f, false);
            }
        }
    }

    // PathGraphic works in 0..1 of its rect (y up); node positions are from the tree's top-left
    private static Vector2 Normalise(Vector2 fromTopLeft, Vector2 size)
    {
        return new Vector2(fromTopLeft.x / size.x, 1f + fromTopLeft.y / size.y);
    }

    private void Unlock()
    {
        if (selected == null)
            return;
        if (PlayerProgress.TryUnlockNode(selected.id, selected.cost))
            Refresh();
    }
}
