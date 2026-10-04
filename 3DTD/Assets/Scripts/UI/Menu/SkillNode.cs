using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hexagon node of a meta-upgrade tree. Owned = filled orange, available = lilac outline, locked = dim with a
// lock icon; the capstone is larger with a pink-orange rim. The selected node gets an orange rim and halo.
public class SkillNode : MonoBehaviour
{
    public enum State
    {
        Locked,
        Available,
        Owned,
    }

    [SerializeField] private BevelButton button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Graphic halo;
    [SerializeField] private TooltipTrigger tooltip;

    public event Action<SkillNode> Clicked;

    public MetaUpgradeTree.Node Node { get; private set; }
    public State CurrentState { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        button.Clicked += _ => Clicked?.Invoke(this);
        if (tooltip != null)
            tooltip.Provider = TooltipText;
    }

    private (string title, string body) TooltipText()
    {
        if (Node == null)
            return ("", "");
        List<string> lines = new List<string>();
        foreach (MetaUpgradeTree.Effect effect in Node.effects)
        {
            EffectRow.Describe(effect, out _, out string name, out string value);
            lines.Add(name + " " + value + (string.IsNullOrEmpty(effect.scope) ? "" : " · " + effect.scope));
        }
        UITheme theme = UITheme.Current;
        string accent = "#" + ColorUtility.ToHtmlStringRGB(theme.accentText);
        string dim = "#" + ColorUtility.ToHtmlStringRGB(theme.textMuted);
        if (CurrentState == State.Owned)
            lines.Add("<color=" + accent + ">Unlocked</color>");
        else if (CurrentState == State.Available)
            lines.Add("<color=" + accent + ">" + Node.cost + " Research to unlock</color>");
        else
            lines.Add("<color=" + dim + ">" + Node.cost + " Research · needs the nodes above first</color>");
        return (Node.displayName, string.Join("\n", lines));
    }

    public void Setup(MetaUpgradeTree.Node node)
    {
        Node = node;
        label.text = node.displayName;
    }

    public void Refresh(bool selected)
    {
        UITheme theme = UITheme.Current;
        bool owned = PlayerProgress.OwnsNode(Node.id);
        bool available = !owned && MetaUpgradeTree.RequirementsMet(Node);
        CurrentState = owned ? State.Owned : available ? State.Available : State.Locked;

        BevelStyle normal;
        if (owned)
            normal = BevelStyle.NodeOwned;
        else if (Node.capstone)
            normal = BevelStyle.NodeCapstone;
        else if (available)
            normal = BevelStyle.NodeAvailable;
        else
            normal = BevelStyle.NodeLocked;
        BevelStyle on = owned ? BevelStyle.NodeOwned : BevelStyle.NodeSelected;
        button.SetStyles(normal, normal, normal, normal, on);
        button.SetOn(selected);

        icon.sprite = theme.Icon(CurrentState == State.Locked && !Node.capstone ? "ic_lock" : Node.icon);
        icon.color = owned ? theme.textOnAccent : CurrentState == State.Locked ? theme.textDim : Color.white;
        label.color = CurrentState == State.Locked ? theme.textLocked : theme.text;
        if (halo != null)
            halo.gameObject.SetActive(selected && !owned);
    }
}
