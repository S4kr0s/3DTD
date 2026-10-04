using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Chamfered switch: dark track with a grey knob on the left when off, orange track with a white knob on the
// right when on, 40 % opacity when disabled.
[RequireComponent(typeof(CanvasGroup))]
public class ToggleSwitch : UnityEngine.UI.Selectable, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private BevelGraphic track;
    [SerializeField] private BevelGraphic knob;
    [SerializeField] private float knobInset = 4f;
    [SerializeField] private bool isOn;

    public event Action<bool> OnValueChanged;

    private CanvasGroup canvasGroup;

    public bool IsOn => isOn;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
        canvasGroup = GetComponent<CanvasGroup>();
        Refresh();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Refresh();
    }

    public void SetIsOn(bool value, bool notify)
    {
        if (isOn == value)
            return;
        isOn = value;
        Refresh();
        if (notify)
            OnValueChanged?.Invoke(isOn);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        Refresh();
    }

    private void Refresh()
    {
        if (track != null)
            track.SetStyle(isOn ? BevelStyle.TrackOn : BevelStyle.TrackOff);
        if (knob != null)
        {
            knob.SetStyle(isOn ? BevelStyle.KnobOn : BevelStyle.KnobOff);
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(isOn ? 1f : 0f, 0.5f);
            knobRect.pivot = new Vector2(isOn ? 1f : 0f, 0.5f);
            knobRect.anchoredPosition = new Vector2(isOn ? -knobInset : knobInset, 0f);
        }
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = IsInteractable() ? 1f : 0.4f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Toggle();
            BevelButton.ReleaseFocus(gameObject);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Toggle();
    }

    private void Toggle()
    {
        if (IsActive() && IsInteractable())
            SetIsOn(!isOn, true);
    }
}
