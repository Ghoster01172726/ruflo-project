using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Package visuals part of WarehouseBuilder: wires the downloaded box/envelope models
/// into Package.prefab's PackagePickup component and hides the old placeholder cube mesh.
/// See PackagePickup.cs for the runtime per-category model swap logic.
/// </summary>
public static partial class WarehouseBuilder
{
    // Скачанные модели посылок (Poly Pizza, CC0): цельная коробка и конверт —
    // не модульные тайлы, поэтому просто нормализуются по наибольшему габариту
    // (см. PackagePickup.FitVisualScale), как и модель рук.
    private const string PackageBoxModelPath = "Assets/Models/Packages/PackageBox.glb";
    private const string PackageEnvelopeModelPath = "Assets/Models/Packages/Envelope.glb";

    [MenuItem("Warehouse Tools/Setup Package Visuals")]
    public static void SetupPackageVisuals()
    {
        var log = new StringBuilder();
        log.AppendLine("Package Visuals report:");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"WarehouseBuilder: prefab not found at '{PackagePrefabPath}'. Aborting.");
            return;
        }

        GameObject boxModel = AssetDatabase.LoadAssetAtPath<GameObject>(PackageBoxModelPath);
        GameObject envelopeModel = AssetDatabase.LoadAssetAtPath<GameObject>(PackageEnvelopeModelPath);
        if (boxModel == null || envelopeModel == null)
        {
            Debug.LogError($"WarehouseBuilder: package models not found at '{PackageBoxModelPath}' / '{PackageEnvelopeModelPath}'. Aborting.");
            return;
        }

        PackagePickup pickup = prefab.GetComponent<PackagePickup>();
        if (pickup == null)
        {
            Debug.LogError($"WarehouseBuilder: '{PackagePrefabPath}' has no PackagePickup component. Aborting.");
            return;
        }

        var serialized = new SerializedObject(pickup);
        serialized.FindProperty("boxModelPrefab").objectReferenceValue = boxModel;
        serialized.FindProperty("envelopeModelPrefab").objectReferenceValue = envelopeModel;
        serialized.ApplyModifiedProperties();
        log.AppendLine("- Wired boxModelPrefab/envelopeModelPrefab on PackagePickup.");

        // Прежний плейсхолдер — сам меш куба префаба — теперь заменяется на реальную
        // модель, которую PackagePickup создаёт как дочерний объект в рантайме. Меш
        // куба отключаем, а не удаляем, чтобы BoxCollider/Rigidbody на этом же объекте
        // остались нетронутыми.
        var meshRenderer = prefab.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
            log.AppendLine("- Disabled the placeholder cube MeshRenderer (real models render instead).");
        }

        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log(log.ToString());
    }
}
