using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    // 19 байт вместо 39: время в миллисекундах, позиция с шагом 1 см (±327 м — арены в пределах ±35 м),
    // поворот 1/65536 круга, скорость только по земле с шагом 1 см/с.
    public struct MotionSample : INetworkSerializable
    {
        const float PositionScale = 100f;
        const float VelocityScale = 100f;
        const float YawScale = 65536f / 360f;

        public double Time;
        public Vector3 Position;
        public float Yaw;
        public Vector3 Velocity;
        public NetPose Pose;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            uint time = 0;
            short x = 0, y = 0, z = 0, vx = 0, vz = 0;
            ushort yaw = 0;
            if (serializer.IsWriter)
            {
                time = (uint)(long)System.Math.Round(Time * 1000.0);
                x = Pack(Position.x, PositionScale);
                y = Pack(Position.y, PositionScale);
                z = Pack(Position.z, PositionScale);
                yaw = (ushort)Mathf.RoundToInt(Mathf.Repeat(Yaw, 360f) * YawScale);
                vx = Pack(Velocity.x, VelocityScale);
                vz = Pack(Velocity.z, VelocityScale);
            }
            serializer.SerializeValue(ref time);
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
            serializer.SerializeValue(ref yaw);
            serializer.SerializeValue(ref vx);
            serializer.SerializeValue(ref vz);
            Pose.NetworkSerialize(serializer);
            if (serializer.IsReader)
            {
                Time = time / 1000.0;
                Position = new Vector3(x / PositionScale, y / PositionScale, z / PositionScale);
                Yaw = yaw / YawScale;
                Velocity = new Vector3(vx / VelocityScale, 0f, vz / VelocityScale);
            }
        }

        static short Pack(float value, float scale) => (short)Mathf.Clamp(Mathf.RoundToInt(value * scale), short.MinValue, short.MaxValue);
    }

    public sealed class MotionBuffer
    {
        const int Capacity = 48;
        const int PayloadSize = 32;
        const float MinDelay = 0.035f;
        const float MaxDelay = 0.35f;
        const float MaxExtrapolation = 0.2f;
        const float BlendTime = 0.12f;
        const float SnapDistance = 4f;
        const float CurveGap = 0.15f;
        const float MaxRateChange = 0.08f;
        const float ResyncError = 0.3f;
        const float FloorRise = 0.03f;

        struct Point
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public byte[] Payload;
            public int PayloadLength;
        }

        readonly List<Point> _samples = new(Capacity);
        readonly Stack<byte[]> _freePayloads = new();
        double _floor;
        double _lastArrival;
        bool _synced;
        float _jitter = 0.01f;
        float _interval = 1f / 60f;
        double _renderTime;
        bool _rendering;
        Vector3 _correction;
        Quaternion _rotationCorrection = Quaternion.identity;
        float _extrapolated;
        float _shown;

        public bool HasData => _samples.Count > 0;
        public float Delay => TargetDelay;
        public float Jitter => _jitter;
        public float StarvedShare => _shown > 0f ? _extrapolated / _shown : 0f;
        public int Snaps { get; private set; }

        float TargetDelay => Mathf.Clamp(_interval + 1.5f * _jitter + 0.015f, MinDelay, MaxDelay);

        public void Clear()
        {
            foreach (var sample in _samples)
                Release(sample);
            _samples.Clear();
            _correction = Vector3.zero;
            _rotationCorrection = Quaternion.identity;
            _synced = false;
            _rendering = false;
        }

        public void Add(double time, Vector3 position, Quaternion rotation, Vector3 velocity, double localNow,
            byte[] payload = null, int payloadLength = 0)
        {
            int count = _samples.Count;
            if (count > 0 && time <= _samples[0].Time)
                return;

            double offset = localNow - time;
            if (!_synced)
            {
                _synced = true;
                _floor = offset;
            }
            else
            {
                double rise = (localNow - _lastArrival) * FloorRise;
                _floor = System.Math.Min(offset, _floor + rise);
                float extra = (float)(offset - _floor);
                _jitter = Mathf.Lerp(_jitter, extra, extra > _jitter ? 0.25f : 0.02f);
            }
            _lastArrival = localNow;

            bool had = count > 0 && _rendering;
            Vector3 before = default;
            Quaternion rotationBefore = Quaternion.identity;
            if (had)
                Evaluate(_renderTime, out before, out rotationBefore, out _);

            int index = count;
            while (index > 0 && _samples[index - 1].Time > time)
                index--;
            if (index > 0 && _samples[index - 1].Time == time)
                return;
            if (index == count && count > 0)
            {
                float gap = (float)(time - _samples[count - 1].Time);
                if (gap < CurveGap)
                    _interval = Mathf.Lerp(_interval, gap, 0.1f);
            }

            var sample = new Point { Time = time, Position = position, Rotation = rotation, Velocity = velocity };
            if (payload != null && payloadLength > 0)
            {
                sample.Payload = _freePayloads.Count > 0 ? _freePayloads.Pop() : new byte[PayloadSize];
                sample.PayloadLength = Mathf.Min(payloadLength, PayloadSize);
                System.Array.Copy(payload, sample.Payload, sample.PayloadLength);
            }
            _samples.Insert(index, sample);
            while (_samples.Count > Capacity)
            {
                Release(_samples[0]);
                _samples.RemoveAt(0);
            }

            if (!had)
                return;
            Evaluate(_renderTime, out Vector3 after, out Quaternion rotationAfter, out _);
            _correction += before - after;
            _rotationCorrection = _rotationCorrection * rotationBefore * Quaternion.Inverse(rotationAfter);
            if (_correction.sqrMagnitude > SnapDistance * SnapDistance)
            {
                _correction = Vector3.zero;
                _rotationCorrection = Quaternion.identity;
                Snaps++;
            }
        }

        public bool Sample(double localNow, float dt, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
            => Sample(localNow, dt, out position, out rotation, out velocity, out _, out _, out _);

        public bool Sample(double localNow, float dt, out Vector3 position, out Quaternion rotation, out Vector3 velocity,
            out byte[] payload, out int payloadLength, out float payloadAge)
        {
            position = default;
            rotation = Quaternion.identity;
            velocity = default;
            payload = null;
            payloadLength = 0;
            payloadAge = 0f;
            if (_samples.Count == 0)
                return false;

            double desired = localNow - _floor - TargetDelay;
            if (!_rendering || System.Math.Abs(desired - _renderTime) > ResyncError)
            {
                _renderTime = desired;
                _rendering = true;
            }
            else
            {
                float error = (float)(desired - _renderTime);
                float rate = 1f + Mathf.Clamp(error * 2f, -MaxRateChange, MaxRateChange);
                _renderTime += dt * rate;
            }
            Evaluate(_renderTime, out position, out rotation, out velocity);

            _shown += dt;
            if (_renderTime > _samples[_samples.Count - 1].Time + 0.01)
                _extrapolated += dt;
            if (_shown > 5f)
            {
                _shown *= 0.5f;
                _extrapolated *= 0.5f;
            }

            float fade = Mathf.Exp(-dt / BlendTime);
            _correction *= fade;
            _rotationCorrection = Quaternion.Slerp(Quaternion.identity, _rotationCorrection, fade);
            position += _correction;
            rotation = _rotationCorrection * rotation;

            int current = 0;
            while (current < _samples.Count - 1 && _samples[current + 1].Time <= _renderTime)
                current++;
            var state = _samples[current];
            payload = state.Payload;
            payloadLength = state.PayloadLength;
            payloadAge = Mathf.Max(0f, (float)(_renderTime - state.Time));

            while (_samples.Count > 2 && _samples[1].Time < _renderTime)
            {
                Release(_samples[0]);
                _samples.RemoveAt(0);
            }
            return true;
        }

        void Release(in Point sample)
        {
            if (sample.Payload != null)
                _freePayloads.Push(sample.Payload);
        }

        void Evaluate(double time, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            int count = _samples.Count;
            var first = _samples[0];
            if (count == 1 || time <= first.Time)
            {
                rotation = first.Rotation;
                if (count == 1 && time > first.Time)
                {
                    Extrapolate(first, time, out position, out velocity);
                    return;
                }
                position = first.Position;
                velocity = count == 1 ? Vector3.zero : first.Velocity;
                return;
            }
            var last = _samples[count - 1];
            if (time >= last.Time)
            {
                rotation = last.Rotation;
                Extrapolate(last, time, out position, out velocity);
                return;
            }
            int i = 0;
            while (i < count - 2 && _samples[i + 1].Time <= time)
                i++;
            var a = _samples[i];
            var b = _samples[i + 1];
            float h = (float)(b.Time - a.Time);
            float u = h > 1e-5f ? Mathf.Clamp01((float)((time - a.Time) / h)) : 1f;
            rotation = Quaternion.Slerp(a.Rotation, b.Rotation, u);
            velocity = Vector3.Lerp(a.Velocity, b.Velocity, u);
            if (h > CurveGap)
            {
                position = Vector3.Lerp(a.Position, b.Position, u);
                return;
            }
            float u2 = u * u;
            float u3 = u2 * u;
            position = (2f * u3 - 3f * u2 + 1f) * a.Position + (u3 - 2f * u2 + u) * h * a.Velocity
                       + (-2f * u3 + 3f * u2) * b.Position + (u3 - u2) * h * b.Velocity;
        }

        static void Extrapolate(in Point sample, double time, out Vector3 position, out Vector3 velocity)
        {
            float ahead = Mathf.Min((float)(time - sample.Time), MaxExtrapolation);
            float k = ahead / MaxExtrapolation;
            position = sample.Position + sample.Velocity * (ahead * (1f - 0.5f * k));
            velocity = sample.Velocity * (1f - k);
        }
    }
}
