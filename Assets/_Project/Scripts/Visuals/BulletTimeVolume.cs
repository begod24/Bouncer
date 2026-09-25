using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Цветокоррекция на время «Замри!» (<see cref="GameFeel.BulletTime"/>) и свистка Физрука
    /// (<see cref="Targetable.EnemiesFrozen01"/>): мир выцветает и холодеет, пока всё вокруг замедлено или стоит.
    /// Вес Volume идёт за силой замедления, профиль — в ассете.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public sealed class BulletTimeVolume : MonoBehaviour
    {
        Volume _volume;

        void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.weight = 0f;
        }

        void LateUpdate() => _volume.weight = Mathf.Max(GameFeel.BulletTime01, Targetable.EnemiesFrozen01 * 0.8f);
    }
}
