using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

// Temporary diagnostic: drives the real StandaloneInputModule with a fake mouse over a menu button and logs
// what the EventSystem raycast hits and whether the button gets pointer events.
//   Unity -batchmode -projectPath ... -executeMethod PointerProbe.Run -logFile <log>
[InitializeOnLoad]
public static class PointerProbe
{
    private const string ActiveKey = "PointerProbe.Active";
    private const string StartedKey = "PointerProbe.Started";
    private const string TickKey = "PointerProbe.Tick";
    private const string DoneKey = "PointerProbe.Done";

    private static FakeInput fake;
    private static UnityEngine.UI.Selectable target;
    private static ProbeHandler handler;

    static PointerProbe()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(StartedKey, false);
        SessionState.SetBool(DoneKey, false);
        SessionState.SetInt(TickKey, 0);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(DoneKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
                return;
            }
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                string scene = "Assets/Scenes/MainMenuLevel.unity";
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-probeScene")
                        scene = args[i + 1];
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            return;
        }

        int tick = SessionState.GetInt(TickKey, 0) + 1;
        SessionState.SetInt(TickKey, tick);
        EventSystem system = EventSystem.current;

        if (tick == 120)
            Setup(system);
        if (tick > 120 && fake != null)
        {
            fake.Pressed = tick == 150;
            fake.Released = tick == 151;
            fake.Held = tick == 150;
            // Batch mode has no focus, so the EventSystem skips its modules; run the module ourselves
            // (Process() checks the focus too, so call the mouse handling directly)
            if (system != null && !system.isFocused && system.currentInputModule != null)
                typeof(StandaloneInputModule).GetMethod("ProcessMouseEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(int) }, null)
                    .Invoke(system.currentInputModule, new object[] { 0 });
        }
        if (tick == 200)
        {
            Debug.Log("PROBE result: enter=" + (handler != null && handler.Entered) + " click=" + (handler != null && handler.Clicked)
                + " focused=" + (system != null && system.isFocused) + " overUI=" + (system != null && system.IsPointerOverGameObject()));
            SessionState.SetBool(DoneKey, true);
            EditorApplication.isPlaying = false;
        }
    }

    private static void Setup(EventSystem system)
    {
        StringBuilder log = new StringBuilder("PROBE setup: ");
        log.Append("EventSystem=").Append(system != null ? system.name : "NONE");
        if (system == null)
        {
            Debug.Log(log.ToString());
            return;
        }
        log.Append(" enabled=").Append(system.isActiveAndEnabled).Append(" module=").Append(system.currentInputModule != null ? system.currentInputModule.GetType().Name : "none");
        log.Append(" eventSystems=").Append(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length);
        foreach (BaseRaycaster raycaster in Object.FindObjectsByType<BaseRaycaster>(FindObjectsInactive.Exclude))
            log.Append("\n  raycaster ").Append(raycaster.GetType().Name).Append(" on ").Append(Path(raycaster.transform)).Append(" enabled=").Append(raycaster.isActiveAndEnabled)
                .Append(" sortOrder=").Append(raycaster.sortOrderPriority).Append(" renderOrder=").Append(raycaster.renderOrderPriority);

        foreach (UnityEngine.UI.Selectable selectable in Object.FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsInactive.Exclude))
        {
            if (!selectable.IsInteractable() || !(selectable.transform is RectTransform rect))
                continue;
            Canvas canvas = selectable.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled)
                continue;
            target = selectable;
            break;
        }
        if (target == null)
        {
            Debug.Log(log.Append("\n  no interactable button").ToString());
            return;
        }

        RectTransform targetRect = (RectTransform)target.transform;
        Canvas root = target.GetComponentInParent<Canvas>().rootCanvas;
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(cam, targetRect.TransformPoint(targetRect.rect.center));
        log.Append("\n  target ").Append(Path(target.transform)).Append(" at ").Append(point).Append(" screen ").Append(Screen.width).Append('x').Append(Screen.height);

        List<RaycastResult> results = new List<RaycastResult>();
        system.RaycastAll(new PointerEventData(system) { position = point }, results);
        log.Append("\n  hits ").Append(results.Count);
        for (int i = 0; i < results.Count && i < 8; i++)
            log.Append("\n    ").Append(i).Append(' ').Append(Path(results[i].gameObject.transform)).Append(" via ").Append(results[i].module.GetType().Name)
                .Append(" dist=").Append(results[i].distance.ToString("F2"));

        handler = target.gameObject.AddComponent<ProbeHandler>();
        if (system.currentInputModule != null)
        {
            fake = system.currentInputModule.gameObject.AddComponent<FakeInput>();
            fake.Position = point;
            system.currentInputModule.inputOverride = fake;
        }
        Debug.Log(log.ToString());
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }
}

public class FakeInput : BaseInput
{
    public Vector2 Position;
    public bool Pressed, Released, Held;
    public override Vector2 mousePosition => Position;
    public override bool mousePresent => true;
    public override bool GetMouseButtonDown(int button) => button == 0 && Pressed;
    public override bool GetMouseButtonUp(int button) => button == 0 && Released;
    public override bool GetMouseButton(int button) => button == 0 && Held;
    public override Vector2 mouseScrollDelta => Vector2.zero;
    public override bool touchSupported => false;
    public override int touchCount => 0;
}

public class ProbeHandler : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public bool Entered, Clicked;
    public void OnPointerEnter(PointerEventData eventData) { Entered = true; }
    public void OnPointerClick(PointerEventData eventData) { Clicked = true; }
}
