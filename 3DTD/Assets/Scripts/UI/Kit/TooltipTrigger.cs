using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Hover text for any UI element (resources, buttons, stat cells, level cards, options...). Shows the
// canvas's TooltipService after a short delay and hides it when the pointer leaves. The text is either
// set in the inspector / by SetText, or produced on demand by Provider for values that change.
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string title;
    [TextArea]
    [SerializeField] private string body;
    [SerializeField] private TooltipService.Side side = TooltipService.Side.Below;
    [Tooltip("Optional: the tooltip is placed beside this rect (e.g. its panel) instead of the element")]
    [SerializeField] private RectTransform outside;
    [Tooltip("Seconds of unscaled time before the tooltip opens")]
    [SerializeField] private float delay = 0.3f;

    // Returns (title, body); an empty title and body show nothing
    public Func<(string title, string body)> Provider { get; set; }

    private bool hovered;
    private bool shown;
    private float showAt;
    private TooltipService service;

    public string Title => title;
    public string Body => body;

    public void SetText(string newTitle, string newBody)
    {
        title = newTitle;
        body = newBody;
        if (shown)
            Show();
    }

    public void Configure(TooltipService.Side newSide, RectTransform newOutside = null)
    {
        side = newSide;
        outside = newOutside;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        showAt = Time.unscaledTime + delay;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Hide();
    }

    private void Update()
    {
        if (hovered && !shown && Time.unscaledTime >= showAt)
            Show();
    }

    private void OnDisable()
    {
        hovered = false;
        Hide();
    }

    // Opens the tooltip without waiting for the pointer (screenshots, play tests)
    public void ShowNow()
    {
        Show();
    }

    // Re-reads the provider while the tooltip is open (e.g. after buying the hovered upgrade)
    public void Refresh()
    {
        if (shown)
            Show();
    }

    private void Show()
    {
        string titleText = title;
        string bodyText = body;
        if (Provider != null)
            (titleText, bodyText) = Provider();
        if (service == null)
            service = TooltipService.For(transform);
        if (service == null)
            return;
        if (string.IsNullOrEmpty(titleText) && string.IsNullOrEmpty(bodyText))
        {
            Hide();
            return;
        }
        service.Show((RectTransform)transform, side, titleText, bodyText, outside);
        shown = true;
    }

    private void Hide()
    {
        if (shown && service != null)
            service.Hide((RectTransform)transform);
        shown = false;
    }
}
