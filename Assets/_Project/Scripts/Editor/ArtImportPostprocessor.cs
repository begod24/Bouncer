using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    /// <summary>
    /// Единые настройки импорта арта из Blender (Bouncer.blend → ba_export.py).
    /// Модели из Art/Models: масштаб 1, плоские нормали из файла, без анимации, материал M_Palette.
    /// Палитры из Art/Palettes: Point, без mip-map и сжатия — каждая ячейка 8×8 px должна остаться чистым цветом.
    /// UI из Art/UI (Tools/ui_art.py и иконки мячей): спрайты с прозрачностью, без mip-map и сжатия.
    /// Настройки применяются при каждом импорте, так что правки руками в инспекторе перезапишутся.
    /// </summary>
    sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/_Project/Art/Models/";
        const string PalettesRoot = "Assets/_Project/Art/Palettes/";
        const string UiRoot = "Assets/_Project/Art/UI/";
        const string PaletteMaterialPath = "Assets/_Project/Art/Materials/M_Palette.mat";
        const string PaletteMaterialName = "M_Palette";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            // Оси (Blender Z-up → Unity Y-up) конвертирует Unity: у всех узлов, включая вложенные
            // (руки/ноги под телом), будет нулевой поворот. «Apply Transform» в Blender ломает вложенные узлы.
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.generateSecondaryUV = false;
            importer.addCollider = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            var palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (palette)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), PaletteMaterialName), palette);
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(UiRoot))
            {
                PreprocessUiSprite();
                return;
            }
            if (!assetPath.StartsWith(PalettesRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }

        void PreprocessUiSprite()
        {
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Полосы и окошко вкладыша тянутся, а меловые концы и скруглённые углы должны остаться целыми (9-slice).
            if (assetPath.Contains("HUD_Bar"))
                importer.spriteBorder = new Vector4(24f, 16f, 24f, 16f);
            else if (assetPath.Contains("Card_Window"))
                importer.spriteBorder = new Vector4(28f, 28f, 28f, 28f);
        }
    }
}
