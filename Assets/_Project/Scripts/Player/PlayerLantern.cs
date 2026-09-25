using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Круг света вокруг игрока на тёмной арене (<see cref="DarkArena"/>): видно, куда бежишь, но тень в нём
    /// не твердеет. С «Фонариком» круг шире и ярче — и тогда это уже свет для тени (радиус задаёт
    /// PlayerController через <see cref="Targetable.LightRadius"/>).
    /// </summary>
    public sealed class PlayerLantern : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("Точечный свет над игроком (выключен на светлых аренах)")]
        [SerializeField] Light lamp;
        [Tooltip("Радиус круга без «Фонарика», м")]
        [SerializeField] float baseRange = 6.5f;
        [SerializeField] float baseIntensity = 14f;
        [Tooltip("На сколько ярче свет с «Фонариком»")]
        [SerializeField] float lanternIntensityMultiplier = 1.6f;
        [Tooltip("Насколько дальше радиуса «Фонарика» достаёт сам свет, м: край круга мягкий")]
        [SerializeField] float lanternRangeExtra = 2f;

        void LateUpdate()
        {
            if (!lamp || !player)
                return;
            bool on = DarkArena.Active && !player.IsDead;
            if (lamp.enabled != on)
                lamp.enabled = on;
            if (!on)
                return;
            float lantern = player.Modifiers.LanternRadius;
            lamp.range = lantern > 0f ? Mathf.Max(baseRange, lantern + lanternRangeExtra) : baseRange;
            lamp.intensity = lantern > 0f ? baseIntensity * lanternIntensityMultiplier : baseIntensity;
        }
    }
}
