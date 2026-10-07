using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Camera Smena", fileName = "Ability_Camera")]
    public sealed class CameraAbility : AbilityDefinition
    {
        [Header("Вспышка")]
        public float range = 7f;
        [Tooltip("Половина угла кадра, градусы")]
        public float halfAngle = 32f;
        [Tooltip("На сколько секунд враги в кадре замирают")]
        public float freeze = 1.3f;
        [Tooltip("Тени (боятся света) в кадре получают столько урона")]
        public int burnDamage = 1;

        [Header("Эффекты")]
        [SerializeField] AbilityProp cameraProp;
        [SerializeField] CameraFlash flash;
        [SerializeField] PhotoPrint photo;
        [Tooltip("Звёздочки «в глазах» у замерших, из пула")]
        [SerializeField] ParticleBurst dazzle;

        public override void Cast(in AbilityCast cast)
        {
            var caster = cast.Caster;
            Vector3 forward = Flat(cast.Direction).normalized;
            Vector3 lens = cast.Origin + Vector3.up * 1.35f + forward * 0.45f;
            GameEvents.PlaySound(SoundCue.CameraFlash, lens);
            if (caster)
                AbilityProp.Show(cameraProp, caster.transform, new Vector3(0f, 1.35f, 0.45f), Quaternion.identity);
            if (flash)
                PoolService.Spawn(flash, lens, Quaternion.identity).Play(forward, range, halfAngle);
            if (photo)
            {
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                Vector3 land = cast.Origin + side * Random.Range(0.6f, 1.1f) - forward * Random.Range(0.2f, 0.7f);
                PoolService.Spawn(photo, lens + Vector3.up * 0.15f, Quaternion.identity).Play(lens + Vector3.up * 0.15f, land + Vector3.up * 0.03f);
            }

            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var target = all[i];
                if (target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                Vector3 to = Flat(target.Position - cast.Origin);
                float distance = to.magnitude;
                if (distance > range || (distance > 0.8f && Vector3.Angle(forward, to) > halfAngle))
                    continue;
                if (dazzle)
                    PoolService.Spawn(dazzle, target.AimPoint + Vector3.up * 0.5f, Quaternion.identity);
                if (!cast.IsOwner)
                    continue;
                target.Freeze(freeze);
                if (burnDamage > 0 && target.TryGetComponent(out IBurnable burnable) && burnable.BurnsInLight
                    && target.TryGetComponent(out IDamageable damageable))
                    NetHooks.ApplyHit(damageable, new HitInfo
                    {
                        Damage = burnDamage,
                        Point = target.AimPoint,
                        Direction = to.sqrMagnitude > 1e-4f ? to.normalized : forward,
                        Force = 2f,
                        SourceTeam = Team.Player,
                        Source = caster ? caster.gameObject : null,
                        Flags = HitFlags.Burn,
                    });
            }
        }
    }
}
