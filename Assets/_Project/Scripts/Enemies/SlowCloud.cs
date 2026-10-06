using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Enemies
{
    public sealed class SlowCloud : MonoBehaviour, IPoolable
    {
        [SerializeField] ParticleSystem puffs;
        [SerializeField] float fadeTime = 0.8f;

        float _radius;
        float _slow;
        float _until;
        bool _fading;

        public static event System.Action<SlowCloud, float, float, float> Played;

        public void OnSpawned() { }

        public void OnDespawned()
        {
            if (puffs)
                puffs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Play(float radius, float life, float slow)
        {
            _radius = radius;
            _slow = slow;
            _until = Time.time + life;
            _fading = false;
            transform.localScale = Vector3.one * (radius / 2.2f);
            if (puffs)
            {
                puffs.Clear();
                puffs.Play();
            }
            if (!NetHooks.IsGuest)
                Played?.Invoke(this, radius, life, slow);
        }

        void Update()
        {
            if (!_fading)
            {
                float radiusSqr = _radius * _radius;
                foreach (var t in Targetable.All)
                {
                    if (t.Team != Team.Player || !t.IsAlive)
                        continue;
                    Vector3 delta = t.Position - transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= radiusSqr && t.TryGetComponent(out PlayerMotor motor))
                        motor.Slow(_slow, 0.25f);
                }
                if (Time.time >= _until)
                {
                    _fading = true;
                    if (puffs)
                        puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                return;
            }
            if (Time.time >= _until + fadeTime)
                PoolService.Despawn(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Played = null;
    }
}
