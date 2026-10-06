using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    public sealed class BossArmor
    {
        public const float ClosedMultiplier = 0.5f;
        public const float OpenMultiplier = 1.5f;

        static readonly Color OpenFlash = new(1f, 0.85f, 0.3f);
        static readonly Color ClosedFlash = new(0.65f, 0.65f, 0.7f);

        float _bank;

        public int Take(int damage, bool open)
        {
            if (damage <= 0)
                return 0;
            _bank += damage * (open ? OpenMultiplier : ClosedMultiplier);
            int whole = Mathf.FloorToInt(_bank + 0.001f);
            _bank -= whole;
            return whole;
        }

        public void Reset() => _bank = 0f;

        public static void Flash(HitFlash flash, bool open)
        {
            if (flash)
                flash.Flash(open ? OpenFlash : ClosedFlash, open ? 0.16f : 0.1f);
        }
    }
}
