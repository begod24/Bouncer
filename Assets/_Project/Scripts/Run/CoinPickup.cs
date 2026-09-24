using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Монетка на асфальте (1 тиын, 5 тиын, 1 тенге). Выпрыгивает из выбитого врага, крутится над землёй и летит
    /// к игроку, когда он рядом: радиус магнита растёт от «Длинных рук». Когда арена пройдена, оставшиеся
    /// слетаются сами (<see cref="CollectAll"/>). Физики нет — движется сама, поэтому монеток может быть много.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinPickup : MonoBehaviour, IPoolable
    {
        static readonly List<CoinPickup> s_active = new();
        static readonly List<PlayerController> s_players = new();
        static int s_playersFrame = -1;

        [Tooltip("Сколько монеток (тиынов) стоит")]
        [SerializeField, Min(1)] int value = 1;
        [Tooltip("Модель: крутится и покачивается, корень остаётся на месте")]
        [SerializeField] Transform visual;

        [Header("Выпрыгивание")]
        [SerializeField] float popSpeed = 2.4f;
        [SerializeField] float popUpSpeed = 5.5f;
        [SerializeField] float gravity = 22f;
        [Tooltip("На какой высоте над асфальтом монетка висит")]
        [SerializeField] float hoverHeight = 0.35f;

        [Header("Магнит")]
        [Tooltip("С какого расстояния монетка летит к игроку; умножается на радиус подбора игрока («Длинные руки»)")]
        [SerializeField] float magnetRadius = 2.6f;
        [Tooltip("Первые доли секунды магнит не тянет — видно, что монетка выпала")]
        [SerializeField] float magnetDelay = 0.3f;
        [SerializeField] float flySpeed = 6f;
        [SerializeField] float flyAcceleration = 45f;
        [SerializeField] float collectDistance = 0.55f;

        [Header("Вид")]
        [SerializeField] float spinSpeed = 220f;
        [SerializeField] float bobHeight = 0.07f;
        [SerializeField] float bobFrequency = 2.2f;

        Vector3 _velocity;
        bool _landed;
        bool _flying;
        float _speed;
        float _spawnTime;
        float _phase;
        PlayerController _target;

        public int Value => value;
        public static int ActiveCount => s_active.Count;

        /// <summary>Арена пройдена: все монетки летят к ближайшему игроку.</summary>
        public static void CollectAll()
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
                s_active[i].StartFlying(NearestPlayer(s_active[i].transform.position, float.PositiveInfinity));
        }

        public void OnSpawned()
        {
            s_active.Add(this);
            Vector2 side = Random.insideUnitCircle * popSpeed;
            _velocity = new Vector3(side.x, popUpSpeed * Random.Range(0.8f, 1.1f), side.y);
            _landed = false;
            _flying = false;
            _target = null;
            _speed = 0f;
            _spawnTime = Time.time;
            _phase = Random.value * 10f;
            if (visual)
                visual.localRotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        }

        public void OnDespawned() => s_active.Remove(this);

        void OnDisable() => s_active.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            Vector3 position = transform.position;
            if (_flying)
            {
                if (_target == null || _target.IsDead)
                {
                    _flying = false;
                    _landed = false;
                    _velocity = Vector3.zero;
                }
                else
                {
                    Vector3 goal = _target.transform.position + Vector3.up * 0.8f;
                    Vector3 delta = goal - position;
                    float distance = delta.magnitude;
                    if (distance <= collectDistance)
                    {
                        Collect();
                        return;
                    }
                    _speed += flyAcceleration * dt;
                    position += delta / distance * Mathf.Min(distance, _speed * dt);
                }
            }
            else
            {
                if (!_landed)
                {
                    _velocity.y -= gravity * dt;
                    position += _velocity * dt;
                    if (position.y <= hoverHeight && _velocity.y < 0f)
                    {
                        position.y = hoverHeight;
                        _landed = true;
                    }
                }
                if (Time.time - _spawnTime >= magnetDelay)
                {
                    var player = NearestPlayer(position, magnetRadius, scaleByPickup: true);
                    if (player != null)
                        StartFlying(player);
                }
            }
            transform.position = position;
            Animate(dt);
        }

        void Animate(float dt)
        {
            if (!visual)
                return;
            _phase += dt * bobFrequency * Mathf.PI * 2f;
            visual.localRotation = Quaternion.Euler(0f, spinSpeed * dt, 0f) * visual.localRotation;
            visual.localPosition = new Vector3(0f, _landed && !_flying ? Mathf.Sin(_phase) * bobHeight : 0f, 0f);
        }

        void StartFlying(PlayerController player)
        {
            if (player == null || _flying)
                return;
            _target = player;
            _flying = true;
            _speed = flySpeed;
        }

        void Collect()
        {
            RunState.AddCoins(value);
            GameEvents.PlaySound(SoundCue.Coin, transform.position);
            PoolService.Despawn(gameObject);
        }

        /// <summary>Ближайший живой игрок в радиусе (с учётом «Длинных рук», если scaleByPickup).</summary>
        static PlayerController NearestPlayer(Vector3 from, float radius, bool scaleByPickup = false)
        {
            if (s_playersFrame != Time.frameCount)
            {
                s_playersFrame = Time.frameCount;
                s_players.Clear();
                foreach (var target in Targetable.All)
                    if (target.Team == Team.Player && target.IsAlive && target.TryGetComponent(out PlayerController player))
                        s_players.Add(player);
            }
            PlayerController best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (var player in s_players)
            {
                Vector3 delta = player.transform.position - from;
                delta.y = 0f;
                float r = scaleByPickup ? radius * player.Modifiers.PickupRadius : radius;
                float sqr = delta.sqrMagnitude;
                if (sqr > r * r || sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = player;
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_active.Clear();
            s_players.Clear();
            s_playersFrame = -1;
        }
    }
}
