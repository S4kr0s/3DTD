using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum OptionSetting
{
    AutoWaveOnStart,
    RangeOnHover,
    InterfaceScale,
    WindowMode,
    Resolution,
    FrameLimit,
    VSync,
    Quality,
    RenderScale,
    Bloom,
    AntiAliasing,
    MasterVolume,
    TowersVolume,
    CameraRotateSpeed,
    CameraZoomSpeed,
    CameraPanSpeed,
    InvertCameraY,
}

// One 58 px options row: label on the left, the control for one setting on the right
public class OptionRow : MonoBehaviour
{
    [SerializeField] private OptionSetting setting;
    [SerializeField] private SegmentedControl segmented;
    [SerializeField] private ToggleSwitch toggle;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text sliderValue;
    [SerializeField] private SimpleDropdown dropdown;

    public event Action Changed;

    private GameOptions.Values target;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    public OptionSetting Setting => setting;

    private void Awake()
    {
        if (segmented != null)
            segmented.OnValueChanged += i => Write();
        if (toggle != null)
            toggle.OnValueChanged += b => Write();
        if (slider != null)
            slider.onValueChanged.AddListener(v => Write());
        if (dropdown != null)
            dropdown.OnValueChanged += i => Write();
    }

    // Shows the values of 'values' and writes edits back into it
    public void Bind(GameOptions.Values values)
    {
        target = values;
        switch (setting)
        {
            case OptionSetting.AutoWaveOnStart: toggle.SetIsOn(values.autoWaveOnStart, false); break;
            case OptionSetting.RangeOnHover: toggle.SetIsOn(values.rangeOnHover, false); break;
            case OptionSetting.InterfaceScale: segmented.SetValue(values.interfaceScale, false); break;
            case OptionSetting.WindowMode: segmented.SetValue(values.windowMode, false); break;
            case OptionSetting.Resolution: BindResolution(values); break;
            case OptionSetting.FrameLimit: segmented.SetValue(values.frameLimit, false); break;
            case OptionSetting.VSync: toggle.SetIsOn(values.vSync, false); break;
            case OptionSetting.Quality: segmented.SetValue(Mathf.Clamp(values.quality, 0, segmented.Count - 1), false); break;
            case OptionSetting.RenderScale: slider.SetValueWithoutNotify(values.renderScale); break;
            case OptionSetting.Bloom: toggle.SetIsOn(values.bloom, false); break;
            case OptionSetting.AntiAliasing: segmented.SetValue(values.antiAliasing, false); break;
            case OptionSetting.MasterVolume: slider.SetValueWithoutNotify(values.masterVolume); break;
            case OptionSetting.TowersVolume: slider.SetValueWithoutNotify(values.towersVolume); break;
            case OptionSetting.CameraRotateSpeed: slider.SetValueWithoutNotify(values.cameraRotateSpeed); break;
            case OptionSetting.CameraZoomSpeed: slider.SetValueWithoutNotify(values.cameraZoomSpeed); break;
            case OptionSetting.CameraPanSpeed: slider.SetValueWithoutNotify(values.cameraPanSpeed); break;
            case OptionSetting.InvertCameraY: toggle.SetIsOn(values.invertCameraY, false); break;
        }
        RefreshSliderLabel();
    }

    private void BindResolution(GameOptions.Values values)
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (!resolutions.Contains(size))
                resolutions.Add(size);
        }
        Vector2Int current = values.resolutionWidth > 0 ? new Vector2Int(values.resolutionWidth, values.resolutionHeight) : new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current))
            resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));

        List<string> labels = new List<string>();
        foreach (Vector2Int size in resolutions)
            labels.Add(size.x + " × " + size.y);
        dropdown.SetOptions(labels, resolutions.IndexOf(current));
    }

    private void Write()
    {
        if (target == null)
            return;
        switch (setting)
        {
            case OptionSetting.AutoWaveOnStart: target.autoWaveOnStart = toggle.IsOn; break;
            case OptionSetting.RangeOnHover: target.rangeOnHover = toggle.IsOn; break;
            case OptionSetting.InterfaceScale: target.interfaceScale = segmented.Value; break;
            case OptionSetting.WindowMode: target.windowMode = segmented.Value; break;
            case OptionSetting.Resolution:
                Vector2Int size = resolutions[Mathf.Clamp(dropdown.Value, 0, resolutions.Count - 1)];
                target.resolutionWidth = size.x;
                target.resolutionHeight = size.y;
                break;
            case OptionSetting.FrameLimit: target.frameLimit = segmented.Value; break;
            case OptionSetting.VSync: target.vSync = toggle.IsOn; break;
            case OptionSetting.Quality: target.quality = segmented.Value; break;
            case OptionSetting.RenderScale: target.renderScale = slider.value; break;
            case OptionSetting.Bloom: target.bloom = toggle.IsOn; break;
            case OptionSetting.AntiAliasing: target.antiAliasing = segmented.Value; break;
            case OptionSetting.MasterVolume: target.masterVolume = slider.value; break;
            case OptionSetting.TowersVolume: target.towersVolume = slider.value; break;
            case OptionSetting.CameraRotateSpeed: target.cameraRotateSpeed = slider.value; break;
            case OptionSetting.CameraZoomSpeed: target.cameraZoomSpeed = slider.value; break;
            case OptionSetting.CameraPanSpeed: target.cameraPanSpeed = slider.value; break;
            case OptionSetting.InvertCameraY: target.invertCameraY = toggle.IsOn; break;
        }
        RefreshSliderLabel();
        Changed?.Invoke();
    }

    private void RefreshSliderLabel()
    {
        if (slider != null && sliderValue != null)
            sliderValue.text = UIFormat.Tabular(Mathf.RoundToInt(slider.value * 100f).ToString()) + "%";
    }
}
