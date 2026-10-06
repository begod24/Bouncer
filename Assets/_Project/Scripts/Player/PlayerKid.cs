using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
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
        public Transform Slot => slot;
        public event Action Changed;

        void Awake()
        {
            var kid = roster != null ? roster[GameSettings.Kid] : null;
            if (kid != null)
                Show(kid);
            else if (slot.childCount > 0)
                Bind(slot.GetChild(0));
        }

        public void ShowKid(int index)
        {
            if (roster != null)
                Show(roster[index]);
        }

        public void Show(KidDefinition kid)
        {
            if (kid == null || kid.model == null || kid == Current)
                return;
            Current = kid;
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
