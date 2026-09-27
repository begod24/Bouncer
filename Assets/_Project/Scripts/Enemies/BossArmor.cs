using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Босс держит удар: вне своих «окон» (открылся после атаки) попадание считается за половину, в окне — за полтора.
    /// Дроби копятся, поэтому вне окна засчитывается каждое второе попадание, в окне — три за два.
    /// Бой идёт по ритму босса, а не по урону сборки.
    /// </summary>
    public sealed class BossArmor
    {
        public const float ClosedMultiplier = 0.5f;
        public const float OpenMultiplier = 1.5f;

        static readonly Color OpenFlash = new(1f, 0.85f, 0.3f);
        static readonly Color ClosedFlash = new(0.65f, 0.65f, 0.7f);

        float _bank;

        /// <summary>Сколько урона засчитать из этого попадания.</summary>
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

        /// <summary>Вспышка попадания: золотая в окне, серая, если удар «в броню».</summary>
        public static void Flash(HitFlash flash, bool open)
        {
            if (flash)
                flash.Flash(open ? OpenFlash : ClosedFlash, open ? 0.16f : 0.1f);
        }
    }
}
