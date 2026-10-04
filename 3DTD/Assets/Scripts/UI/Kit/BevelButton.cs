using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Button for every interactive bevel element: key-cap CTA, glass buttons, nav entries, tabs, segmented items,
// tiles and upgrade rows. The plate swaps bevel styles per state; "on" marks the active tab / nav entry /
// selected tile. Hovering lights the plate up (BevelGraphic.Highlight) and, per button type, scales it or
// slides its content; the animation runs on unscaled time so it also works in the pause menu.
// Note the game has its own global Selectable class, hence the qualified base type.
public class BevelButton : UnityEngine.UI.Selectable, IPointerClickHandler, ISubmitHandler
{
    [Serializable]
    public class ContentTint
    {
        public Graphic graphic;
        public Color normal = Color.white;
        public Color on = Color.white;
        public Color disabled = new Color(1f, 1f, 1f, 0.4f);
    }

    [Header("Plate")]
    [SerializeField] private BevelGraphic plate;
    [SerializeField] private BevelStyle normalStyle = BevelStyle.GlassButton;
    [SerializeField] private BevelStyle highlightedStyle = BevelStyle.GlassButton;
    [SerializeField] private BevelStyle pressedStyle = BevelStyle.GlassButtonPressed;
    [SerializeField] private BevelStyle disabledStyle = BevelStyle.GlassButton;
    [SerializeField] private BevelStyle onStyle = BevelStyle.GlassButton;
    [SerializeField] private BevelStyle onHighlightedStyle = BevelStyle.None;
    [Tooltip("Plate alpha while disabled")]
    [SerializeField] private float disabledAlpha = 1f;

    [Header("Content")]
    [Tooltip("Moved down while pressed (the label 'drops' into the key)")]
    [SerializeField] private RectTransform content;
    [SerializeField] private float pressedOffset = 2f;
    [SerializeField] private List<ContentTint> tints = new List<ContentTint>();
    [SerializeField] private GameObject[] showWhenOn = new GameObject[0];
    [SerializeField] private GameObject[] showWhenOff = new GameObject[0];
    [Tooltip("Soft glow behind a key-cap; hidden while disabled")]
    [SerializeField] private Graphic glow;

    [Header("Hover")]
    [Tooltip("Scale of the whole button while hovered (tiles, cards, key-caps)")]
    [SerializeField] private float hoverScale = 1f;
    [Tooltip("Content offset while hovered (menu entries slide right)")]
    [SerializeField] private Vector2 hoverShift = Vector2.zero;
    [Tooltip("Strength of the plate highlight while hovered")]
    [SerializeField] private float hoverGlow = 1f;

    [Header("State")]
    [SerializeField] private bool isOn;

    public UnityEvent onClick = new UnityEvent();
    public event Action<BevelButton> Clicked;
    public event Action<BevelButton, bool> HoverChanged;

    private Vector2 contentRestPosition;
    private bool contentRestCaptured;
    private SelectionState lastState = SelectionState.Normal;
    private bool pressedNow;
    private bool disabledNow;
    private float hover;
    private float hoverTarget;
    private const float HoverSpeed = 9f;

    public bool IsOn => isOn;
    public BevelGraphic Plate => plate;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
        CaptureContentRest();
    }

    private void CaptureContentRest()
    {
        if (content != null && !contentRestCaptured)
        {
            contentRestPosition = content.anchoredPosition;
            contentRestCaptured = true;
        }
    }

    public void SetOn(bool on)
    {
        if (isOn == on)
            return;
        isOn = on;
        Refresh(true);
    }

    public void SetStyles(BevelStyle normal, BevelStyle highlighted, BevelStyle pressed, BevelStyle disabled, BevelStyle on)
    {
        normalStyle = normal;
        highlightedStyle = highlighted;
        pressedStyle = pressed;
        disabledStyle = disabled;
        onStyle = on;
        Refresh(true);
    }

    public void SetInteractable(bool value)
    {
        if (interactable == value)
            return;
        interactable = value;
        Refresh(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Refresh(true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // A button hidden while hovered comes back at rest
        hover = hoverTarget = 0f;
        ApplyMotion();
    }

    private void Update()
    {
        if (Mathf.Approximately(hover, hoverTarget))
            return;
        hover = Mathf.MoveTowards(hover, hoverTarget, Time.unscaledDeltaTime * HoverSpeed);
        ApplyMotion();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (state != lastState && (state == SelectionState.Highlighted || lastState == SelectionState.Highlighted))
            HoverChanged?.Invoke(this, state == SelectionState.Highlighted);
        lastState = state;
        Apply(state);
    }

    private void Refresh(bool instant)
    {
        Apply(IsInteractable() ? lastState : SelectionState.Disabled);
    }

    private void Apply(SelectionState state)
    {
        bool disabled = state == SelectionState.Disabled || !IsInteractable();
        bool pressed = !disabled && state == SelectionState.Pressed;
        bool highlighted = !disabled && (state == SelectionState.Highlighted || state == SelectionState.Selected);

        if (plate != null)
        {
            BevelStyle style;
            if (disabled)
                style = disabledStyle;
            else if (pressed)
                style = pressedStyle;
            else if (isOn)
                style = highlighted && onHighlightedStyle != BevelStyle.None ? onHighlightedStyle : onStyle;
            else
                style = highlighted ? highlightedStyle : normalStyle;

            plate.SetStyle(style);
            Color plateColor = plate.color;
            plateColor.a = disabled ? disabledAlpha : 1f;
            plate.color = plateColor;
        }

        pressedNow = pressed;
        disabledNow = disabled;
        hoverTarget = highlighted || pressed ? 1f : 0f;
        if (!Application.isPlaying || !isActiveAndEnabled)
            hover = hoverTarget;
        ApplyMotion();

        foreach (GameObject go in showWhenOn)
            if (go != null && go.activeSelf != isOn) go.SetActive(isOn);
        foreach (GameObject go in showWhenOff)
            if (go != null && go.activeSelf == isOn) go.SetActive(!isOn);

        if (glow != null && glow.enabled == disabled)
            glow.enabled = !disabled;
    }

    // The continuous part of the state: hover highlight, scale, content offset and content tints
    private void ApplyMotion()
    {
        float eased = hover * hover * (3f - 2f * hover);
        if (plate != null)
            plate.Highlight = disabledNow ? 0f : eased * hoverGlow;
        if (!Mathf.Approximately(hoverScale, 1f))
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, hoverScale, disabledNow ? 0f : eased);

        CaptureContentRest();
        if (content != null)
        {
            Vector2 offset = pressedNow ? new Vector2(0f, -pressedOffset) : Vector2.zero;
            if (!disabledNow)
                offset += hoverShift * eased;
            content.anchoredPosition = contentRestPosition + offset;
        }

        foreach (ContentTint tint in tints)
        {
            if (tint.graphic == null)
                continue;
            Color color = disabledNow ? tint.disabled : (isOn ? tint.on : tint.normal);
            // Light labels brighten while hovered; dark labels on orange stay as they are
            if (!disabledNow && color.grayscale > 0.5f)
                color = Color.Lerp(color, new Color(1f, 1f, 1f, color.a), 0.45f * eased);
            tint.graphic.color = color;
        }
    }

    // Recolours one content graphic, e.g. a price that turns red when it can't be afforded
    public void SetTint(Graphic graphic, Color normal, Color on, Color disabled)
    {
        ContentTint tint = tints.Find(t => t.graphic == graphic);
        if (tint == null)
        {
            tint = new ContentTint { graphic = graphic };
            tints.Add(tint);
        }
        tint.normal = normal;
        tint.on = on;
        tint.disabled = disabled;
        Refresh(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        Press();
        ReleaseFocus(gameObject);
    }

    // A clicked button would otherwise keep the EventSystem focus, so Space (camera reset) or the arrow
    // keys (camera pan) would re-click it or move the focus around
    public static void ReleaseFocus(GameObject clicked)
    {
        EventSystem system = EventSystem.current;
        if (system != null && system.currentSelectedGameObject == clicked)
            system.SetSelectedGameObject(null);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Press();
    }

    private void Press()
    {
        if (!IsActive() || !IsInteractable())
            return;
        onClick.Invoke();
        Clicked?.Invoke(this);
    }
}
