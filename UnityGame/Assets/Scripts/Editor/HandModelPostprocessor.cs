using UnityEditor;

// Настраивает импорт FBX-модели руки (Assets/Models/Hands/Hand_*.fbx) при её импорте/переимпорте:
// риг оставляем как есть (скелет руки — не гуманоид), а вот запечённый в файле клип
// (демо-анимация вращения объекта + сгибание пальцев для промо-рендера) не импортируем —
// он не предназначен для игры и будет крутить всю модель. Материалы тоже не импортируем:
// исходник без текстур, кожаный материал назначает WarehouseBuilder.SetupHands().
public class HandModelPostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("Assets/Models/Hands/Hand_"))
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }
}
