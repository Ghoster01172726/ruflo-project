using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Conveyor loop (карусель) part of WarehouseBuilder: places a closed waypoint route
/// near SpawnPoint and tiles the downloaded conveyor belt model along it.
/// See ConveyorLoopMover.cs for the runtime movement logic.
/// </summary>
public static partial class WarehouseBuilder
{
    // Скачанная модель ленты конвейера (Poly Pizza, "Conveyor Belt" by Sierra Maple, CC-BY 3.0).
    // Это цельный готовый пролёт (рельсы + ножки + ролики на концах), а не модульная
    // секция для тайлинга — поэтому на каждую грань маршрута ставится ровно одна копия,
    // растянутая по длине под длину этой грани (см. BuildConveyorEdgeVisual).
    private const string ConveyorModelPath = "Assets/Models/Conveyor/ConveyorBelt.glb";
    private const string ConveyorWaypointsName = "ConveyorWaypoints";
    private const string ConveyorVisualName = "ConveyorLoopVisual";
    private const float ConveyorLoopSpeed = 0.6f;
    // Замкнутый прямоугольный маршрут (карусель) рядом с точкой спавна: посылка едет
    // по кругу бесконечно, пока игрок её не заберёт — если не успел, она просто едет дальше.
    private static readonly Vector3 ConveyorLoopCenterOffset = new Vector3(0.8f, 0f, -1.5f);
    private const float ConveyorLoopHalfWidth = 0.6f;
    private const float ConveyorLoopHalfLength = 1.2f;

    [MenuItem("Warehouse Tools/Add Conveyor Loop")]
    public static void AddConveyorLoop()
    {
        var log = new StringBuilder();
        log.AppendLine("Conveyor Loop report:");

        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint == null)
        {
            Debug.LogError("WarehouseBuilder: 'SpawnPoint' not found in the scene. Aborting.");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorModelPath);
        if (modelAsset == null)
        {
            Debug.LogError($"WarehouseBuilder: conveyor model not found at '{ConveyorModelPath}'. Aborting.");
            return;
        }

        Vector3 center = spawnPoint.transform.position + ConveyorLoopCenterOffset;
        // Замкнутый прямоугольный маршрут (карусель): 4 угловые точки, посылки едут
        // по ним по кругу 0 -> 1 -> 2 -> 3 -> 0 бесконечно (см. ConveyorLoopMover.cs).
        Vector3[] corners =
        {
            center + new Vector3(-ConveyorLoopHalfWidth, 0f, ConveyorLoopHalfLength),
            center + new Vector3(ConveyorLoopHalfWidth, 0f, ConveyorLoopHalfLength),
            center + new Vector3(ConveyorLoopHalfWidth, 0f, -ConveyorLoopHalfLength),
            center + new Vector3(-ConveyorLoopHalfWidth, 0f, -ConveyorLoopHalfLength),
        };

        Transform waypointsRoot = BuildConveyorWaypoints(corners, log);
        BuildConveyorVisual(corners, modelAsset, log);

        GameObject spawnerGO = GameObject.Find("PackageSpawner");
        if (spawnerGO != null)
        {
            PackageSpawner spawner = spawnerGO.GetComponent<PackageSpawner>();
            if (spawner != null)
            {
                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("conveyorWaypointsRoot").objectReferenceValue = waypointsRoot;
                serialized.FindProperty("conveyorSpeed").floatValue = ConveyorLoopSpeed;
                serialized.ApplyModifiedProperties();
                log.AppendLine("- Synced PackageSpawner's conveyor waypoints/speed to match the loop.");
            }
        }
        else
        {
            log.AppendLine("- Warning: 'PackageSpawner' not found; spawned packages will use their own default conveyor settings.");
        }

        Debug.Log(log.ToString());
    }

    private static Transform BuildConveyorWaypoints(Vector3[] corners, StringBuilder log)
    {
        GameObject root = GameObject.Find(ConveyorWaypointsName);
        bool created = root == null;
        if (created)
        {
            root = new GameObject(ConveyorWaypointsName);
        }

        for (int i = 0; i < corners.Length; i++)
        {
            string childName = $"WP_{i}";
            Transform child = root.transform.Find(childName);
            if (child == null)
            {
                var childGO = new GameObject(childName);
                childGO.transform.SetParent(root.transform, worldPositionStays: false);
                child = childGO.transform;
            }

            child.position = corners[i];
        }

        log.AppendLine($"- {(created ? "Created" : "Updated")} '{ConveyorWaypointsName}' with {corners.Length} loop points.");
        return root.transform;
    }

    private static void BuildConveyorVisual(Vector3[] corners, GameObject modelAsset, StringBuilder log)
    {
        GameObject visualRoot = GameObject.Find(ConveyorVisualName);
        if (visualRoot != null)
        {
            Object.DestroyImmediate(visualRoot);
        }
        visualRoot = new GameObject(ConveyorVisualName);

        float nativeLength = MeasureNativeLength(modelAsset);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 from = corners[i];
            Vector3 to = corners[(i + 1) % corners.Length];
            BuildConveyorEdgeVisual(visualRoot.transform, from, to, modelAsset, nativeLength, i);
        }

        log.AppendLine($"- Built '{ConveyorVisualName}' from {corners.Length} instances of '{ConveyorModelPath}' (one per edge, stretched to fit).");
    }

    // Модель приходит в собственном "родном" масштабе (метры) с длиной вдоль локальной
    // оси X — измеряем её один раз через объединённые world-bounds рендереров эталонного
    // инстанса (при identity-трансформе world == local), чтобы знать, во сколько раз
    // растягивать/сжимать каждую копию под длину конкретной грани маршрута.
    private static float MeasureNativeLength(GameObject modelAsset)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;
        probe.transform.localScale = Vector3.one;

        Renderer[] renderers = probe.GetComponentsInChildren<Renderer>();
        float length = 1f;
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            length = Mathf.Max(bounds.size.x, 0.01f);
        }

        Object.DestroyImmediate(probe);
        return length;
    }

    // Ставит ровно одну копию цельной модели конвейера вдоль отрезка маршрута: разворачивает
    // её так, чтобы родная ось длины (локальный +X) совпала с направлением отрезка
    // (оба вектора лежат в горизонтальной плоскости, поэтому FromToRotation крутит только
    // вокруг Y — "верх" модели не заваливается), и растягивает только по этой оси —
    // высота и ширина ножек/рельсов остаются в исходных, физически правдоподобных пропорциях.
    private static void BuildConveyorEdgeVisual(Transform parent, Vector3 from, Vector3 to, GameObject modelAsset, float nativeLength, int index)
    {
        Vector3 edge = to - from;
        float edgeLength = edge.magnitude;
        if (edgeLength < 0.01f)
        {
            return;
        }

        Vector3 direction = edge / edgeLength;
        Vector3 midpoint = (from + to) * 0.5f;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, parent);
        instance.name = $"ConveyorEdge_{index}";
        instance.transform.position = midpoint;
        instance.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        instance.transform.localScale = new Vector3(edgeLength / nativeLength, 1f, 1f);
    }

    // Равномерно масштабирует объект так, чтобы его наибольший габарит (по всем осям
    // combined-bounds рендереров) стал равен targetSize — модели, скачанные из внешних
    // источников, приходят в произвольном исходном масштабе. Используется также для
    // рук игрока (WarehouseBuilder.Player.cs), поэтому вынесена сюда как общий helper.
    private static void AutoFitLocalScale(GameObject go, float targetSize)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension <= 0.0001f)
        {
            return;
        }

        float scaleFactor = targetSize / maxDimension;
        go.transform.localScale *= scaleFactor;
    }
}
