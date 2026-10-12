using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    // Комбо 2026-10-12: частицы Смэша, Морской фигуры, Чехарды и Пенальти + проводка на мячи и игрока.
    // Меню Bouncer → Cards → Build Combo FX. Перезапускаемый, без удаления ассетов.
    static partial class CardContentBuilder
    {
        [MenuItem("Bouncer/Cards/Build Combo FX")]
        public static void BuildComboFx()
        {
            var smash = Burst("SmashSpark", 0.8f,
                ("Sparks", new Color(1f, 0.85f, 0.3f), Color.white, 10, 6f, 0.3f, 0.09f, 0f),
                ("Flash", Color.white, new Color(1f, 0.92f, 0.6f), 2, 0.3f, 0.12f, 0.5f, 0f));
            var shards = Burst("IceShards", 1.2f,
                ("Shards", new Color(0.72f, 0.9f, 1f), Color.white, 16, 5f, 0.7f, 0.14f, 1.6f),
                ("Frost", new Color(0.85f, 0.95f, 1f, 0.8f), Color.white, 8, 1.5f, 0.6f, 0.4f, -0.05f));
            var dust = Burst("LeapDust", 1.2f,
                ("Dust", Dust, new Color(0.9f, 0.86f, 0.78f, 0.6f), 16, 2.2f, 0.7f, 0.45f, -0.03f));
            var kick = Burst("KickFlash", 0.8f,
                ("Stars", new Color(1f, 0.85f, 0.3f), Color.white, 6, 3f, 0.25f, 0.2f, 0f),
                ("Flash", Color.white, new Color(1f, 0.95f, 0.7f), 2, 0.2f, 0.1f, 0.7f, 0f));

            foreach (string path in AllBalls)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var ball = root.GetComponent<Ball>();
                Set(ball, "smashSpark", smash.GetComponent<ParticleBurst>());
                Set(ball, "iceShards", shards.GetComponent<ParticleBurst>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            const string playerPath = Root + "Prefabs/Player.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            var fx = player.GetComponent<PlayerCardFx>();
            Set(fx, "leapDust", dust.GetComponent<ParticleBurst>());
            Set(fx, "kickFlash", kick.GetComponent<ParticleBurst>());
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            PrefabUtility.UnloadPrefabContents(player);

            VfxPassBuilder.StyleAllParticles();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cards] Частицы комбо готовы и подключены к мячам и игроку.");
        }
    }
}
