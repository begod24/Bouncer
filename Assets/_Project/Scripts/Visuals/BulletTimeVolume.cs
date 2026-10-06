using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
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
