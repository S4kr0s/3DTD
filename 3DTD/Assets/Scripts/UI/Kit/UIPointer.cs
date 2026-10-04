using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Whether the pointer is over UI. EventSystem.IsPointerOverGameObject also counts the camera's
// PhysicsRaycaster (towers, blocks, anchors), so it is true over the whole map; this only counts canvases.
public static class UIPointer
{
    private static readonly List<RaycastResult> results = new List<RaycastResult>();
    private static int cachedFrame = -1;
    private static bool cachedValue;

    public static bool IsOverUI()
    {
        if (Time.frameCount == cachedFrame)
            return cachedValue;
        cachedFrame = Time.frameCount;
        cachedValue = IsOverUI(Input.mousePosition);
        return cachedValue;
    }

    public static bool IsOverUI(Vector2 screenPosition)
    {
        EventSystem system = EventSystem.current;
        if (system == null)
            return false;
        PointerEventData data = new PointerEventData(system) { position = screenPosition };
        results.Clear();
        system.RaycastAll(data, results);
        foreach (RaycastResult result in results)
        {
            if (result.module is GraphicRaycaster)
                return true;
        }
        return false;
    }
}
