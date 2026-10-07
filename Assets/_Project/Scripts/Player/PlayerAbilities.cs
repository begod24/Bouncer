using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerAbilities : MonoBehaviour
    {
        [SerializeField] AbilityCatalog catalog;

        PlayerController _player;
        AbilityDefinition _shown;
        float _readyAt;
        float _startedAt;

        public static event Action<PlayerController, int, AbilityCast> Casted;

        public AbilityDefinition Current => _player != null ? _player.Modifiers.Ability : null;
        public bool Has => Current != null;
        public bool IsReady => Has && Time.time >= _readyAt;
        public float Ready01
        {
            get
            {
                float total = _readyAt - _startedAt;
                return total <= 0f ? 1f : Mathf.Clamp01(1f - (_readyAt - Time.time) / total);
            }
        }

        void Awake() => _player = GetComponent<PlayerController>();

        public void Tick(in PlayerIntent intent, bool canAct)
        {
            var ability = Current;
            if (ability != _shown)
            {
                _shown = ability;
                _readyAt = _startedAt = 0f;
            }
            if (ability == null || !intent.AbilityPressed)
                return;
            if (!canAct || Time.time < _readyAt)
            {
                GameEvents.PlaySound(SoundCue.AbilityNotReady, transform.position);
                return;
            }
            Vector3 direction = _player.Aim.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                direction = transform.forward;
            var cast = new AbilityCast
            {
                Caster = _player,
                Origin = transform.position,
                Direction = direction.normalized,
                Target = transform.position + direction.normalized * 6f,
                Seed = UnityEngine.Random.Range(1, int.MaxValue),
                IsOwner = true,
                IsAuthority = Online.IsHost,
            };
            if (!ability.Prepare(ref cast))
            {
                GameEvents.PlaySound(SoundCue.AbilityNotReady, transform.position);
                return;
            }
            ability.Cast(cast);
            _startedAt = Time.time;
            _readyAt = Time.time + ability.cooldown;
            Casted?.Invoke(_player, catalog != null ? catalog.IndexOf(ability) : -1, cast);
        }

        public void Replay(int index, AbilityCast cast)
        {
            var ability = catalog != null ? catalog.At(index) : null;
            if (ability == null)
                return;
            cast.Caster = _player;
            cast.IsOwner = false;
            cast.IsAuthority = Online.IsHost;
            ability.Cast(cast);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Casted = null;
    }
}
