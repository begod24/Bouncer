using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/_Project/Art/Models/";
        const string KidsRoot = "Assets/_Project/Art/Models/Kids/";
        const string KidAnimPrefix = "Anim_";
        static readonly string[] KidLoopClips = { "Idle", "Run_", "Cheer", "Pose_", "Test_" };
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
            if (assetPath.StartsWith(KidsRoot))
                PreprocessKid(importer);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            var palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (palette)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), PaletteMaterialName), palette);
            var decals = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
            if (decals)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), DecalMaterialName), decals);
        }

        void PreprocessKid(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = System.IO.Path.GetFileName(assetPath).StartsWith(KidAnimPrefix);
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = true;

            var description = importer.humanDescription;
            description.human = System.Array.Empty<HumanBone>();
            description.skeleton = System.Array.Empty<SkeletonBone>();
            importer.humanDescription = description;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(KidsRoot))
                return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0)
                    clip.name = clip.name[(bar + 1)..];
                clip.loopTime = System.Array.Exists(KidLoopClips, p => clip.name.StartsWith(p));
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
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
            if (assetPath.Contains("HUD_Bar"))
                importer.spriteBorder = new Vector4(24f, 16f, 24f, 16f);
            else if (assetPath.Contains("Card_Window"))
                importer.spriteBorder = new Vector4(28f, 28f, 28f, 28f);
        }
    }
}
