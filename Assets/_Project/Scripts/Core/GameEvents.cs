using System;
using UnityEngine;

namespace Bouncer.Core
{
    public static class GameEvents
    {
        public static event Action<GameObject, HitInfo> EnemyKilled;
        public static event Action<GameObject> PlayerDied;
        public static event Action<SoundCue, Vector3> SoundRequested;
        public static event Action BossDefeated;
        public static event Action<Announcement> Announced;
        public static event Action<Announcement> WorldAnnounced;
        public static event Action<SpawnRequest> SpawnRequested;
        public static event Action MomCalled;
        public static event Action<bool> RunFinished;

        public static Func<SoundCue, bool> LocalSoundFilter { get; set; }

        public static bool MuteLocalAnnouncements { get; set; }

        public static void RaiseEnemyKilled(GameObject enemy, in HitInfo hit) => EnemyKilled?.Invoke(enemy, hit);
        public static void RaisePlayerDied(GameObject player) => PlayerDied?.Invoke(player);
        public static void PlaySound(SoundCue cue, Vector3 position)
        {
            if (LocalSoundFilter != null && !LocalSoundFilter(cue))
                return;
            SoundRequested?.Invoke(cue, position);
        }

        public static void PlaySoundFromNetwork(SoundCue cue, Vector3 position) => SoundRequested?.Invoke(cue, position);
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();
        public static void Announce(in Announcement announcement)
        {
            if (MuteLocalAnnouncements)
                return;
            Announced?.Invoke(announcement);
            WorldAnnounced?.Invoke(announcement);
        }

        public static void AnnounceFromNetwork(in Announcement announcement) => Announced?.Invoke(announcement);

        public static void AnnounceLocal(in Announcement announcement) => Announced?.Invoke(announcement);
        public static void RequestSpawn(in SpawnRequest request) => SpawnRequested?.Invoke(request);
        public static void RaiseMomCalled() => MomCalled?.Invoke();
        public static void RaiseRunFinished(bool victory) => RunFinished?.Invoke(victory);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemyKilled = null;
            PlayerDied = null;
            SoundRequested = null;
            BossDefeated = null;
            Announced = null;
            WorldAnnounced = null;
            SpawnRequested = null;
            MomCalled = null;
            LocalSoundFilter = null;
            MuteLocalAnnouncements = false;
        }
    }

    public struct Announcement
    {
        public string Title;
        public string Hint;
        public float Seconds;
        public string Arg;
    }

    public struct SpawnRequest
    {
        public GameObject Prefab;
        public int Count;
        public Vector3 Position;
        public bool Line;
    }
}
