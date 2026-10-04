using UnityEngine;
using UnityEngine.EventSystems;

// Input helpers for the batch-mode UI capture and play test: clicks through the EventSystem, pad picking
public static class UIEditorDrive
{
    // Free pad whose screen position is closest to the given viewport point
    public static AnchorPoint PickPad(Vector2 viewport)
    {
        Camera camera = Camera.main;
        AnchorPoint best = null;
        float bestDistance = float.MaxValue;
        foreach (AnchorPoint pad in Object.FindObjectsByType<AnchorPoint>(FindObjectsInactive.Exclude))
        {
            if (!pad.IsFree() || Vector3.Dot(pad.transform.forward, Vector3.up) < 0.9f)
                continue;
            Vector3 screen = camera.WorldToViewportPoint(pad.transform.position);
            if (screen.z <= 0f)
                continue;
            float distance = Vector2.Distance(screen, viewport);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pad;
            }
        }
        return best;
    }

    public static void Click(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("UICapture: nothing to click");
            return;
        }
        PointerEventData data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    public static GameObject FindInactive<T>(string name, string parentName) where T : Component
    {
        foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
        {
            if (component.name == name && component.transform.parent != null && component.transform.parent.name == parentName)
                return component.gameObject;
        }
        return null;
    }

}
