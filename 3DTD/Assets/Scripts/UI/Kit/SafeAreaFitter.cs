using UnityEngine;

// Fits this RectTransform to Screen.safeArea so HUD corners stay clear of notches and rounded corners
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class SafeAreaFitter : MonoBehaviour
{
    private Rect appliedArea;
    private Vector2Int appliedScreen;

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != appliedArea || appliedScreen.x != Screen.width || appliedScreen.y != Screen.height)
            Apply();
    }

    private void Apply()
    {
        Rect area = Screen.safeArea;
        appliedArea = area;
        appliedScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
