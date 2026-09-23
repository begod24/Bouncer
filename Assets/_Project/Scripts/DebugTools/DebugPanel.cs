using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Enemies;
using Bouncer.Player;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.DebugTools
{
    /// <summary>
    /// Отладочное окно (F1): ползунки для тюнинга прямо в игре, спавн, бессмертие, скорость времени.
    /// Ползунки меняют ScriptableObject-ассеты напрямую; кнопка «Сохранить» записывает их на диск.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        static readonly string[] Tabs = { "Игрок", "Мяч", "Враги", "Мир" };

        [SerializeField] bool visible;
        [SerializeField] float referenceHeight = 1080f;

        Rect _window = new(24f, 250f, 470f, 620f);
        Vector2 _scroll;
        int _tab;
        PlayerController _player;
        SimpleWaveSpawner _spawner;
        BallLauncher _launcher;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.f1Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame))
                visible = !visible;
            if (!visible)
                GameSettings.PointerOverDebugUI = false;

            if (_player == null)
                _player = FindFirstObjectByType<PlayerController>();
            if (_spawner == null)
                _spawner = FindFirstObjectByType<SimpleWaveSpawner>();
            if (_launcher == null)
                _launcher = FindFirstObjectByType<BallLauncher>();
        }

        void OnDisable() => GameSettings.PointerOverDebugUI = false;

        void OnGUI()
        {
            if (!visible)
                return;
            float scale = Screen.height / referenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUILayout.Window(0x0B0B, _window, DrawWindow, "Отладка — F1");
            if (Event.current.type == EventType.Repaint)
                GameSettings.PointerOverDebugUI = _window.Contains(Event.current.mousePosition);
        }

        void DrawWindow(int id)
        {
            _tab = GUILayout.Toolbar(_tab, Tabs);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawPlayer(); break;
                case 1: DrawBall(); break;
                case 2: DrawEnemies(); break;
                default: DrawWorld(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        void DrawPlayer()
        {
            if (_player == null)
            {
                GUILayout.Label("Игрок не найден");
                return;
            }
            var s = _player.Stats;
            Header("Движение");
            s.moveSpeed = Slider("Скорость бега", s.moveSpeed, 2f, 14f);
            s.acceleration = Slider("Разгон", s.acceleration, 10f, 200f, "0");
            s.deceleration = Slider("Торможение", s.deceleration, 10f, 200f, "0");
            Header("Рывок");
            s.dashDistance = Slider("Дистанция", s.dashDistance, 1f, 10f);
            s.dashDuration = Slider("Длительность", s.dashDuration, 0.05f, 0.5f);
            s.dashCooldown = Slider("Перезарядка", s.dashCooldown, 0f, 3f);
            s.dashInvulnerability = Slider("Неуязвимость", s.dashInvulnerability, 0f, 0.6f);
            Header("Бросок");
            s.tapThreshold = Slider("Порог заряда", s.tapThreshold, 0.05f, 0.5f);
            s.chargeTime = Slider("Время заряда", s.chargeTime, 0.1f, 2f);
            s.chargingMoveMultiplier = Slider("Скорость при заряде", s.chargingMoveMultiplier, 0f, 1f);
            s.throwCooldown = Slider("Пауза между бросками", s.throwCooldown, 0f, 1f);
            Header("Ловля и подбор");
            s.catchWindow = Slider("Окно ловли", s.catchWindow, 0.05f, 1f);
            s.catchRadius = Slider("Радиус ловли", s.catchRadius, 0.5f, 3.5f);
            s.catchMissCooldown = Slider("Штраф за промах", s.catchMissCooldown, 0f, 2f);
            s.candleCatchHeight = Slider("Высота свечки", s.candleCatchHeight, 1f, 6f);
            s.pickupRadius = Slider("Радиус подбора", s.pickupRadius, 0.3f, 3f);
            s.maxBalls = IntSlider("Макс. мячей", s.maxBalls, 1, 8);
            Header("Прицел");
            s.autoAimAngle = Slider("Угол автоприцела", s.autoAimAngle, 5f, 90f, "0");
            s.autoAimRange = Slider("Дальность автоприцела", s.autoAimRange, 5f, 40f, "0");

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 мяч"))
                _player.Balls.GiveBall();
            if (GUILayout.Button("Вылечить"))
                _player.Health.Heal(99);
            GUILayout.EndHorizontal();
            GameSettings.GodMode = GUILayout.Toggle(GameSettings.GodMode, " Бессмертие");
            SaveButton(s);
        }

        void DrawBall()
        {
            var d = _player ? _player.Balls.BallDefinition : null;
            if (d == null)
            {
                GUILayout.Label("Определение мяча не найдено");
                return;
            }
            Header("Бросок (обычный → заряженный)");
            d.speed = Slider("Скорость", d.speed, 5f, 45f, "0.0");
            d.chargedSpeed = Slider("Скорость заряженного", d.chargedSpeed, 5f, 60f, "0.0");
            d.upVelocity = Slider("Подброс вверх", d.upVelocity, 0f, 5f);
            d.liveGravity = Slider("Гравитация полёта", d.liveGravity, 0f, 15f);
            d.chargedLiveGravity = Slider("Гравитация заряженного", d.chargedLiveGravity, 0f, 15f);
            Header("Урон и отброс");
            d.damage = IntSlider("Урон", d.damage, 0, 5);
            d.chargedDamage = IntSlider("Урон заряженного", d.chargedDamage, 0, 5);
            d.candleDamage = IntSlider("Урон после свечки", d.candleDamage, 0, 8);
            d.knockback = Slider("Отброс", d.knockback, 0f, 30f, "0.0");
            d.chargedKnockback = Slider("Отброс заряженного", d.chargedKnockback, 0f, 40f, "0.0");
            d.candleKnockback = Slider("Отброс после свечки", d.candleKnockback, 0f, 50f, "0.0");
            Header("Рикошеты");
            d.maxRicochets = IntSlider("Макс. рикошетов", d.maxRicochets, 0, 10);
            d.wallSpeedKeep = Slider("Сохранение скорости", d.wallSpeedKeep, 0.3f, 1f);
            d.minLiveSpeed = Slider("Мин. опасная скорость", d.minLiveSpeed, 0f, 20f, "0.0");
            Header("Свечка");
            d.popUpSpeed = Slider("Подброс от тела", d.popUpSpeed, 0f, 20f, "0.0");
            d.popHorizontalKeep = Slider("Отлёт в сторону", d.popHorizontalKeep, 0f, 1f);
            d.popGravity = Slider("Гравитация свечки", d.popGravity, 2f, 30f, "0.0");

            GUILayout.Space(6f);
            if (GUILayout.Button($"Убрать мячи с пола ({Ball.LooseCount})"))
                Ball.DespawnAllLoose();
            SaveButton(d);
        }

        void DrawEnemies()
        {
            var roly = _spawner && _spawner.EnemyPrefab ? _spawner.EnemyPrefab.GetComponent<RolyPolyEnemy>() : null;
            var e = roly ? roly.Definition : null;
            if (e != null)
            {
                Header($"{e.displayName} (для новых врагов)");
                e.moveSpeed = Slider("Скорость", e.moveSpeed, 0.5f, 8f);
                e.hitsToKill = IntSlider("Попаданий до смерти", e.hitsToKill, 1, 8);
                e.comboResetTime = Slider("Сброс серии, с", e.comboResetTime, 0.5f, 8f);
                e.attackRange = Slider("Дальность атаки", e.attackRange, 0.8f, 4f);
                e.windupTime = Slider("Замах", e.windupTime, 0.1f, 1.5f);
                e.attackCooldown = Slider("Пауза между атаками", e.attackCooldown, 0.2f, 4f);
                e.tipPerImpulse = Slider("Опрокидывание", e.tipPerImpulse, 0f, 2f);
                e.hitLift = Slider("Подброс от удара", e.hitLift, 0f, 1f);
                e.stunTime = Slider("Оглушение", e.stunTime, 0f, 3f);
                e.uprightStrength = Slider("Равновесие", e.uprightStrength, 0f, 200f, "0");
                SaveButton(e);
            }

            if (_spawner)
            {
                Header("Спавнер");
                _spawner.Spawning = GUILayout.Toggle(_spawner.Spawning, " Спавнить врагов");
                _spawner.SpawnInterval = Slider("Интервал", _spawner.SpawnInterval, 0.5f, 15f, "0.0");
                _spawner.MaxAlive = IntSlider("Лимит живых (база)", _spawner.MaxAlive, 1, 30);
                GUILayout.Label($"Живых: {_spawner.AliveCount} / {_spawner.EffectiveMaxAlive}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Спавн неваляшки"))
                    _spawner.QueueSpawn();
                if (GUILayout.Button("Убить всех"))
                    _spawner.KillAll();
                GUILayout.EndHorizontal();
            }

            if (_launcher)
            {
                Header("Тестовая пушка");
                _launcher.Firing = GUILayout.Toggle(_launcher.Firing, " Стреляет");
                _launcher.Interval = Slider("Интервал", _launcher.Interval, 0.5f, 8f, "0.0");
                _launcher.Speed = Slider("Скорость мяча", _launcher.Speed, 5f, 30f, "0.0");
            }
        }

        void DrawWorld()
        {
            Header("Время");
            GameFeel.DebugTimeScale = Slider("Скорость времени", GameFeel.DebugTimeScale, 0.1f, 2f);
            if (GUILayout.Button("Нормальная скорость"))
                GameFeel.DebugTimeScale = 1f;
            Header("Ощущения");
            GameFeel.ShakeMultiplier = Slider("Тряска камеры", GameFeel.ShakeMultiplier, 0f, 3f);
            Header("Настройки игрока");
            bool changed = false;
            changed |= Toggle(ref GameSettings.AutoAimMouse, " Автоприцел для мыши");
            changed |= Toggle(ref GameSettings.AutoAimGamepad, " Автоприцел для геймпада");
            changed |= Toggle(ref GameSettings.ShowAimPreview, " Линия прицела");
            if (changed)
                GameSettings.Save();
            GUILayout.Space(10f);
            if (GUILayout.Button("Перезапустить забег") && GameSession.Instance)
                GameSession.Instance.Restart();
        }

        // ---------- Виджеты ----------

        static void Header(string text)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"<b>{text}</b>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        static float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(170f));
            GUILayout.Label(value.ToString(format), GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return value;
        }

        static int IntSlider(string label, int value, int min, int max) =>
            Mathf.RoundToInt(Slider(label, value, min, max, "0"));

        static bool Toggle(ref bool value, string label)
        {
            bool next = GUILayout.Toggle(value, label);
            bool changed = next != value;
            value = next;
            return changed;
        }

        static void SaveButton(Object asset)
        {
#if UNITY_EDITOR
            if (GUILayout.Button("Сохранить значения в ассет"))
            {
                UnityEditor.EditorUtility.SetDirty(asset);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
            }
#endif
        }
    }
}
