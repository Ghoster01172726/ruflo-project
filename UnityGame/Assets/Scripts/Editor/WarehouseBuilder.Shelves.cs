using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shelf-sorting part of WarehouseBuilder: builds wall-mounted shelf racks (posts + boards)
/// and wires them up as ShelfSlot trigger zones, one per package category. Also removes the
/// older floor-based SortingZone mats, which are no longer used now that shelves are the
/// sorting destination.
/// </summary>
public static partial class WarehouseBuilder
{
    private const float ShelfWidth = 1.6f;
    private const float ShelfDepth = 0.5f;
    private const float ShelfRackHeight = 2f;
    private const float ShelfPostThickness = 0.08f;
    private const float ShelfBoardThickness = 0.06f;
    // Расстояние от стены (Wall_North), на котором стоят стеллажи.
    private const float ShelfWallMargin = 0.45f;

    private static readonly float[] ShelfBoardHeights = { 0.5f, 1.1f, 1.7f };
    private static readonly float[] ShelfOffsetsX = { -3f, 0f, 3f };
    private static readonly string[] ShelfUnitNames = { "ShelfUnit_1", "ShelfUnit_2", "ShelfUnit_3" };
    // Порядок соответствует ShelfUnitNames/ShelfOffsetsX: какая категория посылок
    // принимается каждым из трёх стеллажей.
    private static readonly PackageCategory[] ShelfCategories =
    {
        PackageCategory.Heavy, PackageCategory.Normal, PackageCategory.Fragile
    };

    private static readonly string[] SortingZoneNames =
    {
        "SortingZone_Fragile", "SortingZone_Normal", "SortingZone_Heavy"
    };

    [MenuItem("Warehouse Tools/Add Shelves")]
    public static void AddShelves()
    {
        var log = new StringBuilder();
        log.AppendLine("Add Shelves report:");

        GameObject northWall = GameObject.Find("Wall_North");
        float wallZ = northWall != null ? northWall.transform.position.z : 5f;
        float shelfZ = wallZ - ShelfWallMargin;

        Material shelfMat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        if (shelfMat == null)
        {
            log.AppendLine($"- Warning: '{WallMaterialPath}' not found; shelves use the default material.");
        }

        for (int i = 0; i < ShelfUnitNames.Length; i++)
        {
            BuildShelfUnit(ShelfUnitNames[i], new Vector3(ShelfOffsetsX[i], 0f, shelfZ), shelfMat, log);
        }

        Debug.Log(log.ToString());
    }

    private static void BuildShelfUnit(string name, Vector3 position, Material material, StringBuilder log)
    {
        GameObject root = GameObject.Find(name);
        bool created = root == null;
        if (created)
        {
            root = new GameObject(name);
        }
        root.transform.position = position;

        float postOffsetX = ShelfWidth / 2f - ShelfPostThickness / 2f;
        float postOffsetZ = ShelfDepth / 2f - ShelfPostThickness / 2f;
        Vector3 postScale = new Vector3(ShelfPostThickness, ShelfRackHeight, ShelfPostThickness);
        Vector3 postPosition = new Vector3(0f, ShelfRackHeight / 2f, 0f);

        CreateShelfPart(root.transform, "Post_FrontLeft", postPosition + new Vector3(-postOffsetX, 0f, -postOffsetZ), postScale, material);
        CreateShelfPart(root.transform, "Post_FrontRight", postPosition + new Vector3(postOffsetX, 0f, -postOffsetZ), postScale, material);
        CreateShelfPart(root.transform, "Post_BackLeft", postPosition + new Vector3(-postOffsetX, 0f, postOffsetZ), postScale, material);
        CreateShelfPart(root.transform, "Post_BackRight", postPosition + new Vector3(postOffsetX, 0f, postOffsetZ), postScale, material);

        Vector3 boardScale = new Vector3(ShelfWidth, ShelfBoardThickness, ShelfDepth);
        for (int i = 0; i < ShelfBoardHeights.Length; i++)
        {
            CreateShelfPart(root.transform, $"Board_{i + 1}", new Vector3(0f, ShelfBoardHeights[i], 0f), boardScale, material);
        }

        log.AppendLine($"- {(created ? "Created" : "Updated")} '{name}' at {position} (4 posts + {ShelfBoardHeights.Length} boards).");
    }

    private static void CreateShelfPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, worldPositionStays: false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        if (material != null)
        {
            AssignMaterial(part, material);
        }
    }

    [MenuItem("Warehouse Tools/Setup Shelf Sorting")]
    public static void SetupShelfSorting()
    {
        var log = new StringBuilder();
        log.AppendLine("Setup Shelf Sorting report:");

        for (int i = 0; i < ShelfUnitNames.Length; i++)
        {
            GameObject unit = GameObject.Find(ShelfUnitNames[i]);
            if (unit == null)
            {
                log.AppendLine($"- Skipped '{ShelfUnitNames[i]}' (not found in scene; run 'Add Shelves' first).");
                continue;
            }

            ShelfSlot slot = unit.GetComponent<ShelfSlot>();
            if (slot == null)
            {
                slot = unit.AddComponent<ShelfSlot>();
            }

            var serialized = new SerializedObject(slot);
            serialized.FindProperty("acceptedCategory").enumValueIndex = (int)ShelfCategories[i];
            serialized.FindProperty("despawnDelay").floatValue = 0.5f;
            serialized.ApplyModifiedProperties();
            slot.correctFeedbackColor = Color.green;

            // Баг из прошлой версии: у корня ShelfUnit не было своего Collider-а, поэтому
            // OnTriggerEnter никогда не срабатывал и стеллаж ни на что не реагировал.
            // Ставим собственный триггер-BoxCollider по границам стеллажа, чтобы это работало.
            FitTriggerCollider(unit);

            log.AppendLine($"- Configured '{ShelfUnitNames[i]}' to accept {ShelfCategories[i]} packages (trigger collider fitted).");
        }

        Debug.Log(log.ToString());
    }

    private static void FitTriggerCollider(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider trigger = root.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = root.AddComponent<BoxCollider>();
        }
        trigger.isTrigger = true;

        Vector3 lossyScale = root.transform.lossyScale;
        Vector3 localSize = new Vector3(
            worldBounds.size.x / Mathf.Max(lossyScale.x, 0.0001f),
            worldBounds.size.y / Mathf.Max(lossyScale.y, 0.0001f),
            worldBounds.size.z / Mathf.Max(lossyScale.z, 0.0001f));

        trigger.center = root.transform.InverseTransformPoint(worldBounds.center);
        // Небольшой запас (0.1), чтобы посылка засчитывалась чуть раньше, чем упрётся в полку.
        trigger.size = localSize + Vector3.one * 0.1f;
    }

    [MenuItem("Warehouse Tools/Remove Sorting Zones")]
    public static void RemoveSortingZones()
    {
        var log = new StringBuilder();
        log.AppendLine("Remove Sorting Zones report:");

        // Напольные SortingZone больше не нужны — сортировка теперь идёт через
        // ShelfSlot на стеллажах (см. AddShelves()/SetupShelfSorting() выше).
        foreach (var zoneName in SortingZoneNames)
        {
            GameObject zone = GameObject.Find(zoneName);
            if (zone == null)
            {
                log.AppendLine($"- Skipped '{zoneName}' (not found in scene).");
                continue;
            }

            Object.DestroyImmediate(zone);
            log.AppendLine($"- Removed '{zoneName}' from the scene.");
        }

        // На "Main Camera" когда-то случайно оказался компонент SortingZone — раз скрипт
        // удалён, он превратился бы в "Missing Script". Чистим такие сироты на всей сцене.
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removedMissing = 0;
        foreach (var go in allObjects)
        {
            int removedHere = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removedMissing += removedHere;
        }

        if (removedMissing > 0)
        {
            log.AppendLine($"- Cleaned up {removedMissing} leftover 'Missing Script' component(s) from the old SortingZone script.");
        }

        Debug.Log(log.ToString());
    }
}
