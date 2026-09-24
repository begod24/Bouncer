using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    /// <summary>
    /// Единые настройки импорта арта из Blender (Bouncer.blend → ba_export.py).
    /// Модели из Art/Models: масштаб 1, плоские нормали из файла, без анимации, материалы M_Palette и M_Decals
    /// (надписи: UV0 — ячейка палитры, UV1 — место в атласе, поэтому второй UV не генерируется).
    /// Палитры из Art/Palettes: Point, без mip-map и сжатия — каждая ячейка 8×8 px должна остаться чистым цветом.
    /// Атлас надписей из Art/Decals (Tools/decal_art.py): одноканальная маска (R) с mip-map, сжатая.
    /// UI из Art/UI (Tools/ui_art.py и иконки мячей): спрайты с прозрачностью, без mip-map и сжатия.
    /// Настройки применяются при каждом импорте, так что правки руками в инспекторе перезапишутся.
    /// </summary>
    sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/_Project/Art/Models/";
        const string PalettesRoot = "Assets/_Project/Art/Palettes/";
        const string DecalsRoot = "Assets/_Project/Art/Decals/";
        const string UiRoot = "Assets/_Project/Art/UI/";
        const string PaletteMaterialPath = "Assets/_Project/Art/Materials/M_Palette.mat";
        const string PaletteMaterialName = "M_Palette";
        const string DecalMaterialPath = "Assets/_Project/Art/Materials/M_Decals.mat";
        const string DecalMaterialName = "M_Decals";

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
            var decals = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
            if (decals)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), DecalMaterialName), decals);
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(UiRoot))
            {
                PreprocessUiSprite();
                return;
            }
            if (assetPath.StartsWith(DecalsRoot))
            {
                PreprocessDecalAtlas();
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

        void PreprocessDecalAtlas()
        {
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.SingleChannel;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
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
