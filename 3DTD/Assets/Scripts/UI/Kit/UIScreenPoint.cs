using UnityEngine;

// World point -> anchored position under a UI parent, for screen-space overlay and camera canvases alike
public static class UIScreenPoint
{
    public static bool TryPlace(RectTransform element, Camera worldCamera, Vector3 world, Vector2 offset)
    {
        if (worldCamera == null)
            return false;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return false;

        RectTransform parent = (RectTransform)element.parent;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            return false;

        element.anchorMin = element.anchorMax = new Vector2(0.5f, 0.5f);
        element.anchoredPosition = local + offset - parent.rect.center;
        return true;
    }
}
