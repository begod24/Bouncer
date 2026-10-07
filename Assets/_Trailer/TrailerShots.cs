using System.Collections;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using static Bouncer.Trailer.TrailerStage;

namespace Bouncer.Trailer
{
    // Кадры ролика «Мама зовёт домой». Дети: 0 Отличник, 1 Толстяк, 2 Мелкая, 3 Хулиган.
    public sealed partial class TrailerDirector
    {
        const int Otlichnik = 0, Tolstyak = 1, Melkaya = 2, Huligan = 3;

        static void Track(float seconds, Vector3 fromPos, Vector3 toPos, Vector3 fromLook, Vector3 toLook, float fov,
            float toFov = -1f)
        {
            Go(Over(seconds, t =>
            {
                float u = Ease(t);
                Shot(Vector3.Lerp(fromPos, toPos, u), Vector3.Lerp(fromLook, toLook, u),
                    toFov > 0f ? Mathf.Lerp(fov, toFov, u) : fov);
            }));
        }

        static void Follow(float seconds, Transform target, Vector3 offset, Vector3 lookOffset, float fov,
            Vector3 offsetEnd = default)
        {
            Vector3 smooth = target.position;
            Go(Over(seconds, t =>
            {
                if (target == null)
                    return;
                smooth = Vector3.Lerp(smooth, target.position, 0.12f);
                Vector3 o = offsetEnd == default ? offset : Vector3.Lerp(offset, offsetEnd, Ease(t));
                Shot(smooth + o, smooth + lookOffset, fov);
            }));
        }

        static Vector3[] Ring(Vector3 center, float radius, int count, float startDeg = 0f)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = (startDeg + 360f * i / count) * Mathf.Deg2Rad;
                points[i] = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            }
            return points;
        }

        static float YawTo(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ---------------------------------------------------------------- 1. двор на закате, катится мяч
        IEnumerator Open()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Evening", "Dusk");
            var kid = SpawnKid(0, Huligan, new Vector3(4.2f, 0f, -6.5f), 220f, balls: 0);
            kid.AutoCatch = false;
            yield return null;
            var ball = RollBall(new Vector3(-3.2f, 0.3f, -9.2f), new Vector3(2.6f, 0f, -0.25f));
            Go(Over(5f, t =>
            {
                // низко у земли: мяч, потом подъём к мальчишке на фоне заката
                float u = Ease(Mathf.InverseLerp(0.35f, 1f, t));
                Vector3 kidHead = kid.transform.position + Vector3.up * 1.1f;
                Vector3 look = Vector3.Lerp(new Vector3(-0.5f, 0.35f, -9.4f), kidHead, u);
                Shot(Vector3.Lerp(new Vector3(0.4f, 0.28f, -13.2f), new Vector3(0.3f, 0.75f, -12.6f), u), look, Mathf.Lerp(42f, 38f, u));
            }));
            Go(WalkToBall(kid, ball));
            yield return Record("u01_open", 4.6f, 0.05f);
        }

        static IEnumerator WalkToBall(TrailerPuppet kid, Ball ball)
        {
            yield return Wait(1.4f);
            while (kid.Player.Balls.Balls == 0 && ball != null && ball.State == BallState.Loose)
            {
                Vector3 to = ball.Position - kid.transform.position;
                to.y = 0f;
                kid.Move = to.magnitude > 0.3f ? to.normalized * 0.55f : Vector3.zero;
                kid.AimAt(ball.Position);
                yield return null;
            }
            kid.Move = Vector3.zero;
            yield return Wait(0.3f);
            kid.AimDirection = Vector3.back;    // к камере
        }

        // ---------------------------------------------------------------- 2. «ещё 5 минуточек»: четверо против игрушек
        IEnumerator FiveMore()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.08f, "Evening", "Dusk");
            float[] xs = { -3f, -1f, 1f, 3f };
            int[] order = { Otlichnik, Tolstyak, Melkaya, Huligan };
            var kids = new List<TrailerPuppet>();
            for (int i = 0; i < 4; i++)
            {
                var kid = SpawnKid(i, order[i], new Vector3(xs[i], 0f, -8f), 0f);
                kid.AutoCatch = false;
                kids.Add(kid);
            }
            yield return null;
            Track(4f, new Vector3(0.3f, 1.25f, -15.2f), new Vector3(0.2f, 1.35f, -14.2f),
                new Vector3(0f, 1.3f, 4f), new Vector3(0f, 1.5f, 4f), 46f);
            Go(SpawnArmy());
            yield return Record("u02_fivemore", 3.6f, 0.1f);
        }

        IEnumerator SpawnArmy()
        {
            yield return Wait(0.35f);
            var spots = new (string prefab, Vector3 p)[]
            {
                ("Pupsik", new Vector3(-4f, 0f, -1f)), ("Pupsik", new Vector3(4.5f, 0f, -0.5f)),
                ("Pupsik", new Vector3(-1.3f, 0f, -1.8f)), ("Pupsik", new Vector3(1.6f, 0f, -2f)),
                ("RolyPoly", new Vector3(-5.5f, 0f, 2.5f)), ("RolyPoly", new Vector3(5.5f, 0f, 3f)),
                ("Boss_RolyPoly_Big", new Vector3(0f, 0f, 5.5f)),
            };
            foreach (var s in spots)
            {
                Go(SpawnWithMarker(s.prefab, s.p, 180f, 0.7f));
                yield return Wait(0.18f);
            }
            Group("TinSoldier", new[] { new Vector3(-8f, 0f, 4.5f), new Vector3(-6.8f, 0f, 5.5f), new Vector3(7.5f, 0f, 5f) }, 180f);
        }

        // ---------------------------------------------------------------- 3. КИДАЙ: заряженный бросок насквозь
        IEnumerator ThrowShot()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Day");
            var kid = SpawnKid(0, Huligan, new Vector3(-7f, 0f, -4f), 90f);
            Card(kid, "Card_Mod_Slingshot");
            Card(kid, "Card_Mod_Hooligan");
            kid.AutoCatch = false;
            for (int i = 0; i < 5; i++)
                Enemy("Pupsik", new Vector3(2f + i * 1.8f, 0f, -4f + (i % 2 == 0 ? 0.4f : -0.4f)), -90f);
            yield return null;
            // держим кадр на Хулигане, пока он заряжает, и ведём за мячом после броска (~1.3 с)
            Go(Over(3.4f, t =>
            {
                float s = t * 3.4f;
                float u = Ease(Mathf.InverseLerp(1.15f, 2.3f, s));
                Vector3 pos = Vector3.Lerp(new Vector3(-5.6f, 1.25f, -9.2f), new Vector3(-1.2f, 1.5f, -10.6f), u);
                Vector3 look = Vector3.Lerp(new Vector3(-6.2f, 1f, -4f), new Vector3(4f, 0.9f, -4f), u);
                Shot(pos + new Vector3(0.4f * Mathf.Min(s, 1.15f), 0f, 0f), look, 44f);
            }));
            Go(ThrowAt(kid, new Vector3(12f, 0f, -4f), 0.25f, 1.05f));
            yield return Record("u03_throw", 3.2f, 0.1f);
        }

        static IEnumerator ThrowAt(TrailerPuppet kid, Vector3 target, float startAt, float hold)
        {
            kid.AimAt(target);
            yield return Wait(startAt);
            kid.AimAt(target);
            kid.HoldThrow = true;
            yield return Wait(hold);
            kid.AimAt(target);
            kid.HoldThrow = false;
        }

        // ---------------------------------------------------------------- 4. ЛОВИ: залп солдатиков, идеальная ловля, ответ
        IEnumerator CatchShot()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Day");
            var kid = SpawnKid(0, Melkaya, new Vector3(0f, 0f, -7f), 0f);
            Card(kid, "Card_Mod_HotPotato");
            kid.AutoCatch = true;
            // «Крышка» принимает второй мяч залпа, иначе Мелкая мигает неуязвимостью и пропадает из кадра
            Card(kid, "Card_Pas_Lid");
            Group("TinSoldier", new[] { new Vector3(-2f, 0f, -1.2f), new Vector3(0f, 0f, -0.8f), new Vector3(2f, 0f, -1.2f) }, 180f);
            yield return null;
            Follow(7f, kid.transform, new Vector3(1.1f, 1.45f, -3.1f), new Vector3(0f, 1f, 4f), 42f);
            Go(CatchThenThrow(kid));
            yield return Record("u04_catch", 6.5f, 0.1f);
        }

        static IEnumerator CatchThenThrow(TrailerPuppet kid)
        {
            int caught = 0;
            kid.Player.Balls.Caught += _ => caught++;
            while (caught == 0)
                yield return null;
            // ответ сразу, без бота: мяч обратно в среднего солдатика, «Горячая картошка» взрывается
            kid.AutoCatch = false;
            yield return ThrowAt(kid, new Vector3(0f, 0f, -0.8f), 0.3f, 0.35f);
        }

        // ---------------------------------------------------------------- 5. ВЫЖИВАЙ: толпа вокруг Толстяка
        IEnumerator SurviveShot()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Day");
            var kid = SpawnKid(0, Tolstyak, new Vector3(0f, 0f, -2f), 0f, balls: 4);
            Card(kid, "Card_Mod_Split");
            Card(kid, "Card_Mod_Bouncy");
            kid.Bot = true;
            kid.PreferredRange = 6f;
            kid.HomeRadius = 5f;
            foreach (var p in Ring(new Vector3(0f, 0f, -2f), 9f, 12, 10f))
                Enemy("Pupsik", p, YawTo(p, Vector3.zero));
            Enemy("RolyPoly", new Vector3(-8f, 0f, 6f), 160f);
            Enemy("RolyPoly", new Vector3(8f, 0f, 6f), 200f);
            Enemy("RCCar", new Vector3(-12f, 0f, -8f), 60f);
            Enemy("RCCar", new Vector3(12f, 0f, -9f), -60f);
            yield return Wait(0.8f);
            Follow(5f, kid.transform, new Vector3(0f, 7.6f, -6.6f), new Vector3(0f, 0.6f, 0.3f), 42f,
                new Vector3(0.8f, 6.6f, -5.8f));
            yield return Record("u05_survive", 4.2f, 0.1f);
        }

        // ---------------------------------------------------------------- 6. ВМЕСТЕ: четверо против волны
        IEnumerator CoopShot()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.3f, "Morning", "Day");
            var homes = new[] { new Vector3(-4f, 0f, -7f), new Vector3(-1.3f, 0f, -9f), new Vector3(1.3f, 0f, -9f), new Vector3(4f, 0f, -7f) };
            int[] order = { Otlichnik, Tolstyak, Melkaya, Huligan };
            for (int i = 0; i < 4; i++)
            {
                var kid = SpawnKid(i, order[i], homes[i], 0f, balls: 3);
                kid.Bot = true;
                kid.HomeRadius = 3.5f;
                kid.PreferredRange = 10f;
            }
            Card(Kids[1], "Card_Mod_HotPotato");
            Card(Kids[3], "Card_Mod_Slingshot");
            for (int i = 0; i < 10; i++)
                Enemy("Pupsik", new Vector3(-9f + i * 2f, 0f, 4f + (i % 3)), 180f);
            Enemy("RolyPoly", new Vector3(-6f, 0f, 9f), 180f);
            Enemy("RolyPoly", new Vector3(6f, 0f, 9f), 180f);
            Group("TinSoldier", new[] { new Vector3(-3f, 0f, 11f), new Vector3(-1f, 0f, 11.5f), new Vector3(1f, 0f, 11.5f), new Vector3(3f, 0f, 11f) }, 180f);
            Enemy("Bear", new Vector3(0f, 0f, 8f), 180f);
            yield return Wait(0.5f);
            Go(Over(5f, t =>
            {
                float a = Mathf.Lerp(-24f, 8f, Ease(t)) * Mathf.Deg2Rad;
                Vector3 center = new(0f, 0f, -3f);
                Vector3 pos = center + new Vector3(Mathf.Sin(a) * 11f, Mathf.Lerp(8f, 7f, t), -Mathf.Cos(a) * 11f);
                Shot(pos, center + new Vector3(0f, 0.5f, 1.5f), 46f);
            }));
            yield return Record("u06_coop", 4.5f, 0.1f);
        }

        // ---------------------------------------------------------------- 7. другие арены: короткие склейки
        IEnumerator ArenaShot(string scene, string clip, float progress, string[] enemies, int kidIndex, Vector3 camOffset,
            float fov = 44f, bool lantern = false)
        {
            yield return LoadArena(scene);
            TimeOfDay(progress);
            if (lantern)
            {
                // мягкий лунный заполняющий свет: ночные арены в коротких склейках иначе не читаются
                var fill = new GameObject("TrailerFill").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.72f, 0.78f, 1f);
                fill.intensity = 0.9f;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }
            Vector3 start = PlayerSpawner.Instance ? PlayerSpawner.Instance.StartPose(0).position : Vector3.zero;
            var kid = SpawnKid(0, kidIndex, start, 0f, balls: 3);
            kid.Bot = true;
            kid.HomeRadius = 4f;
            kid.PreferredRange = 7f;
            for (int i = 0; i < enemies.Length; i++)
            {
                float a = (-50f + 100f * i / Mathf.Max(1, enemies.Length - 1)) * Mathf.Deg2Rad;
                Vector3 p = start + new Vector3(Mathf.Sin(a) * 9f, 0f, Mathf.Cos(a) * 9f);
                Enemy(enemies[i], p, YawTo(p, start));
            }
            yield return Wait(0.9f);
            Follow(3.5f, kid.transform, camOffset, new Vector3(0f, 0.8f, 3f), fov, camOffset + new Vector3(0.6f, -0.4f, 0.6f));
            yield return Record(clip, 2.8f, 0.05f);
        }

        // ---------------------------------------------------------------- 8. боссы
        IEnumerator BossSplit()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0f, "Day");
            var a = SpawnKid(0, Huligan, new Vector3(-1.5f, 0f, -3.5f), 0f, balls: 4);
            var b = SpawnKid(1, Otlichnik, new Vector3(3.5f, 0f, -4f), 0f, balls: 4);
            Card(a, "Card_Mod_Hooligan");
            Card(b, "Card_Mod_Hooligan");
            var boss = Enemy("Boss_RolyPoly_Big", new Vector3(0f, 0f, 3f), 180f);
            yield return null;
            if (boss && boss.TryGetComponent(out Health h))
                h.Configure(5, 0f);
            foreach (var kid in new[] { a, b })
            {
                kid.Bot = true;
                kid.HomeRadius = 1.2f;
                kid.PreferredRange = 6f;
                kid.ForcedTarget = boss ? boss.GetComponent<Targetable>() : null;
                kid.ThrowPause = new Vector2(0.15f, 0.3f);
            }
            Track(4.5f, new Vector3(-5f, 1.0f, -9.5f), new Vector3(-3.6f, 1.2f, -8.4f),
                new Vector3(0.3f, 2.0f, 3f), new Vector3(0.3f, 1.7f, 3f), 48f);
            yield return Record("u08_split", 4.2f, 0.1f);
        }

        IEnumerator BossTransformer()
        {
            yield return LoadArena("Bazaar");
            TimeOfDay(0.2f);
            Vector3 start = PlayerSpawner.Instance ? PlayerSpawner.Instance.StartPose(0).position : Vector3.zero;
            var kid = SpawnKid(0, Melkaya, start, 0f, balls: 3);
            var kid2 = SpawnKid(1, Tolstyak, start + new Vector3(3f, 0f, -1f), 0f, balls: 3);
            foreach (var k in new[] { kid, kid2 })
            {
                k.Bot = true;
                k.HomeRadius = 5f;
                k.PreferredRange = 10f;
            }
            var boss = Enemy("Boss_Transformer", start + new Vector3(0f, 0f, 14f), 180f);
            yield return Wait(0.2f);
            Follow(9f, boss.transform, new Vector3(5.5f, 3.6f, -8.5f), new Vector3(0f, 0.8f, 0f), 50f);
            yield return Record("u08_transformer", 8.5f, 0.1f);
        }

        IEnumerator BossBabai()
        {
            // Бабай в сумерках обычного двора: в ночном финале его почти не видно
            yield return LoadArena("Yard");
            TimeOfDay(0.75f, "Evening", "Dusk");
            yield return null;
            var a = SpawnKid(0, Otlichnik, new Vector3(-2.5f, 0f, -6f), 0f, balls: 3);
            var b = SpawnKid(1, Huligan, new Vector3(2.5f, 0f, -6.5f), 0f, balls: 3);
            foreach (var k in new[] { a, b })
            {
                k.Bot = true;
                k.HomeRadius = 3f;
                k.PreferredRange = 10f;
            }
            Enemy("Boss_Dusk", new Vector3(0f, 0f, 7f), 180f);
            yield return Wait(0.6f);
            Track(6.5f, new Vector3(0f, 0.9f, -8.5f), new Vector3(0.8f, 1.1f, -7f),
                new Vector3(0f, 3f, 6f), new Vector3(0f, 3.2f, 6f), 48f);
            yield return Record("u08_babai", 6f, 0.1f);
        }

        // Замедление при ловле: GameFeel меряет время реальными часами, поэтому здесь time scale ставится вручную.
        IEnumerator SlowCatch()
        {
            yield return LoadArena("Yard");
            TimeOfDay(0.7f, "Evening", "Dusk");
            yield return null;
            var feel = Object.FindFirstObjectByType<GameFeel>();
            var kid = SpawnKid(0, Huligan, new Vector3(0f, 0f, -5f), 0f, balls: 0);
            kid.AutoCatch = true;
            Group("TinSoldier", new[] { new Vector3(-1f, 0f, 3f), new Vector3(1f, 0f, 3f) }, 180f);
            yield return null;
            // в профиль: мяч влетает справа, мальчишка ловит
            Shot(new Vector3(3.1f, 1.15f, -4.1f), new Vector3(0f, 1.1f, -3.9f), 38f);
            TrailerRecorder.Begin("u08_slowcatch");
            bool slowed = false;
            float startedAt = Now;
            while (Now - startedAt < 8f)
            {
                if (!slowed && IncomingSoon(kid, 0.35f))
                {
                    slowed = true;
                    if (feel)
                        feel.enabled = false;
                    yield return Over(0.2f, t => Time.timeScale = Mathf.Lerp(1f, 0.2f, t));
                    yield return Wait(1.6f);
                    yield return Over(0.4f, t => Time.timeScale = Mathf.Lerp(0.2f, 1f, t));
                    Time.timeScale = 1f;
                    if (feel)
                        feel.enabled = true;
                    yield return Wait(1.2f);
                    break;
                }
                yield return null;
            }
            TrailerRecorder.End();
        }

        static bool IncomingSoon(TrailerPuppet kid, float seconds)
        {
            Vector3 chest = kid.transform.position + Vector3.up;
            foreach (var ball in Ball.Active)
            {
                if (ball.State != BallState.Live || !ball.Team.IsHostileTo(Team.Player))
                    continue;
                Vector3 v = ball.Velocity;
                if (v.sqrMagnitude < 1f)
                    continue;
                float t = Vector3.Dot(chest - ball.Position, v) / v.sqrMagnitude;
                if (t > 0f && t < seconds && (ball.Position + v * t - chest).magnitude < 1.5f)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 9. финал: мама зовёт, бегом к подъезду
        IEnumerator Finale()
        {
            yield return LoadArena("Yard", 3);
            yield return null;
            var starts = new[] { new Vector3(4.5f, 0f, 2f), new Vector3(6.5f, 0f, 0.5f), new Vector3(9f, 0f, 1.5f), new Vector3(11f, 0f, 3f) };
            int[] order = { Otlichnik, Tolstyak, Melkaya, Huligan };
            for (int i = 0; i < 4; i++)
            {
                var kid = SpawnKid(i, order[i], starts[i], 0f, balls: 0);
                kid.AutoCatch = false;
            }
            var home = Bouncer.Run.HomeCall.Instance;
            var path = new List<Vector3> { new(7.6f, 0f, 13.2f), new(7.6f, 0f, 16.3f), new(7.7f, 0f, 19f), new(8.6f, 0f, 24.5f), new(9f, 0f, 27.8f), new(8f, 0f, 29.5f), new(7.5f, 0f, 30.3f) };
            yield return null;
            // бежим за детьми: сзади, чуть выше голов, через проход между гаражами к светящейся двери
            var lead = Kids[1].transform;
            Vector3 smooth = lead.position;
            Go(Over(6f, t =>
            {
                smooth = Vector3.Lerp(smooth, lead.position, 0.08f);
                Vector3 pos = new Vector3(Mathf.Lerp(smooth.x, 7.6f, 0.6f), 2.0f, smooth.z - 5.2f);
                Shot(pos, new Vector3(7.6f, 2.4f, smooth.z + 12f), 52f);
            }));
            Go(FinaleAction(home, path));
            yield return Record("u09_finale", 5.6f, 0.1f);
        }

        IEnumerator FinaleAction(Bouncer.Run.HomeCall home, List<Vector3> path)
        {
            yield return Wait(0.5f);
            if (home)
            {
                home.BeginCall(true);
                home.enabled = false;   // окна уже горят; бег к двери ведём сами, без победного экрана
            }
            yield return Wait(0.6f);
            float[] delays = { 0f, 0.12f, 0.3f, 0.05f };
            float[] side = { -0.5f, 0.4f, -0.2f, 0.6f };
            for (int i = 0; i < Kids.Count; i++)
            {
                var kid = Kids[i];
                var p = new List<Vector3>();
                foreach (var q in path)
                    p.Add(q + new Vector3(side[i], 0f, 0f));
                Go(Delayed(delays[i], RunPath(kid, p, 4.6f + 0.2f * i)));
            }
        }

        static IEnumerator Delayed(float seconds, IEnumerator routine)
        {
            yield return Wait(seconds);
            yield return routine;
        }
    }
}
