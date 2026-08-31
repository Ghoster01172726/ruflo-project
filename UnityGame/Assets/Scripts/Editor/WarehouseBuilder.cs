using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only tool that assembles a basic warehouse shell (walls, lighting, materials)
/// around the existing Floor object. Run from Warehouse Tools/Build Warehouse Shell.
/// Split into partial files by feature: this file holds the shell/floor/sorting-zone/
/// package core plus shared helpers; see WarehouseBuilder.Conveyor.cs,
/// WarehouseBuilder.Player.cs and WarehouseBuilder.PostProcessing.cs for the rest.
/// </summary>
public static partial class WarehouseBuilder
{
    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;

    private const string FloorMaterialPath = "Assets/Materials/Mat_Floor.mat";
    private const string WallMaterialPath = "Assets/Materials/Mat_Wall.mat";
    private const string CleanFloorTexturePath = "Assets/Materials/Textures/Concrete_Floor_Clean_Color.jpg";

    private const string PackageMaterialPath = "Assets/Materials/Mat_Package.mat";
    private const string PackagePrefabPath = "Assets/Prefabs/Package.prefab";

    private static readonly Color FloorColor = new Color(0.5f, 0.5f, 0.5f); // grey concrete
    private static readonly Color WallColor = new Color(0.75f, 0.75f, 0.75f); // light grey
    private static readonly Color WarmLightColor = new Color(1f, 0.95f, 0.85f); // warm off-white

    private const string PackageContainerName = "PackageContainer";
    private static readonly Vector3 PackageContainerScale = new Vector3(1.2f, 1.2f, 1.2f);
    // Placed just north of the existing SpawnPoint (0,0,0) so newly spawned packages
    // visually appear to come out of the container's front face.
    private static readonly Vector3 PackageContainerOffsetFromSpawn = new Vector3(0f, 0f, 0.8f);

    [MenuItem("Warehouse Tools/Build Warehouse Shell")]
    public static void BuildWarehouseShell()
    {
        var log = new StringBuilder();
        log.AppendLine("Warehouse Shell build report:");

        GameObject floor = GameObject.Find("Floor");
        if (floor == null)
        {
            Debug.LogError("WarehouseBuilder: no GameObject named 'Floor' found in the scene. Aborting.");
            return;
        }

        Bounds floorBounds = GetWorldBounds(floor);
        Vector3 center = floorBounds.center;
        float sizeX = floorBounds.size.x;
        float sizeZ = floorBounds.size.z;

        BuildWall(log, "Wall_North", new Vector3(center.x, WallHeight / 2f, center.z + sizeZ / 2f),
            new Vector3(sizeX, WallHeight, WallThickness));
        BuildWall(log, "Wall_South", new Vector3(center.x, WallHeight / 2f, center.z - sizeZ / 2f),
            new Vector3(sizeX, WallHeight, WallThickness));
        BuildWall(log, "Wall_East", new Vector3(center.x + sizeX / 2f, WallHeight / 2f, center.z),
            new Vector3(WallThickness, WallHeight, sizeZ));
        BuildWall(log, "Wall_West", new Vector3(center.x - sizeX / 2f, WallHeight / 2f, center.z),
            new Vector3(WallThickness, WallHeight, sizeZ));

        ConfigureDirectionalLight(log);

        Material floorMat = GetOrCreateMaterial(log, FloorMaterialPath, FloorColor, "Mat_Floor");
        AssignMaterial(floor, floorMat);

        Material wallMat = GetOrCreateMaterial(log, WallMaterialPath, WallColor, "Mat_Wall");
        foreach (var wallName in new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" })
        {
            GameObject wall = GameObject.Find(wallName);
            if (wall != null)
            {
                AssignMaterial(wall, wallMat);
            }
        }

        Debug.Log(log.ToString());
    }

    [MenuItem("Warehouse Tools/Update Floor Texture (Clean Concrete)")]
    public static void UpdateFloorTextureToClean()
    {
        var log = new StringBuilder();
        log.AppendLine("Floor Texture Update report:");

        AssetDatabase.Refresh();

        Texture2D cleanTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanFloorTexturePath);
        if (cleanTexture == null)
        {
            Debug.LogError($"WarehouseBuilder: texture not found at '{CleanFloorTexturePath}'. Aborting.");
            return;
        }

        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        if (floorMat == null)
        {
            Debug.LogError($"WarehouseBuilder: '{FloorMaterialPath}' not found. Run 'Build Warehouse Shell' first. Aborting.");
            return;
        }

        floorMat.mainTexture = cleanTexture;
        EditorUtility.SetDirty(floorMat);
        AssetDatabase.SaveAssets();

        log.AppendLine($"- Updated '{FloorMaterialPath}' Albedo to reference '{CleanFloorTexturePath}'.");
        log.AppendLine("- Mat_Wall and Mat_SortingZone were not touched.");

        Debug.Log(log.ToString());
    }

    [MenuItem("Warehouse Tools/Add Package Container")]
    public static void AddPackageContainer()
    {
        var log = new StringBuilder();
        log.AppendLine("Package Container report:");

        if (GameObject.Find(PackageContainerName) != null)
        {
            log.AppendLine($"- Skipped '{PackageContainerName}' (already exists).");
            Debug.Log(log.ToString());
            return;
        }

        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
        Vector3 basePosition = spawnPosition + PackageContainerOffsetFromSpawn;
        Vector3 containerPosition = new Vector3(basePosition.x, PackageContainerScale.y / 2f, basePosition.z);

        GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);
        container.name = PackageContainerName;
        container.transform.position = containerPosition;
        container.transform.localScale = PackageContainerScale;

        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        if (wallMat != null)
        {
            AssignMaterial(container, wallMat);
        }
        else
        {
            log.AppendLine($"- Warning: '{WallMaterialPath}' not found; container uses the default material.");
        }

        log.AppendLine($"- Created '{PackageContainerName}' at {containerPosition}, next to the existing SpawnPoint.");
        log.AppendLine("- PackageSpawner/SpawnPoint were not modified; packages still spawn at the original point.");

        Debug.Log(log.ToString());
    }

    [MenuItem("Warehouse Tools/Fix Package Material")]
    public static void FixPackageMaterial()
    {
        var log = new StringBuilder();
        log.AppendLine("Package Material Fix report:");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"WarehouseBuilder: prefab not found at '{PackagePrefabPath}'. Aborting.");
            return;
        }

        Renderer prefabRenderer = prefab.GetComponentInChildren<Renderer>();
        if (prefabRenderer == null)
        {
            Debug.LogError($"WarehouseBuilder: '{PackagePrefabPath}' has no Renderer. Aborting.");
            return;
        }

        Material packageMat = AssetDatabase.LoadAssetAtPath<Material>(PackageMaterialPath);
        if (packageMat == null)
        {
            packageMat = new Material(GetDefaultShader());
            // Neutral white: PackagePickup.ApplyCategoryColor() tints material.color per
            // category (yellow/red/white) at runtime, so the base color must stay neutral.
            packageMat.color = Color.white;
            AssetDatabase.CreateAsset(packageMat, PackageMaterialPath);
            log.AppendLine($"- Created '{PackageMaterialPath}' (Universal Render Pipeline/Lit shader).");
        }
        else
        {
            log.AppendLine($"- Reused existing '{PackageMaterialPath}'.");
        }

        // Package.prefab's Renderer previously referenced Unity's built-in Default-Material
        // (Standard shader, Built-in RP), which renders magenta under URP — that was the
        // pink cube visible in the Game view. Assigning a proper URP material here fixes it.
        prefabRenderer.sharedMaterial = packageMat;
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();

        log.AppendLine($"- Assigned '{PackageMaterialPath}' to '{PackagePrefabPath}' (was Unity's built-in Default-Material).");

        Debug.Log(log.ToString());
    }

    private static Bounds GetWorldBounds(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }

        // Fallback: default Unity Plane is 10x10 units at scale 1.
        return new Bounds(go.transform.position, new Vector3(10f * go.transform.localScale.x, 0f, 10f * go.transform.localScale.z));
    }

    private static void BuildWall(StringBuilder log, string name, Vector3 position, Vector3 scale)
    {
        if (GameObject.Find(name) != null)
        {
            log.AppendLine($"- Skipped '{name}' (already exists).");
            return;
        }

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;

        log.AppendLine($"- Created '{name}' at {position} with scale {scale}.");
    }

    private static void ConfigureDirectionalLight(StringBuilder log)
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light directional = null;
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                directional = light;
                break;
            }
        }

        if (directional == null)
        {
            log.AppendLine("- No Directional Light found in the scene; skipped lighting setup.");
            return;
        }

        directional.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        directional.color = WarmLightColor;
        directional.intensity = 1.0f;

        log.AppendLine($"- Configured Directional Light '{directional.name}' (rotation, warm color, intensity).");
    }

    private static Shader GetDefaultShader()
    {
        // Project runs on URP, where the legacy "Standard" shader (Built-in RP) renders
        // magenta. Prefer URP/Lit and only fall back to Standard if URP is somehow absent.
        return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
    }

    private static Material GetOrCreateMaterial(StringBuilder log, string path, Color color, string label)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            log.AppendLine($"- Skipped '{label}' material (already exists at {path}).");
            return material;
        }

        material = new Material(GetDefaultShader());
        material.color = color;
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();

        log.AppendLine($"- Created '{label}' material at {path}.");
        return material;
    }

    private static void AssignMaterial(GameObject go, Material material)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
