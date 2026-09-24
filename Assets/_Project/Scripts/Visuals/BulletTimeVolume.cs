using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Цветокоррекция на время «Замри!» (<see cref="GameFeel.BulletTime"/>): мир выцветает и холодеет,
    /// пока всё вокруг замедлено. Вес Volume идёт за силой замедления, профиль — в ассете.
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

        void LateUpdate() => _volume.weight = GameFeel.BulletTime01;
    }
}
