using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    public interface ILightBeam
    {
        bool BeamOn { get; }
        Vector3 BeamOrigin { get; }
        Vector3 BeamDirection { get; }
        float BeamRange { get; }
        float BeamHalfAngle { get; }
    }

    public static class LightBeams
    {
        static readonly List<ILightBeam> s_beams = new();

        public static void Register(ILightBeam beam)
        {
            if (beam != null && !s_beams.Contains(beam))
                s_beams.Add(beam);
        }

        public static void Unregister(ILightBeam beam) => s_beams.Remove(beam);

        public static bool Contains(Vector3 point)
        {
            for (int i = s_beams.Count - 1; i >= 0; i--)
            {
                var beam = s_beams[i];
                if (beam == null || !beam.BeamOn)
                    continue;
                Vector3 to = point - beam.BeamOrigin;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > beam.BeamRange)
                    continue;
                if (distance < 0.6f || Vector3.Angle(beam.BeamDirection, to) <= beam.BeamHalfAngle)
                    return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_beams.Clear();
    }
}
