using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Audio
{
    /// <summary>
    /// Играет звуки на события игры (<see cref="GameEvents.SoundRequested"/>): свой пул источников,
    /// случайная высота, ограничение частоты одного и того же звука. Звуки в мире наполовину
    /// позиционные — камера далеко, чистое 3D слишком глушило бы их.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        [SerializeField] SoundBank bank;
        [SerializeField, Min(1)] int voices = 24;
        [Tooltip("0 — все звуки по центру, 1 — полностью 3D")]
        [SerializeField, Range(0f, 1f)] float spatialBlend = 0.45f;
        [SerializeField] float minDistance = 12f;
        [SerializeField] float maxDistance = 60f;

        readonly Dictionary<SoundCue, SoundBank.Entry> _entries = new();
        readonly Dictionary<SoundCue, float> _lastPlayed = new();
        AudioSource[] _sources;
        int _next;

        void Awake()
        {
            _sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var source = new GameObject("Voice " + i).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
            if (bank)
                foreach (var entry in bank.entries)
                    _entries[entry.cue] = entry;
        }

        void OnEnable() => GameEvents.SoundRequested += Play;

        void OnDisable() => GameEvents.SoundRequested -= Play;

        void Play(SoundCue cue, Vector3 position)
        {
            if (!_entries.TryGetValue(cue, out var entry) || entry.clips == null || entry.clips.Length == 0)
                return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < entry.minInterval)
                return;
            _lastPlayed[cue] = now;

            var clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null)
                return;
            var source = NextSource();
            source.transform.position = position;
            source.spatialBlend = entry.ui ? 0f : spatialBlend;
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.volume = entry.volume * bank.masterVolume * GameSettings.SfxGain;
            source.clip = clip;
            source.Play();
        }

        /// <summary>Свободный источник, а если все заняты — тот, что звучит дольше всех.</summary>
        AudioSource NextSource()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = _sources[(_next + i) % _sources.Length];
                if (!source.isPlaying)
                {
                    _next = (_next + i + 1) % _sources.Length;
                    return source;
                }
            }
            var oldest = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            return oldest;
        }
    }
}
