using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// First-person player part of WarehouseBuilder: sets up a CharacterController-based
/// Player with camera, visible hands model and package-grabbing.
/// See PlayerController.cs / PackageGrabber.cs for the runtime behaviour.
/// </summary>
public static partial class WarehouseBuilder
{
    // Устаревшее имя модели рук (Poly Pizza, "Rigged Fps Arms" by Player11132, CC-BY 3.0) —
    // заменена на HandModelPath ниже; константа оставлена только чтобы SetupHands() мог
    // найти и удалить старый объект при повторном запуске на уже существующей сцене.
    private const string LegacyHandsModelName = "FpsArms";

    // Риггованная низкополигональная модель кисти руки (правая; левая — её зеркальная копия).
    private const string HandModelPath = "Assets/Models/Hands/Hand_R.fbx";
    private const string HandMaterialPath = "Assets/Materials/Mat_Hand.mat";
    private static readonly Color HandSkinColor = new Color(0.87f, 0.68f, 0.53f);
    private const string HandsRootName = "Hands";
    private const string RightHandName = "Hand_Right";
    private const string LeftHandName = "Hand_Left";
    private const float HandTargetSize = 0.19f; // примерный размер кисти взрослого человека, метры
    private static readonly Vector3 RightHandLocalPosition = new Vector3(0.18f, -0.32f, 0.5f);
    private static readonly Vector3 LeftHandLocalPosition = new Vector3(-0.18f, -0.32f, 0.5f);

    private const string HandAnchorName = "HandAnchor";
    private static readonly Vector3 HandAnchorLocalPosition = new Vector3(0.3f, -0.25f, 0.6f);

    private const string PlayerName = "Player";
    private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, 1f, -3f);

    [MenuItem("Warehouse Tools/Setup Player")]
    public static void SetupPlayer()
    {
        var log = new StringBuilder();
        log.AppendLine("Setup Player report:");

        GameObject player = GameObject.Find(PlayerName);
        if (player == null)
        {
            player = new GameObject(PlayerName);
            player.transform.position = PlayerSpawnPosition;
            log.AppendLine($"- Created '{PlayerName}' at {PlayerSpawnPosition}.");
        }
        else
        {
            log.AppendLine($"- Reused existing '{PlayerName}'.");
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.3f;
            log.AppendLine("- Added CharacterController.");
        }

        Camera mainCamera = Camera.main;
        GameObject cameraGO;
        if (mainCamera != null)
        {
            cameraGO = mainCamera.gameObject;
            if (cameraGO.transform.parent != player.transform)
            {
                cameraGO.transform.SetParent(player.transform, worldPositionStays: false);
                cameraGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                cameraGO.transform.localRotation = Quaternion.identity;
                log.AppendLine("- Reparented existing Main Camera under Player.");
            }
        }
        else
        {
            cameraGO = new GameObject("PlayerCamera");
            cameraGO.transform.SetParent(player.transform, worldPositionStays: false);
            cameraGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            cameraGO.AddComponent<Camera>();
            cameraGO.AddComponent<AudioListener>();
            cameraGO.tag = "MainCamera";
            log.AppendLine("- Created new PlayerCamera (no Main Camera found in scene).");
        }

        Transform cameraTransform = cameraGO.transform;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = player.AddComponent<PlayerController>();
        }
        var playerSerialized = new SerializedObject(playerController);
        playerSerialized.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
        playerSerialized.ApplyModifiedProperties();
        log.AppendLine("- Configured PlayerController (WASD + mouse look).");

        Transform hands = SetupHands(cameraTransform, log);
        Transform handAnchor = SetupHandAnchor(cameraTransform, log);

        PackageGrabber grabber = player.GetComponent<PackageGrabber>();
        if (grabber == null)
        {
            grabber = player.AddComponent<PackageGrabber>();
        }
        var grabberSerialized = new SerializedObject(grabber);
        grabberSerialized.FindProperty("playerCamera").objectReferenceValue = cameraGO.GetComponent<Camera>();
        grabberSerialized.FindProperty("handAnchor").objectReferenceValue = handAnchor;
        grabberSerialized.ApplyModifiedProperties();
        log.AppendLine("- Configured PackageGrabber (клавиша E, смотреть на посылку и брать/бросать).");

        Debug.Log(log.ToString());
    }

    private static Transform SetupHands(Transform cameraTransform, StringBuilder log)
    {
        Transform legacy = cameraTransform.Find(LegacyHandsModelName);
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy.gameObject);
            log.AppendLine($"- Removed legacy '{LegacyHandsModelName}' hands model (replaced by rigged hand mesh).");
        }

        Transform existingRoot = cameraTransform.Find(HandsRootName);
        if (existingRoot != null)
        {
            log.AppendLine("- Rigged hands already present under the camera.");
            return existingRoot;
        }

        GameObject handAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HandModelPath);
        if (handAsset == null)
        {
            log.AppendLine($"- Warning: hand model not found at '{HandModelPath}'. Skipped hands setup.");
            return null;
        }

        Material handMaterial = GetOrCreateMaterial(log, HandMaterialPath, HandSkinColor, "Mat_Hand");

        var handsRoot = new GameObject(HandsRootName);
        handsRoot.transform.SetParent(cameraTransform, worldPositionStays: false);
        handsRoot.transform.localPosition = Vector3.zero;
        handsRoot.transform.localRotation = Quaternion.identity;

        InstantiateHand(handAsset, handsRoot.transform, RightHandName, RightHandLocalPosition, mirror: false, handMaterial);
        InstantiateHand(handAsset, handsRoot.transform, LeftHandName, LeftHandLocalPosition, mirror: true, handMaterial);

        HandsMotion motion = handsRoot.GetComponent<HandsMotion>();
        if (motion == null)
        {
            motion = handsRoot.AddComponent<HandsMotion>();
        }
        CharacterController controller = cameraTransform.GetComponentInParent<CharacterController>();
        var motionSerialized = new SerializedObject(motion);
        motionSerialized.FindProperty("playerController").objectReferenceValue = controller;
        motionSerialized.ApplyModifiedProperties();

        log.AppendLine("- Added rigged left/right hand meshes under the camera with walk-bob/look-sway motion.");
        return handsRoot.transform;
    }

    private static void InstantiateHand(GameObject handAsset, Transform parent, string name, Vector3 localPosition, bool mirror, Material material)
    {
        var handGO = (GameObject)PrefabUtility.InstantiatePrefab(handAsset, parent);
        handGO.name = name;
        handGO.transform.localPosition = Vector3.zero;
        handGO.transform.localRotation = Quaternion.identity;
        handGO.transform.localScale = Vector3.one;
        AutoFitLocalScale(handGO, HandTargetSize);
        handGO.transform.localPosition = localPosition;

        if (mirror)
        {
            // Готовой модели левой руки нет — отражаем меш правой руки по оси X. Unity
            // корректно перерисовывает нормали/порядок вершин при отрицательном масштабе.
            Vector3 scale = handGO.transform.localScale;
            handGO.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }

        foreach (var renderer in handGO.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Transform SetupHandAnchor(Transform cameraTransform, StringBuilder log)
    {
        Transform anchor = cameraTransform.Find(HandAnchorName);
        if (anchor != null)
        {
            return anchor;
        }

        var anchorGO = new GameObject(HandAnchorName);
        anchorGO.transform.SetParent(cameraTransform, worldPositionStays: false);
        anchorGO.transform.localPosition = HandAnchorLocalPosition;
        log.AppendLine("- Created hand anchor point for carried packages.");
        return anchorGO.transform;
    }
}
