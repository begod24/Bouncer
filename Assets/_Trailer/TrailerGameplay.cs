using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEditor;
using UnityEngine;
using static Bouncer.Trailer.TrailerStage;

namespace Bouncer.Trailer
{
    // Кадры геймплейного ролика (Steam + Instagram): игровая камера, как у игрока; боссы и кооп — из трейлера.
    // Каждый кадр снимается дважды: TrailerRecorder.Hud = false (чистый) и true (с интерфейсом, клип *_hud).
    public sealed partial class TrailerDirector
    {
        static readonly Vector3 GameOffset = new(0f, 16.38f, -11.47f);

        IEnumerator GameplayScene(string shot) => shot switch
        {
            "g_hook" => Hook(),
            "g_loop" => Loop(),
            "g_card" => CardPick(),
            "g_tennis" => BallShot("g_tennis", "Card_Ball_Tennis", Huligan, new[] { "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "RolyPoly" }),
            "g_basket" => BallShot("g_basket", "Card_Ball_Basketball", Tolstyak, new[] { "ShieldSoldier", "ShieldSoldier", "ShieldSoldier", "Pupsik", "Pupsik" }, range: 9f),
            "g_football" => BallShot("g_football", "Card_Ball_Football", Melkaya, new[] { "Pupsik", "Pupsik", "RCCar", "Pupsik", "Frog", "Pupsik" }, range: 5f),
            "g_pingpong" => BallShot("g_pingpong", "Card_Ball_PingPong", Otlichnik, new[] { "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik" }, radius: 6f),
            "g_medicine" => BallShot("g_medicine", "Card_Ball_Medicine", Tolstyak, new[] { "Bear", "RolyPoly", "Pupsik", "RolyPoly", "Pupsik" }),
            "g_fence" => FenceShot(),
            "g_rink" => GameArena("Rink", "g_rink", 0.2f, new[] { "Frog", "Frog", "Top", "Lunokhod", "Frog", "Pupsik", "Top" }, Melkaya),
            "g_bazaar" => GameArena("Bazaar", "g_bazaar", 0.1f, new[] { "Drummer", "RCCar", "DendyGun", "CryDoll", "RCCar", "Pupsik" }, Tolstyak),
            "g_site" => GameArena("Site", "g_site", 0f, new[] { "Shadow", "Elite_RockingHorse", "Scarecrow", "Shadow", "Mannequin" }, Huligan, lantern: true),
            "g_kg" => GameArena("Kindergarten", "g_kg", 0f, new[] { "Ballerina", "Chick", "Bear", "Chick", "CryDoll", "Chick" }, Otlichnik, lantern: true),
            _ => null,
        };

        // Игровая камера (как в Yard.unity: FOV 40, смещение 0/16.38/−11.47), чуть ближе — scale.
        static void GameCam(float seconds, Transform target, float scale = 0.75f, float fov = 40f)
        {
            Vector3 smooth = target.position;
            bool first = true;
            Go(Over(seconds, t =>
            {
                if (target == null)
                    return;
                smooth = first ? target.position : Vector3.Lerp(smooth, target.position, 0.1f);
                first = false;
                Vector3 focus = smooth + Vector3.up;
                Shot(focus + GameOffset * scale, focus, fov);
            }));
        }

        static void Bot(TrailerPuppet kid, float home, float range)
        {
            kid.Bot = true;
            kid.HomeRadius = home;
            kid.PreferredRange = range;
        }

        static void Arc(Vector3 center, float radius, string[] enemies, float from = -60f, float to = 60f, int hp = 0)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                float a = Mathf.Lerp(from, to, enemies.Length == 1 ? 0.5f : i / (float)(enemies.Length - 1)) * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Tough(Enemy(enemies[i], p, YawTo(p, center)), hp);
            }
        }

        // Крепче обычного: в коротком кадре враги не должны кончиться за секунду
        static void Tough(GameObject enemy, int hp)
        {
            if (hp > 0 && enemy && enemy.TryGetComponent(out Health health))
                health.Configure(hp, 0f);
        }

        // Подкрепление с метками появления, как у волн
        static IEnumerator Reinforce(float delay, Vector3 center, float radius, string[] enemies, int hp)
        {
            yield return Wait(delay);
            var ring = Ring(center, radius, enemies.Length, Random.Range(0f, 360f));
            for (int i = 0; i < enemies.Length; i++)
            {
                Go(SpawnTough(enemies[i], ring[i], YawTo(ring[i], center), hp));
                yield return Wait(0.08f);
            }
        }

        static IEnumerator SpawnTough(string prefab, Vector3 p, float yaw, int hp)
        {
            yield return SpawnWithMarker(prefab, p, yaw, 0.6f);
            yield return null;
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (hp > 0 && (h.transform.position - p).sqrMagnitude < 0.5f && h.GetComponent<TrailerPuppet>() == null)
                    h.Configure(hp, 0f);
        }

        // ---------------------------------------------------------------- крючок: замедленная ловля и ответный бросок
        IEnumerator Hook()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Day");
            var feel = Object.FindFirstObjectByType<GameFeel>();
            var kid = SpawnKid(0, Melkaya, new Vector3(0f, 0f, -7f), 0f, balls: 0);
            // без «Крышки»: из кармана она отбивает мяч раньше, чем Мелкая его ловит
            Card(kid, "Card_Mod_HotPotato");
            kid.AutoCatch = false;     // ловит сама сцена (LateCatch): автоловля бота в замедлении срывается
            Group("TinSoldier", new[] { new Vector3(-2f, 0f, -0.8f), new Vector3(0f, 0f, -0.4f), new Vector3(2f, 0f, -0.8f) }, 180f);
            yield return null;
            Follow(9f, kid.transform, new Vector3(1.3f, 1.5f, -3.3f), new Vector3(0f, 1f, 4f), 42f);
            TrailerRecorder.Begin("g_hook");
            int caught = 0;
            kid.Player.Balls.Caught += _ => caught++;
            bool slowed = false;
            float startedAt = Now;
            while (Now - startedAt < 8f)
            {
                if (!slowed && IncomingSoon(kid, 0.3f))
                {
                    slowed = true;
                    if (feel)
                        feel.enabled = false;
                    yield return Over(0.15f, t => Time.timeScale = Mathf.Lerp(1f, 0.22f, t));
                    float until = Now + 2.5f;
                    while (caught == 0 && Now < until)
                    {
                        LateCatch(kid);
                        yield return null;
                    }
                    yield return Wait(0.35f);
                    yield return Over(0.3f, t => Time.timeScale = Mathf.Lerp(0.22f, 1f, t));
                    Time.timeScale = 1f;
                    if (feel)
                        feel.enabled = true;
                    kid.AutoCatch = false;
                    yield return ThrowAt(kid, new Vector3(0f, 0f, -0.4f), 0.05f, 0.35f);
                    yield return Wait(1.6f);
                    break;
                }
                yield return null;
            }
            TrailerRecorder.End();
        }

        // Ловля в последний момент: мяч в ~0.06 с от груди — повернуться к нему и нажать «ловить»
        static void LateCatch(TrailerPuppet kid)
        {
            Vector3 chest = kid.transform.position + Vector3.up;
            foreach (var ball in Bouncer.Balls.Ball.Active)
            {
                if (ball.State != Bouncer.Balls.BallState.Live || !ball.Team.IsHostileTo(Team.Player))
                    continue;
                Vector3 v = ball.Velocity;
                if (v.sqrMagnitude < 1f)
                    continue;
                float t = Vector3.Dot(chest - ball.Position, v) / v.sqrMagnitude;
                if (t > 0f && t < 0.06f && (ball.Position + v * t - chest).magnitude < 1.4f)
                {
                    kid.AimAt(ball.Position);
                    kid.Catch();
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- основной цикл во дворе
        IEnumerator Loop()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.1f, "Day");
            var kid = SpawnKid(0, Huligan, new Vector3(0f, 0f, -3f), 0f, balls: 3);
            Card(kid, "Card_Mod_Slingshot");
            Card(kid, "Card_Mod_Bouncy");
            Bot(kid, 4.5f, 7f);
            foreach (var p in Ring(new Vector3(0f, 0f, -3f), 9f, 7, 20f))
                Tough(Enemy("Pupsik", p, YawTo(p, Vector3.zero)), 2);
            Go(Reinforce(2.5f, new Vector3(0f, 0f, -3f), 7f, new[] { "Pupsik", "Pupsik", "RolyPoly", "Pupsik", "Frog" }, 2));
            Enemy("RolyPoly", new Vector3(-7f, 0f, 5f), 160f);
            Enemy("RolyPoly", new Vector3(7f, 0f, 5f), 200f);
            Group("TinSoldier", new[] { new Vector3(-1.5f, 0f, 7f), new Vector3(0f, 0f, 7.4f), new Vector3(1.5f, 0f, 7f) }, 180f);
            Enemy("RCCar", new Vector3(-10f, 0f, -8f), 60f);
            yield return Wait(0.3f);
            GameCam(8f, kid.transform, 0.52f);
            yield return Record("g_loop", 7f, 0.1f);
        }

        // ---------------------------------------------------------------- выбор карточки → теннисный пулемёт
        IEnumerator CardPick()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.1f, "Day");
            var kid = SpawnKid(0, Otlichnik, new Vector3(0f, 0f, -3f), 0f, balls: 3);
            Bot(kid, 4f, 7f);
            Arc(new Vector3(0f, 0f, -3f), 9f, new[] { "Pupsik", "Pupsik", "Pupsik", "RolyPoly", "Pupsik", "Pupsik", "Pupsik", "Pupsik", "Pupsik" }, -80f, 80f, hp: 3);
            yield return Wait(0.3f);
            GameCam(7f, kid.transform, 0.52f);
            Go(OfferAndPick(kid));
            yield return Record("g_card", 6f, 0.1f);
        }

        static IEnumerator OfferAndPick(TrailerPuppet kid)
        {
            yield return Wait(0.5f);
            var cards = kid.GetComponent<PlayerCards>();
            var offer = new List<UpgradeCard>();
            foreach (var name in new[] { "Card_Ball_Tennis", "Card_Ability_Fence", "Card_Pas_Speed" })
            {
                var card = AssetDatabase.LoadAssetAtPath<UpgradeCard>("Assets/_Project/Data/Upgrades/" + name + ".asset");
                if (card)
                    offer.Add(card);
            }
            CardShot = true;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var list = (List<UpgradeCard>)typeof(PlayerCards).GetField("_offer", flags).GetValue(cards);
            list.Clear();
            list.AddRange(offer);
            typeof(PlayerCards).GetProperty("OfferKind", flags).SetValue(cards, OfferKind.Portfolio);
            GameSession.Instance?.BeginUpgradeChoice();
            var changed = (System.Delegate)typeof(PlayerCards).GetField("OfferChanged", flags).GetValue(cards);
            changed?.DynamicInvoke();
            yield return Wait(1.7f);
            cards.Choose(0);
            CardShot = false;
            GameSession.Instance?.EndUpgradeChoice();
        }

        // ---------------------------------------------------------------- мячи: короткие склейки
        IEnumerator BallShot(string clip, string ballCard, int kidIndex, string[] enemies, float range = 7f, float radius = 9f)
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.15f, "Day");
            var kid = SpawnKid(0, kidIndex, new Vector3(0f, 0f, -3f), 0f, balls: 3);
            Card(kid, ballCard);
            Bot(kid, 3.5f, range);
            kid.ThrowPause = new Vector2(0.2f, 0.5f);
            Arc(new Vector3(0f, 0f, -3f), radius, enemies, -70f, 70f, hp: 3);
            Go(Reinforce(1.2f, new Vector3(0f, 0f, -3f), radius * 0.8f, enemies, 3));
            yield return Wait(0.3f);
            GameCam(5f, kid.transform, 0.46f);
            yield return Record(clip, 4.2f, 0.1f);
        }

        // ---------------------------------------------------------------- «Забор» на Q: залп солдатиков отлетает обратно
        IEnumerator FenceShot()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.1f, "Day");
            var kid = SpawnKid(0, Melkaya, new Vector3(0f, 0f, -6f), 0f, balls: 0);
            Card(kid, "Card_Ability_Fence");
            kid.AutoCatch = false;
            Group("TinSoldier", new[] { new Vector3(-3f, 0f, 1.5f), new Vector3(-1f, 0f, 2f), new Vector3(1f, 0f, 2f), new Vector3(3f, 0f, 1.5f) }, 180f);
            yield return null;
            Follow(6f, kid.transform, new Vector3(3.2f, 4.2f, -5.2f), new Vector3(0f, 0.6f, 3.5f), 44f);
            Go(FenceOnVolley(kid));
            yield return Record("g_fence", 4.8f, 0.1f);
        }

        static IEnumerator FenceOnVolley(TrailerPuppet kid)
        {
            kid.AimDirection = Vector3.forward;
            float start = Now;
            while (!IncomingSoon(kid, 0.75f) && Now - start < 5f)
                yield return null;
            kid.AimDirection = Vector3.forward;
            kid.Ability();
        }

        // ---------------------------------------------------------------- другие дворы игровой камерой
        IEnumerator GameArena(string scene, string clip, float progress, string[] enemies, int kidIndex, bool lantern = false)
        {
            yield return LoadArena(scene);
            TimeOfDay(progress);
            if (lantern)
            {
                var fill = new GameObject("TrailerFill").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.72f, 0.78f, 1f);
                fill.intensity = 0.9f;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }
            Vector3 start = PlayerSpawner.Instance ? PlayerSpawner.Instance.StartPose(0).position : Vector3.zero;
            var kid = SpawnKid(0, kidIndex, start, 0f, balls: 3);
            Bot(kid, 4f, 7f);
            Arc(start, 9f, enemies, -60f, 60f, hp: 2);
            yield return Wait(0.6f);
            GameCam(4.5f, kid.transform, 0.55f);
            yield return Record(clip, 3.8f, 0.05f);
        }
    }
}
