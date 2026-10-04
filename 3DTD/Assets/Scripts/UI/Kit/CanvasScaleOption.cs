using UnityEngine;
using UnityEngine.UI;

// Scales the UI uniformly from the 1100 x 611 reference: matched to screen height on wide screens, to
// width on narrower ones (CanvasScaler "Expand"), times the player's interface scale option.
[RequireComponent(typeof(CanvasScaler))]
public class CanvasScaleOption : MonoBehaviour
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1100f, 611f);

    private CanvasScaler scaler;

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Apply();
    }

    private void OnEnable()
    {
        GameOptions.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameOptions.Changed -= Apply;
    }

    private void Apply()
    {
        if (scaler != null)
            scaler.referenceResolution = ReferenceResolution / GameOptions.InterfaceScale;
    }
}
