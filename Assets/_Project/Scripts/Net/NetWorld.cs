using System.Collections.Generic;
using Bouncer.Arena;
using Bouncer.Core;
using Bouncer.Enemies;
using Bouncer.Player;
using Bouncer.Run;
using Bouncer.Visuals;
using Bouncer.Waves;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    public sealed class NetWorld : NetworkBehaviour
    {
        const float ArenaSyncInterval = 1f;
        const int CartEveryTicks = 3;
        const float CartRefresh = 2f;
        const int MaxSoundsPerTick = 24;

        const byte EffectRing = 0;
        const byte EffectBurst = 1;
        const byte EffectDebris = 2;
        const byte EffectGum = 3;
        const byte EffectCircle = 4;
        const byte EffectLine = 5;
        const byte EffectMarkerHide = 6;
        const byte EffectSpawn = 7;
        const byte EffectCloud = 8;
        const byte EffectGiantBall = 9;
        const byte EffectMine = 10;
        const byte EffectCarrot = 11;
        const byte EffectChalk = 12;

        [Tooltip("Эффекты, которые хозяин показывает гостям: кольца, взрывы, обломки, жвачка")]
        [SerializeField] GameObject[] effects;
        [Tooltip("Монетки (по номиналу) и лимонад")]
        [SerializeField] CoinPickup[] coins;
        [SerializeField] HealPickup[] heals;

        struct PendingSound
        {
            public SoundCue Cue;
            public Vector3 Position;
        }

        readonly List<PendingSound> _sounds = new();
        readonly List<NetEffect> _effects = new();
        readonly Dictionary<CoinPickup, ushort> _coinIds = new();
        readonly Dictionary<ushort, CoinPickup> _coinPuppets = new();
        readonly Dictionary<HealPickup, ushort> _healIds = new();
        readonly Dictionary<ushort, HealPickup> _healPuppets = new();
        readonly Dictionary<GroundMarker, ushort> _markerIds = new();
        readonly Dictionary<ushort, GroundMarker> _markerPuppets = new();
        ushort _nextMarkerId;
        readonly Dictionary<PortfolioPickup, ushort> _portfolioIds = new();
        readonly Dictionary<ushort, PortfolioPickup> _portfolioPuppets = new();
        readonly List<Vector3> _markerPositions = new();
        ushort _nextPickupId;
        float _nextArenaSync;
        WaveSpawner _spawner;
        WeatherController _weather;
        LootDropper _loot;
        readonly List<PushCart> _carts = new();
        readonly List<Vector3> _cartSent = new();
        readonly List<Vector3> _cartTarget = new();
        readonly List<float> _cartTargetYaw = new();
        readonly List<byte> _cartOutIndex = new();
        readonly List<Vector3> _cartOutPosition = new();
        readonly List<float> _cartOutYaw = new();
        bool _cartsFound;
        int _tick;
        float _nextCartRefresh;

        public override void OnNetworkSpawn()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (IsServer)
            {
                NetworkManager.NetworkTickSystem.Tick += OnTick;
                GameEvents.SoundRequested += OnSound;
                GameEvents.WorldAnnounced += OnAnnounced;
                ExpandingRing.Played += OnRing;
                ParticleBurst.Played += OnBurst;
                Debris.Scattered += OnDebris;
                GumSpot.Dropped += OnGum;
                ChalkMark.Dropped += OnChalk;
                GroundMarker.Shown += OnMarkerShown;
                GroundMarker.Hidden += OnMarkerHidden;
                WaveSpawner.GroupQueued += OnGroupQueued;
                ArenaDirector.Completed += OnArenaCompleted;
                CoinPickup.Spawned += OnCoinSpawned;
                CoinPickup.FlyStarted += OnCoinFlying;
                CoinPickup.Collected += OnCoinCollected;
                HealPickup.Spawned += OnHealSpawned;
                HealPickup.Taken += OnHealTaken;
                PortfolioPickup.Spawned += OnPortfolioSpawned;
                PortfolioPickup.Taken += OnPortfolioTaken;
                WeatherController.Struck += OnLightning;
                LightZone.WentOut += OnLampOut;
                LightsOut.Triggered += OnLightsOut;
                HomeCall.CallStarted += OnMomCall;
                BreakEvents.Broken += OnBroken;
                SlowCloud.Played += OnCloud;
                GiantBall.Launched += OnGiantBall;
                BatteryMine.Thrown += OnMine;
                CarrotBoomerang.Thrown += OnCarrot;
                Targetable.EnemiesFroze += OnEnemiesFroze;
                NetHooks.HitRemotePlayer = (go, hit) => go.TryGetComponent(out NetPlayer player) && player.SendHit(hit);
                NetHooks.HealRemotePlayer = (go, amount) => go.TryGetComponent(out NetPlayer player) && player.SendHeal(amount);
                NetHooks.GivePortfolio = go => go.TryGetComponent(out NetPlayer player) && player.SendPortfolio();
                NetHooks.MirrorSpawn = OnMirrorSpawn;
            }
            else
            {
                GameEvents.LocalSoundFilter = IsLocalCue;
                GameEvents.MuteLocalAnnouncements = true;
            }
        }

        public override void OnNetworkDespawn() => Unhook();

        public override void OnDestroy()
        {
            Unhook();
            base.OnDestroy();
        }

        void Unhook()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                NetworkManager.NetworkTickSystem.Tick -= OnTick;
            GameEvents.SoundRequested -= OnSound;
            GameEvents.WorldAnnounced -= OnAnnounced;
            ExpandingRing.Played -= OnRing;
            ParticleBurst.Played -= OnBurst;
            Debris.Scattered -= OnDebris;
            GumSpot.Dropped -= OnGum;
            ChalkMark.Dropped -= OnChalk;
            GroundMarker.Shown -= OnMarkerShown;
            GroundMarker.Hidden -= OnMarkerHidden;
            WaveSpawner.GroupQueued -= OnGroupQueued;
            ArenaDirector.Completed -= OnArenaCompleted;
            CoinPickup.Spawned -= OnCoinSpawned;
            CoinPickup.FlyStarted -= OnCoinFlying;
            CoinPickup.Collected -= OnCoinCollected;
            HealPickup.Spawned -= OnHealSpawned;
            HealPickup.Taken -= OnHealTaken;
            PortfolioPickup.Spawned -= OnPortfolioSpawned;
            PortfolioPickup.Taken -= OnPortfolioTaken;
            WeatherController.Struck -= OnLightning;
            LightZone.WentOut -= OnLampOut;
            LightsOut.Triggered -= OnLightsOut;
            HomeCall.CallStarted -= OnMomCall;
            BreakEvents.Broken -= OnBroken;
            SlowCloud.Played -= OnCloud;
            GiantBall.Launched -= OnGiantBall;
            BatteryMine.Thrown -= OnMine;
            CarrotBoomerang.Thrown -= OnCarrot;
            Targetable.EnemiesFroze -= OnEnemiesFroze;
            if (IsServer)
            {
                NetHooks.HitRemotePlayer = null;
                NetHooks.HealRemotePlayer = null;
                NetHooks.GivePortfolio = null;
                if (NetHooks.MirrorSpawn == (System.Action<GameObject>)OnMirrorSpawn)
                    NetHooks.MirrorSpawn = null;
            }
            if (GameEvents.LocalSoundFilter == (System.Func<SoundCue, bool>)IsLocalCue)
            {
                GameEvents.LocalSoundFilter = null;
                GameEvents.MuteLocalAnnouncements = false;
            }
        }

        void OnSceneChanged(Scene previous, Scene next)
        {
            _sounds.Clear();
            _effects.Clear();
            _coinIds.Clear();
            _coinPuppets.Clear();
            _healIds.Clear();
            _healPuppets.Clear();
            _portfolioIds.Clear();
            _portfolioPuppets.Clear();
            _markerIds.Clear();
            _markerPuppets.Clear();
            _spawner = null;
            _weather = null;
            _loot = null;
            _carts.Clear();
            _cartsFound = false;
        }

        static bool IsLocalCue(SoundCue cue) => cue is SoundCue.Throw or SoundCue.ThrowCharged or SoundCue.ThrowCandle
            or SoundCue.Catch or SoundCue.CatchCandle or SoundCue.CatchMiss or SoundCue.Pickup or SoundCue.Dash
            or SoundCue.PlayerHurt or SoundCue.PlayerKnockedOut or SoundCue.BallWall or SoundCue.LevelUp or SoundCue.CardPick
            or SoundCue.UiMove or SoundCue.Victory or SoundCue.GameOver or SoundCue.Portfolio or SoundCue.KioskOpen
            or SoundCue.Purchase or SoundCue.NotEnoughCoins or SoundCue.SecondWind or SoundCue.ShieldBlock or SoundCue.Whistle
            or SoundCue.FenceRise or SoundCue.FenceKnock or SoundCue.BubbleBlow or SoundCue.BubblePop or SoundCue.CapGun
            or SoundCue.CameraFlash or SoundCue.MagnetPull or SoundCue.Rewind or SoundCue.ElasticTwang or SoundCue.RadioCrackle
            or SoundCue.TamagotchiBeep or SoundCue.SpinWhoosh or SoundCue.AbilityNotReady;

        void OnTick()
        {
            if (!IsServer || !IsSpawned)
                return;
            if (_sounds.Count > 0)
            {
                var batch = new NetSoundBatch { Cues = new ushort[_sounds.Count], Positions = new Vector3[_sounds.Count] };
                for (int i = 0; i < _sounds.Count; i++)
                {
                    batch.Cues[i] = (ushort)_sounds[i].Cue;
                    batch.Positions[i] = _sounds[i].Position;
                }
                _sounds.Clear();
                SoundsRpc(batch);
            }
            if (_effects.Count > 0)
            {
                EffectsRpc(new NetEffectBatch { Effects = _effects });
                _effects.Clear();
            }
            if (++_tick % CartEveryTicks == 0)
                SendCarts();
            if (Time.unscaledTime >= _nextArenaSync)
            {
                _nextArenaSync = Time.unscaledTime + ArenaSyncInterval;
                var director = ArenaDirector.Instance;
                float frozen = Targetable.EnemiesFrozen ? Targetable.EnemiesFrozenLeft : 0f;
                if (director != null)
                    ArenaRpc(director.ArenaTime, frozen, (byte)director.Weather, director.WeatherSeed);
            }
        }

        public void SendStateTo(ulong clientId)
        {
            if (!IsServer || !IsSpawned)
                return;
            foreach (var pair in _coinIds)
            {
                var coin = pair.Key;
                int index = coin != null && coin.isActiveAndEnabled ? CoinIndex(coin.Value) : -1;
                if (index >= 0)
                    CoinSpawnRpc(pair.Value, (byte)index, coin.transform.position, Vector3.zero, To(clientId));
            }
            foreach (var pair in _healIds)
            {
                var heal = pair.Key;
                int index = heal != null && heal.isActiveAndEnabled ? HealIndex(heal) : -1;
                if (index >= 0)
                    HealSpawnRpc(pair.Value, (byte)index, heal.transform.position, To(clientId));
            }
            foreach (var pair in _portfolioIds)
                if (pair.Key != null && pair.Key.isActiveAndEnabled)
                    PortfolioSpawnRpc(pair.Value, pair.Key.transform.position, Vector3.zero, To(clientId));

            var director = ArenaDirector.Instance;
            if (director != null)
                ArenaRpc(director.ArenaTime, Targetable.EnemiesFrozen ? Targetable.EnemiesFrozenLeft : 0f, (byte)director.Weather,
                    director.WeatherSeed, To(clientId));
            if (LightsOut.Active)
                LightsOutRpc(LightsOut.Left, To(clientId));
            foreach (var lamp in LightZone.All)
                if (lamp != null && !lamp.IsOn)
                    LampOutRpc(lamp.Center, lamp.OutLeft, To(clientId));
            var home = HomeCall.Instance;
            if (home != null && home.Called)
                MomCallRpc(home.BossDefeated, To(clientId));
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour is IBreakable { IsBroken: true })
                    BreakRpc(behaviour.transform.position, Vector3.forward, 0.5f, To(clientId));
        }

        RpcParams To(ulong clientId) => RpcTarget.Single(clientId, RpcTargetUse.Temp);

        void OnSound(SoundCue cue, Vector3 position)
        {
            if (IsLocalCue(cue) || _sounds.Count >= MaxSoundsPerTick)
                return;
            _sounds.Add(new PendingSound { Cue = cue, Position = position });
        }

        void OnAnnounced(Announcement announcement) => AnnounceRpc(Fixed(announcement.Title), Fixed(announcement.Hint), announcement.Seconds);

        static FixedString64Bytes Fixed(string text)
        {
            var value = new FixedString64Bytes();
            if (!string.IsNullOrEmpty(text))
                value.CopyFromTruncated(text);
            return value;
        }

        void OnCloud(SlowCloud cloud, float radius, float life, float slow) =>
            QueueEffect(EffectCloud, cloud.gameObject, cloud.transform.position, cloud.transform.rotation, radius,
                new Vector3(life, slow, 0f), default);

        void OnMine(BatteryMine mine, Vector3 from, Vector3 to, float flight, float fuse, float radius) =>
            QueueEffect(EffectMine, mine.gameObject, from, Quaternion.identity, flight, to, new Vector3(fuse, radius, 0f));

        void OnCarrot(CarrotBoomerang carrot, Vector3 start, Vector3 far, float flight, float radius) =>
            QueueEffect(EffectCarrot, carrot.gameObject, start, Quaternion.identity, flight, far, new Vector3(radius, 0f, 0f));

        void OnGiantBall(GiantBall ball, Vector3 direction) =>
            QueueEffect(EffectGiantBall, ball.gameObject, ball.transform.position, ball.transform.rotation, 0f, direction, default);

        void OnEnemiesFroze(float seconds)
        {
            if (IsSpawned)
                FreezeAllRpc(seconds);
        }

        void OnMirrorSpawn(GameObject instance) =>
            QueueEffect(EffectSpawn, instance, instance.transform.position, instance.transform.rotation, 0f, default, default);

        void OnRing(ExpandingRing ring, float radius) =>
            QueueEffect(EffectRing, ring.gameObject, ring.transform.position, ring.transform.rotation, radius, default, default);

        void OnBurst(ParticleBurst burst, float scale) =>
            QueueEffect(EffectBurst, burst.gameObject, burst.transform.position, burst.transform.rotation, scale, default, default);

        void OnDebris(Debris debris, Vector3 direction, float force, Vector3 inherited) =>
            QueueEffect(EffectDebris, debris.gameObject, debris.transform.position, debris.transform.rotation, force, direction, inherited);

        void OnGum(GumSpot prefab, Vector3 above)
        {
            int index = IndexOf(prefab != null ? prefab.gameObject : null);
            if (index >= 0)
                _effects.Add(new NetEffect { Kind = EffectGum, Prefab = (ushort)index, Position = above, Rotation = Quaternion.identity });
        }

        void OnChalk(ChalkMark prefab, Vector3 above)
        {
            int index = IndexOf(prefab != null ? prefab.gameObject : null);
            if (index >= 0)
                _effects.Add(new NetEffect { Kind = EffectChalk, Prefab = (ushort)index, Position = above, Rotation = Quaternion.identity });
        }

        void OnMarkerShown(GroundMarker marker, bool circle, Vector3 a, Vector3 b, float seconds)
        {
            if (!IsSpawned || !marker.TryGetComponent(out PooledObject tag))
                return;
            int index = IndexOf(tag.Prefab);
            if (index < 0)
                return;
            do
                _nextMarkerId++;
            while (_nextMarkerId == 0);
            _markerIds[marker] = _nextMarkerId;
            _effects.Add(new NetEffect
            {
                Kind = circle ? EffectCircle : EffectLine,
                Prefab = (ushort)index,
                Position = a,
                Rotation = Quaternion.identity,
                Value = seconds,
                Direction = circle ? new Vector3(a.x, b.x, 0f) : b,
                Inherited = new Vector3(_nextMarkerId, 0f, 0f),
            });
        }

        void OnMarkerHidden(GroundMarker marker)
        {
            if (!IsSpawned || !_markerIds.TryGetValue(marker, out ushort id))
                return;
            _markerIds.Remove(marker);
            _effects.Add(new NetEffect { Kind = EffectMarkerHide, Rotation = Quaternion.identity, Inherited = new Vector3(id, 0f, 0f) });
        }

        void QueueEffect(byte kind, GameObject instance, Vector3 position, Quaternion rotation, float value, Vector3 direction,
            Vector3 inherited)
        {
            if (!IsSpawned || !instance.TryGetComponent(out PooledObject tag))
                return;
            int index = IndexOf(tag.Prefab);
            if (index < 0)
                return;
            _effects.Add(new NetEffect
            {
                Kind = kind,
                Prefab = (ushort)index,
                Position = position,
                Rotation = rotation,
                Value = value,
                Direction = direction,
                Inherited = inherited,
            });
        }

        int IndexOf(GameObject prefab)
        {
            if (prefab == null)
                return -1;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] == prefab)
                    return i;
            return -1;
        }

        void OnGroupQueued(bool elite, IReadOnlyList<Vector3> positions)
        {
            if (!IsSpawned)
                return;
            var array = new Vector3[positions.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = positions[i];
            MarkersRpc(elite, array);
        }

        void OnArenaCompleted(bool boss, bool last)
        {
            if (IsSpawned)
                CompletedRpc(boss, last);
        }

        void FindCarts()
        {
            if (_cartsFound)
                return;
            _cartsFound = true;
            _carts.Clear();
            _carts.AddRange(FindObjectsByType<PushCart>(FindObjectsSortMode.None));
            _carts.Sort((a, b) =>
            {
                int x = a.StartPosition.x.CompareTo(b.StartPosition.x);
                return x != 0 ? x : a.StartPosition.z.CompareTo(b.StartPosition.z);
            });
            _cartSent.Clear();
            _cartTarget.Clear();
            _cartTargetYaw.Clear();
            foreach (var cart in _carts)
            {
                _cartSent.Add(new Vector3(float.MaxValue, 0f, 0f));
                _cartTarget.Add(cart.transform.position);
                _cartTargetYaw.Add(cart.transform.eulerAngles.y);
                if (!IsServer)
                    cart.BecomePuppet();
            }
        }

        void SendCarts()
        {
            FindCarts();
            if (_carts.Count == 0)
                return;
            bool refresh = Time.unscaledTime >= _nextCartRefresh;
            if (refresh)
                _nextCartRefresh = Time.unscaledTime + CartRefresh;
            _cartOutIndex.Clear();
            _cartOutPosition.Clear();
            _cartOutYaw.Clear();
            for (int i = 0; i < _carts.Count && i < 255; i++)
            {
                var cart = _carts[i];
                if (cart == null)
                    continue;
                Vector3 position = cart.Body ? cart.Body.position : cart.transform.position;
                float yaw = (cart.Body ? cart.Body.rotation : cart.transform.rotation).eulerAngles.y;
                if (!refresh && (position - _cartSent[i]).sqrMagnitude < 1e-4f)
                    continue;
                _cartSent[i] = position;
                _cartOutIndex.Add((byte)i);
                _cartOutPosition.Add(position);
                _cartOutYaw.Add(yaw);
            }
            if (_cartOutIndex.Count > 0)
                CartsRpc(_cartOutIndex.ToArray(), _cartOutPosition.ToArray(), _cartOutYaw.ToArray());
        }

        void OnBroken(Component prop, Vector3 direction, float force)
        {
            if (IsSpawned && prop != null)
                BreakRpc(prop.transform.position, direction, force);
        }

        void Update()
        {
            if (!IsSpawned || IsServer)
                return;
            FindCarts();
            float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
            for (int i = 0; i < _carts.Count; i++)
            {
                var cart = _carts[i];
                if (cart == null)
                    continue;
                var t = cart.transform;
                Quaternion yaw = Quaternion.Euler(0f, _cartTargetYaw[i], 0f);
                t.SetPositionAndRotation(Vector3.Lerp(t.position, _cartTarget[i], k), Quaternion.Slerp(t.rotation, yaw, k));
            }
        }

        void OnLightning()
        {
            if (IsSpawned)
                LightningRpc();
        }

        void OnLightsOut(float seconds)
        {
            if (IsSpawned)
                LightsOutRpc(seconds);
        }

        void OnMomCall(bool bossDefeated)
        {
            if (IsSpawned)
                MomCallRpc(bossDefeated);
        }

        void OnLampOut(LightZone lamp, float seconds)
        {
            if (IsSpawned && lamp != null)
                LampOutRpc(lamp.Center, seconds);
        }

        void OnPortfolioSpawned(PortfolioPickup portfolio)
        {
            if (!IsSpawned || portfolio.IsPuppet)
                return;
            ushort id = NextPickupId();
            _portfolioIds[portfolio] = id;
            PortfolioSpawnRpc(id, portfolio.transform.position, portfolio.PopVelocity);
        }

        void OnPortfolioTaken(PortfolioPickup portfolio)
        {
            if (IsSpawned && _portfolioIds.TryGetValue(portfolio, out ushort id))
            {
                _portfolioIds.Remove(portfolio);
                PortfolioGoneRpc(id);
            }
        }

        ushort NextPickupId()
        {
            do
                _nextPickupId++;
            while (_nextPickupId == 0);
            return _nextPickupId;
        }

        void OnCoinSpawned(CoinPickup coin)
        {
            if (!IsSpawned)
                return;
            int index = CoinIndex(coin.Value);
            if (index < 0)
                return;
            ushort id = NextPickupId();
            _coinIds[coin] = id;
            CoinSpawnRpc(id, (byte)index, coin.transform.position, coin.PopVelocity);
        }

        void OnCoinFlying(CoinPickup coin, PlayerController player)
        {
            if (IsSpawned && player != null && _coinIds.TryGetValue(coin, out ushort id))
                CoinFlyRpc(id, (byte)player.Slot);
        }

        void OnCoinCollected(CoinPickup coin)
        {
            if (!IsSpawned)
                return;
            ushort id = _coinIds.TryGetValue(coin, out ushort known) ? known : (ushort)0;
            _coinIds.Remove(coin);
            CoinCollectedRpc(id, (ushort)coin.Value);
        }

        int CoinIndex(int value)
        {
            for (int i = 0; i < coins.Length; i++)
                if (coins[i] != null && coins[i].Value == value)
                    return i;
            return -1;
        }

        void OnHealSpawned(HealPickup heal)
        {
            if (!IsSpawned || heal.IsPuppet)
                return;
            int index = HealIndex(heal);
            if (index < 0)
                return;
            ushort id = NextPickupId();
            _healIds[heal] = id;
            HealSpawnRpc(id, (byte)index, heal.transform.position);
        }

        void OnHealTaken(HealPickup heal)
        {
            if (IsSpawned && _healIds.TryGetValue(heal, out ushort id))
            {
                _healIds.Remove(heal);
                HealGoneRpc(id);
            }
        }

        int HealIndex(HealPickup heal)
        {
            var source = heal.TryGetComponent(out PooledObject tag) ? tag.Prefab : null;
            for (int i = 0; i < heals.Length; i++)
                if (heals[i] != null && heals[i].gameObject == source)
                    return i;
            return -1;
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void SoundsRpc(NetSoundBatch batch)
        {
            if (batch.Cues == null)
                return;
            for (int i = 0; i < batch.Cues.Length; i++)
            {
                var cue = (SoundCue)batch.Cues[i];
                GameEvents.PlaySoundFromNetwork(cue, batch.Positions[i]);
                if (cue == SoundCue.Explosion && Players.Local != null
                    && (Players.Local.transform.position - batch.Positions[i]).sqrMagnitude < 100f)
                    GameFeel.Shake(0.4f);
            }
        }

        [Rpc(SendTo.NotServer)]
        void AnnounceRpc(FixedString64Bytes title, FixedString64Bytes hint, float seconds) =>
            GameEvents.AnnounceFromNetwork(new Announcement { Title = title.ToString(), Hint = hint.ToString(), Seconds = seconds });

        [Rpc(SendTo.NotServer)]
        void EffectsRpc(NetEffectBatch batch)
        {
            if (batch.Effects == null)
                return;
            foreach (var effect in batch.Effects)
                Play(effect);
        }

        void Play(in NetEffect effect)
        {
            if (effect.Kind == EffectMarkerHide)
            {
                ushort hidden = (ushort)Mathf.RoundToInt(effect.Inherited.x);
                if (_markerPuppets.TryGetValue(hidden, out var shown))
                {
                    _markerPuppets.Remove(hidden);
                    if (shown != null && shown.isActiveAndEnabled && shown.IsPuppet)
                        PoolService.Despawn(shown.gameObject);
                }
                return;
            }
            if (effect.Prefab >= effects.Length || effects[effect.Prefab] == null)
                return;
            var prefab = effects[effect.Prefab];
            switch (effect.Kind)
            {
                case EffectRing:
                    if (prefab.TryGetComponent(out ExpandingRing _))
                        PoolService.Spawn(prefab, effect.Position, effect.Rotation).GetComponent<ExpandingRing>().Play(effect.Value);
                    break;
                case EffectBurst:
                    if (prefab.TryGetComponent(out ParticleBurst _))
                        PoolService.Spawn(prefab, effect.Position, effect.Rotation).GetComponent<ParticleBurst>().Play(effect.Value);
                    break;
                case EffectDebris:
                    if (prefab.TryGetComponent(out Debris _))
                        PoolService.Spawn(prefab, effect.Position, effect.Rotation).GetComponent<Debris>()
                            .Burst(effect.Direction, effect.Value, effect.Inherited);
                    break;
                case EffectGum:
                    if (prefab.TryGetComponent(out GumSpot gum))
                        GumSpot.Drop(gum, effect.Position);
                    break;
                case EffectChalk:
                    if (prefab.TryGetComponent(out ChalkMark chalk))
                        ChalkMark.Drop(chalk, effect.Position, quiet: true);
                    break;
                case EffectSpawn:
                    PoolService.Spawn(prefab, effect.Position, effect.Rotation);
                    break;
                case EffectMine:
                    if (prefab.TryGetComponent(out BatteryMine _))
                    {
                        var mine = PoolService.Spawn(prefab, effect.Position, Quaternion.identity).GetComponent<BatteryMine>();
                        mine.IsPuppet = true;
                        mine.Throw(effect.Position, effect.Direction, effect.Value, effect.Inherited.x, effect.Inherited.y, 0, 0f, null);
                    }
                    break;
                case EffectCarrot:
                    if (prefab.TryGetComponent(out CarrotBoomerang _))
                    {
                        var hare = FindFirstObjectByType<HareBoss>();
                        if (hare == null)
                            break;
                        var owner = hare.transform;
                        var carrot = PoolService.Spawn(prefab, effect.Position, Quaternion.identity).GetComponent<CarrotBoomerang>();
                        carrot.IsPuppet = true;
                        carrot.Throw(owner, effect.Position, effect.Direction, () => owner.position + Vector3.up * 1.1f, effect.Value,
                            effect.Inherited.x, 0, 0f, null);
                    }
                    break;
                case EffectGiantBall:
                    if (prefab.TryGetComponent(out GiantBall _))
                        PoolService.Spawn(prefab, effect.Position, effect.Rotation).GetComponent<GiantBall>().Launch(effect.Direction);
                    break;
                case EffectCloud:
                    if (prefab.TryGetComponent(out SlowCloud _))
                        PoolService.Spawn(prefab, effect.Position, effect.Rotation).GetComponent<SlowCloud>()
                            .Play(effect.Value, effect.Direction.x, effect.Direction.y);
                    break;
                case EffectCircle:
                case EffectLine:
                    if (!prefab.TryGetComponent(out GroundMarker _))
                        break;
                    var marker = PoolService.Spawn(prefab, effect.Position, Quaternion.identity).GetComponent<GroundMarker>();
                    marker.IsPuppet = true;
                    if (effect.Kind == EffectCircle)
                        marker.ShowCircle(effect.Position, effect.Direction.y, effect.Value);
                    else
                        marker.ShowLine(effect.Position, effect.Direction, effect.Value);
                    _markerPuppets[(ushort)Mathf.RoundToInt(effect.Inherited.x)] = marker;
                    break;
            }
        }

        [Rpc(SendTo.NotServer)]
        void MarkersRpc(bool elite, Vector3[] positions)
        {
            if (_spawner == null)
                _spawner = FindFirstObjectByType<WaveSpawner>();
            if (_spawner == null || positions == null)
                return;
            _markerPositions.Clear();
            _markerPositions.AddRange(positions);
            _spawner.ShowMarkers(elite, _markerPositions);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable, AllowTargetOverride = true)]
        void ArenaRpc(float waveTime, float enemiesFrozen, byte weather, int weatherSeed, RpcParams rpc = default)
        {
            var director = ArenaDirector.Instance;
            if (director != null)
            {
                director.SyncWaveTime(waveTime);
                director.ApplyWeatherFromNetwork((WeatherKind)weather, weatherSeed);
            }
            if (enemiesFrozen > 0.1f && Targetable.EnemiesFrozenLeft < enemiesFrozen - 0.3f)
                Targetable.FreezeEnemiesLocal(enemiesFrozen);
        }

        [Rpc(SendTo.NotServer)]
        void CompletedRpc(bool boss, bool last)
        {
            var director = ArenaDirector.Instance;
            if (director != null)
                director.CompleteFromNetwork(boss, last);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void CartsRpc(byte[] indices, Vector3[] positions, float[] yaws)
        {
            FindCarts();
            if (indices == null || positions == null || yaws == null)
                return;
            for (int i = 0; i < indices.Length && i < positions.Length && i < yaws.Length; i++)
            {
                int index = indices[i];
                if (index >= _carts.Count)
                    continue;
                _cartTarget[index] = positions[i];
                _cartTargetYaw[index] = yaws[i];
            }
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void BreakRpc(Vector3 position, Vector3 direction, float force, RpcParams rpc = default)
        {
            IBreakable best = null;
            float bestSqr = 1f;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is not IBreakable breakable || breakable.IsBroken)
                    continue;
                float sqr = (behaviour.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = breakable;
                }
            }
            best?.Break(direction, force);
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void LightsOutRpc(float seconds, RpcParams rpc = default) => LightsOut.Trigger(seconds);

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void MomCallRpc(bool bossDefeated, RpcParams rpc = default)
        {
            if (HomeCall.Instance != null)
                HomeCall.Instance.BeginCall(bossDefeated);
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void LampOutRpc(Vector3 center, float seconds, RpcParams rpc = default)
        {
            var lamp = LightZone.FindAt(center);
            if (lamp != null)
                lamp.PutOut(seconds);
        }

        [Rpc(SendTo.NotServer)]
        void FreezeAllRpc(float seconds)
        {
            if (Targetable.EnemiesFrozenLeft < seconds - 0.2f)
                Targetable.FreezeEnemiesLocal(seconds);
        }

        [Rpc(SendTo.NotServer)]
        void LightningRpc()
        {
            if (_weather == null)
                _weather = FindFirstObjectByType<WeatherController>();
            if (_weather != null)
                _weather.StrikeFromNetwork();
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void PortfolioSpawnRpc(ushort id, Vector3 position, Vector3 popVelocity, RpcParams rpc = default)
        {
            if (_loot == null)
                _loot = FindFirstObjectByType<LootDropper>();
            if (_loot == null || _loot.PortfolioPrefab == null)
                return;
            var portfolio = PoolService.Spawn(_loot.PortfolioPrefab, position, Quaternion.identity);
            portfolio.BeginPuppet(popVelocity);
            _portfolioPuppets[id] = portfolio;
        }

        [Rpc(SendTo.NotServer)]
        void PortfolioGoneRpc(ushort id)
        {
            if (!_portfolioPuppets.TryGetValue(id, out var portfolio))
                return;
            _portfolioPuppets.Remove(id);
            if (portfolio != null && portfolio.isActiveAndEnabled && portfolio.IsPuppet)
                PoolService.Despawn(portfolio.gameObject);
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void CoinSpawnRpc(ushort id, byte prefab, Vector3 position, Vector3 popVelocity, RpcParams rpc = default)
        {
            if (prefab >= coins.Length || coins[prefab] == null)
                return;
            var coin = PoolService.Spawn(coins[prefab], position, Quaternion.identity);
            coin.BeginPuppet(popVelocity);
            _coinPuppets[id] = coin;
        }

        [Rpc(SendTo.NotServer)]
        void CoinFlyRpc(ushort id, byte slot)
        {
            var player = Players.InSlot(slot);
            if (player != null && _coinPuppets.TryGetValue(id, out var coin) && coin != null && coin.isActiveAndEnabled)
                coin.FlyTo(player);
        }

        [Rpc(SendTo.NotServer)]
        void CoinCollectedRpc(ushort id, ushort value)
        {
            RunState.AddLoot(value);
            if (id != 0 && _coinPuppets.TryGetValue(id, out var coin))
            {
                _coinPuppets.Remove(id);
                if (coin != null && coin.isActiveAndEnabled && coin.IsPuppet)
                    PoolService.Despawn(coin.gameObject);
            }
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void HealSpawnRpc(ushort id, byte prefab, Vector3 position, RpcParams rpc = default)
        {
            if (prefab >= heals.Length || heals[prefab] == null)
                return;
            var heal = PoolService.Spawn(heals[prefab], position, Quaternion.identity);
            heal.IsPuppet = true;
            _healPuppets[id] = heal;
        }

        [Rpc(SendTo.NotServer)]
        void HealGoneRpc(ushort id)
        {
            if (_healPuppets.TryGetValue(id, out var heal))
            {
                _healPuppets.Remove(id);
                if (heal != null && heal.isActiveAndEnabled)
                    PoolService.Despawn(heal.gameObject);
            }
        }
    }

    public struct NetSoundBatch : INetworkSerializable
    {
        public ushort[] Cues;
        public Vector3[] Positions;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Cues);
            serializer.SerializeValue(ref Positions);
        }
    }

    public struct NetEffect : INetworkSerializable
    {
        public byte Kind;
        public ushort Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Value;
        public Vector3 Direction;
        public Vector3 Inherited;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Prefab);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Value);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Inherited);
        }
    }

    public struct NetEffectBatch : INetworkSerializable
    {
        public List<NetEffect> Effects;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count = Effects?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                Effects = new List<NetEffect>(count);
            for (int i = 0; i < count; i++)
            {
                var effect = serializer.IsReader ? default : Effects[i];
                effect.NetworkSerialize(serializer);
                if (serializer.IsReader)
                    Effects.Add(effect);
            }
        }
    }
}
