using System;
using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Audio
{
    /// <summary>Какие клипы звучат на каждое событие игры (<see cref="SoundCue"/>) и как громко.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundCue cue;
            [Tooltip("Несколько клипов — каждый раз случайный")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("Случайный разброс высоты: 0.08 = ±8%")]
            [Range(0f, 0.5f)] public float pitchJitter = 0.08f;
            [Tooltip("Звук интерфейса: без положения в мире")]
            public bool ui;
            [Tooltip("Не чаще, чем раз в столько секунд: толпа пупсов не должна оглушать")]
            [Min(0f)] public float minInterval = 0.04f;
        }

        [Range(0f, 1f)] public float masterVolume = 0.9f;
        public List<Entry> entries = new();
    }
}
