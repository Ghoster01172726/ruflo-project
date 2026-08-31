using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP post-processing part of WarehouseBuilder: sets up a Global Volume with
/// Vignette and Color Adjustments overrides.
/// </summary>
public static partial class WarehouseBuilder
{
    private const string PostProcessingProfilePath = "Assets/Materials/PostProcessing_Profile.asset";
    private const string GlobalVolumeName = "Global Volume";

    private const float VignetteIntensity = 0.25f;
    private static readonly Color VignetteColor = Color.black;
    private const float ColorAdjustmentsPostExposure = 0.1f;
    private static readonly Color ColorAdjustmentsFilter = new Color(1f, 0.96f, 0.9f); // slightly warm white

    [MenuItem("Warehouse Tools/Add Post Processing")]
    public static void AddPostProcessing()
    {
        var log = new StringBuilder();
        log.AppendLine("Post Processing report:");

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessingProfilePath);
            log.AppendLine($"- Created '{PostProcessingProfilePath}'.");
        }
        else
        {
            log.AppendLine($"- Reused existing profile at '{PostProcessingProfilePath}'.");
        }

        var vignette = GetOrAddOverride<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = VignetteIntensity;
        vignette.color.overrideState = true;
        vignette.color.value = VignetteColor;
        log.AppendLine($"- Configured Vignette (intensity {VignetteIntensity}, color {VignetteColor}).");

        var colorAdjustments = GetOrAddOverride<ColorAdjustments>(profile);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = ColorAdjustmentsPostExposure;
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.colorFilter.value = ColorAdjustmentsFilter;
        log.AppendLine($"- Configured Color Adjustments (post exposure {ColorAdjustmentsPostExposure}, filter {ColorAdjustmentsFilter}).");

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject volumeGO = GameObject.Find(GlobalVolumeName);
        if (volumeGO == null)
        {
            volumeGO = new GameObject(GlobalVolumeName);
            log.AppendLine($"- Created '{GlobalVolumeName}' GameObject.");
        }
        else
        {
            log.AppendLine($"- Reused existing '{GlobalVolumeName}' GameObject.");
        }

        Volume volume = volumeGO.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeGO.AddComponent<Volume>();
            log.AppendLine("- Added Volume component.");
        }

        volume.isGlobal = true;
        volume.weight = 1f;
        // Use sharedProfile (not profile): "profile" is a runtime-only instanced copy that
        // is never serialized, so assigning it here would not persist in the saved scene.
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
        log.AppendLine("- Configured Volume as global (Is Global = true, Weight = 1) and assigned profile.");

        Debug.Log(log.ToString());
    }

    private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
        }

        return component;
    }
}
