using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    // Элиту видно издалека: пульсирующий цветной контур (PaletteLit через HitFlash) и искры у ног.
    // Цвет — по свойству элиты (EliteAffix), как у подписи над ней (UI/AffixLabels); без свойства — золотой.
    // Висит на префабах Elite_* (ставит EnemyFxBuilder), так что у гостя работает так же.
    [DisallowMultipleComponent]
    public sealed class EliteGlow : MonoBehaviour
    {
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Искры у ног, цвет им ставим сами")]
        [SerializeField] ParticleSystem sparks;
        [SerializeField, Range(0f, 2f)] float strength = 1f;
        [SerializeField] Color plainColor = new(1f, 0.8f, 0.3f);
        [SerializeField] Color swiftColor = new(0.45f, 0.9f, 1f);
        [SerializeField] Color commanderColor = new(1f, 0.45f, 0.35f);
        [SerializeField] Color catcherColor = new(0.55f, 0.95f, 0.45f);

        EliteAffix _affix;
        AffixKind _shown;
        bool _applied;

        void OnEnable() => _applied = false;

        void LateUpdate()
        {
            // свойство раздают после появления (EliteAffix.Assign), компонент может добавиться позже нас
            if (!_affix)
                TryGetComponent(out _affix);
            var kind = _affix ? _affix.Kind : AffixKind.None;
            if (_applied && kind == _shown)
                return;
            _applied = true;
            _shown = kind;
            var color = kind switch
            {
                AffixKind.Swift => swiftColor,
                AffixKind.Commander => commanderColor,
                AffixKind.Catcher => catcherColor,
                _ => plainColor,
            };
            if (hitFlash)
                hitFlash.SetGlow(color, strength);
            if (sparks)
            {
                var main = sparks.main;
                main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            }
        }
    }
}
