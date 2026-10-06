using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    /// <summary>
    /// Где был игрок в момент <see cref="Time"/> (часы его компьютера), куда смотрел, как бежал на самом деле
    /// (скорость контроллера, а не желание стика) и что делал (<see cref="NetPose"/>).
    /// </summary>
    public struct MotionSample : INetworkSerializable
    {
        public double Time;
        public Vector3 Position;
        public float Yaw;
        public Vector3 Velocity;
        public NetPose Pose;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Time);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Velocity);
            Pose.NetworkSerialize(serializer);
        }
    }

    /// <summary>
    /// Плавное движение чужого персонажа по присланным точкам («буфер дрожания»).
    /// Точки подписаны часами того, кто их прислал: эти часы идут ровно, их не двигает синхронизация сети. Здесь
    /// они переводятся в свои по самой быстрой дошедшей точке (<see cref="Add"/>), а персонаж показывается чуть
    /// в прошлом — на промежуток между точками плюс запас на их разброс. Часы показа идут ровно и подстраиваются
    /// мягко, не быстрее ±<see cref="MaxRateChange"/>: персонаж не замедляется и не ускоряется рывками.
    /// Между точками — кривая по скоростям, без углов. Точки не пришли вовремя — персонаж немного бежит по последней
    /// скорости, плавно останавливаясь. Скачок из-за новой точки вливается за <see cref="BlendTime"/>; телепорт —
    /// только настоящий (дальше <see cref="SnapDistance"/>).
    /// К точке можно приложить состояние (поза игрока, анимация врага) — оно меняется вместе с положением.
    /// </summary>
    public sealed class MotionBuffer
    {
        const int Capacity = 48;
        const int PayloadSize = 32;
        /// <summary>Запас на разброс точек: не меньше и не больше, с.</summary>
        const float MinDelay = 0.035f;
        const float MaxDelay = 0.35f;
        /// <summary>Сколько бежать по последней скорости, когда точки опаздывают, с.</summary>
        const float MaxExtrapolation = 0.2f;
        const float BlendTime = 0.12f;
        const float SnapDistance = 4f;
        /// <summary>Длиннее этого промежуток между точками — между ними прямая (персонаж стоял), с.</summary>
        const float CurveGap = 0.15f;
        /// <summary>Часы показа идут быстрее или медленнее не больше чем на столько (0.08 = ±8%).</summary>
        const float MaxRateChange = 0.08f;
        /// <summary>Так далеко часы показа отстали или убежали — переставить сразу, с.</summary>
        const float ResyncError = 0.3f;
        /// <summary>Как быстро может вырасти «самая быстрая дорога» точек, с в секунду (связь стала медленнее).</summary>
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
        /// <summary>Свои часы минус часы отправителя у самой быстрой точки.</summary>
        double _floor;
        double _lastArrival;
        bool _synced;
        /// <summary>Насколько точки обычно опаздывают сверх самой быстрой (быстро растёт, медленно спадает), с.</summary>
        float _jitter = 0.01f;
        float _interval = 1f / 60f;
        /// <summary>Момент показа по часам отправителя.</summary>
        double _renderTime;
        bool _rendering;
        Vector3 _correction;
        Quaternion _rotationCorrection = Quaternion.identity;
        float _extrapolated;
        float _shown;

        public bool HasData => _samples.Count > 0;
        /// <summary>Насколько в прошлом показывается персонаж сверх самой быстрой доставки, с (отладка).</summary>
        public float Delay => TargetDelay;
        /// <summary>Разброс доставки точек, с (отладка).</summary>
        public float Jitter => _jitter;
        /// <summary>Доля времени, когда точек не хватило и персонаж бежал «на угад» (отладка, 0..1).</summary>
        public float StarvedShare => _shown > 0f ? _extrapolated / _shown : 0f;
        /// <summary>Сколько раз пришлось переставить персонажа рывком (отладка).</summary>
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

        /// <summary>
        /// Пришла точка: time — по часам отправителя, localNow — свои часы сейчас.
        /// </summary>
        public void Add(double time, Vector3 position, Quaternion rotation, Vector3 velocity, double localNow,
            byte[] payload = null, int payloadLength = 0)
        {
            int count = _samples.Count;
            if (count > 0 && time <= _samples[0].Time)
                return;

            // Перевод часов: самая быстрая точка задаёт «пол», остальные опаздывают на разброс.
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
            // Новая точка поменяла кривую там, где персонаж сейчас: показанное не прыгает, разница вливается.
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

        /// <summary>Где показать персонажа сейчас (localNow — свои часы). false — точек ещё нет.</summary>
        public bool Sample(double localNow, float dt, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
            => Sample(localNow, dt, out position, out rotation, out velocity, out _, out _, out _);

        /// <summary>
        /// Где показать персонажа сейчас и его состояние на этот момент: payload — из последней точки до момента
        /// показа, age — сколько прошло с неё. false — точек ещё нет.
        /// </summary>
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

            // Часы показа: ровно вперёд, с мягкой подстройкой к нужному отставанию.
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
            // Отладочная доля — за последние секунды, а не за всю игру.
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
            // Кривая Эрмита: проходит через обе точки с их скоростями — бег и рывки без изломов.
            float u2 = u * u;
            float u3 = u2 * u;
            position = (2f * u3 - 3f * u2 + 1f) * a.Position + (u3 - 2f * u2 + u) * h * a.Velocity
                       + (-2f * u3 + 3f * u2) * b.Position + (u3 - u2) * h * b.Velocity;
        }

        /// <summary>Точки кончились: бежать дальше по последней скорости, плавно тормозя, и встать.</summary>
        static void Extrapolate(in Point sample, double time, out Vector3 position, out Vector3 velocity)
        {
            float ahead = Mathf.Min((float)(time - sample.Time), MaxExtrapolation);
            // Скорость спадает до нуля к концу окна: путь = v·(t − t²/2T).
            float k = ahead / MaxExtrapolation;
            position = sample.Position + sample.Velocity * (ahead * (1f - 0.5f * k));
            velocity = sample.Velocity * (1f - k);
        }
    }
}
