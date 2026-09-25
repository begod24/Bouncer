using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Модель ребёнка на игроке. В префабе в слоте стоит ребёнок по умолчанию — чтобы игрока было видно в сцене;
    /// в игре его место занимает выбранный (<see cref="GameSettings.Kid"/>), а экран выбора меняет его на ходу.
    /// Мяч висит в правой руке, мигание неуязвимости и вспышки ударов идут по мешу ребёнка.
    /// Аниматором управляет <see cref="KidAnimator"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerKid : MonoBehaviour
    {
        [SerializeField] KidRoster roster;
        [Tooltip("Куда ставится модель ребёнка")]
        [SerializeField] Transform slot;
        [Tooltip("AC_Kid: бег, рывок, замах, ловля, выбывание")]
        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] PlayerVisuals visuals;
        [SerializeField] HitFlash hitFlash;

        [Header("Мяч в руке")]
        [SerializeField] Transform handBall;
        [Tooltip("Где мяч относительно кисти правой руки, в её осях: z — вдоль пальцев, y — тыльная сторона (ладонь — минус)")]
        [SerializeField] Vector3 handBallOffset = new(0f, -0.15f, 0.07f);

        public KidDefinition Current { get; private set; }
        public Animator Animator { get; private set; }
        /// <summary>Слот модели: <see cref="KidAnimator"/> поворачивает его в рывке.</summary>
        public Transform Slot => slot;
        /// <summary>Модель сменилась (новый Animator).</summary>
        public event Action Changed;

        void Awake()
        {
            var kid = roster != null ? roster[GameSettings.Kid] : null;
            if (kid != null)
                Show(kid);
            else if (slot.childCount > 0)
                Bind(slot.GetChild(0));
        }

        /// <summary>Поставить другого ребёнка. Тот же — ничего не делает.</summary>
        public void Show(KidDefinition kid)
        {
            if (kid == null || kid.model == null || kid == Current)
                return;
            Current = kid;
            // Мяч не часть модели: снимаем его со старой руки, пока та уходит.
            if (handBall)
                handBall.SetParent(transform, false);
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var old = slot.GetChild(i).gameObject;
                old.SetActive(false);
                old.transform.SetParent(null, false);
                Destroy(old);
            }
            var model = Instantiate(kid.model, slot, false);
            model.name = kid.model.name;
            Bind(model.transform);
            Changed?.Invoke();
        }

        void Bind(Transform model)
        {
            Animator = model.GetComponent<Animator>();
            if (Animator)
            {
                Animator.runtimeAnimatorController = controller;
                Animator.applyRootMotion = false;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            // Кувырки, подкат и падение выходят за границы позы покоя — пусть меш не пропадает у края кадра.
            foreach (var r in renderers)
                if (r is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            if (visuals)
                visuals.SetBodyRenderers(renderers);
            if (hitFlash)
                hitFlash.SetRenderers(renderers);

            var hand = Animator && Animator.isHuman ? Animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (handBall && hand)
            {
                handBall.SetParent(hand, false);
                handBall.localPosition = handBallOffset;
                handBall.localRotation = Quaternion.identity;
            }
        }
    }
}
