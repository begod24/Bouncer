using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Сцена экрана выбора: дети стоят в ряд далеко под двором, своя камера рисует их в RenderTexture
    /// (фон прозрачный — поверх затемнённого двора). Выбранный стоит в своей позе с листа «Дети двора»,
    /// под ним меловой круг; остальные просто ждут и чуть темнее. Реквизит (книга, чипсы, скакалка, мяч)
    /// следует за рукой или лежит на земле под ногой.
    /// </summary>
    public sealed class KidStage : MonoBehaviour
    {
        [SerializeField] KidRoster roster;
        [Tooltip("AC_KidSelect: Idle и позы")]
        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] Camera stageCamera;
        [Tooltip("Меловой круг под выбранным")]
        [SerializeField] Transform ring;
        [SerializeField] float spacing = 1.25f;
        [Tooltip("Крайние чуть развёрнуты к середине, °")]
        [SerializeField] float turnIn = 10f;
        [Tooltip("Невыбранные темнее — подкраска палитры")]
        [SerializeField] Color dimColor = new(0.16f, 0.18f, 0.26f);
        [SerializeField, Range(0f, 1f)] float dimAmount = 0.45f;
        [SerializeField] float crossFade = 0.25f;

        sealed class Slot
        {
            public KidDefinition Kid;
            public Transform Root;
            public Animator Animator;
            public Renderer[] Renderers;
            public Transform Prop;
            public Transform Bone;
        }

        readonly List<Slot> _slots = new();
        MaterialPropertyBlock _block;
        int _selected = -1;

        public RenderTexture Texture { get; private set; }

        /// <summary>Поставить детей и завести картинку нужного размера (пиксели).</summary>
        public void Build(int width, int height)
        {
            _block = new MaterialPropertyBlock();
            Texture = new RenderTexture(Mathf.Max(64, width), Mathf.Max(64, height), 24, RenderTextureFormat.ARGB32)
            {
                name = "KidStage",
                antiAliasing = 1,
            };
            stageCamera.targetTexture = Texture;
            stageCamera.aspect = (float)Texture.width / Texture.height;

            int count = roster.Count;
            for (int i = 0; i < count; i++)
            {
                var kid = roster[i];
                // Камера смотрит на детей спереди (с +Z), так что первый в списке — слева в кадре, на +X.
                float x = ((count - 1) * 0.5f - i) * spacing;
                var root = new GameObject(kid.name).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(x, 0f, 0f);
                root.localRotation = Quaternion.Euler(0f, -Mathf.Sign(x) * (Mathf.Abs(x) > 0.01f ? turnIn : 0f), 0f);
                var model = Instantiate(kid.model, root, false);
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var slot = new Slot { Kid = kid, Root = root, Animator = animator };
                var renderers = new List<Renderer>(model.GetComponentsInChildren<Renderer>(true));
                foreach (var r in renderers)
                    if (r is SkinnedMeshRenderer skinned)
                        skinned.updateWhenOffscreen = true;
                if (kid.prop)
                {
                    slot.Prop = Instantiate(kid.prop, root, false).transform;
                    slot.Bone = animator.isHuman ? animator.GetBoneTransform(kid.propBone) : null;
                    renderers.AddRange(slot.Prop.GetComponentsInChildren<Renderer>(true));
                }
                slot.Renderers = renderers.ToArray();
                _slots.Add(slot);
            }
        }

        /// <summary>Где в кадре стоит ребёнок: 0 — левый край картинки, 1 — правый.</summary>
        public float ViewportX(int index) =>
            index >= 0 && index < _slots.Count ? stageCamera.WorldToViewportPoint(_slots[index].Root.position).x : 0.5f;

        public void Select(int index)
        {
            if (index == _selected || index < 0 || index >= _slots.Count)
                return;
            _selected = index;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool chosen = i == index;
                slot.Animator.CrossFadeInFixedTime(chosen ? slot.Kid.selectPose : "Idle", crossFade);
                Tint(slot, chosen ? 0f : dimAmount);
            }
            if (ring)
                ring.position = _slots[index].Root.position + Vector3.up * 0.03f;
        }

        void Tint(Slot slot, float amount)
        {
            foreach (var r in slot.Renderers)
            {
                if (!r || !PaletteShader.Supports(r.sharedMaterial))
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(PaletteShader.TintColor, PaletteShader.Tint(dimColor, amount));
                r.SetPropertyBlock(_block);
            }
        }

        void LateUpdate()
        {
            foreach (var slot in _slots)
            {
                if (slot.Prop == null || slot.Bone == null)
                    continue;
                var kid = slot.Kid;
                var model = slot.Root;
                var rotation = model.rotation * Quaternion.Euler(kid.propRotation);
                if (kid.PropOnGround)
                {
                    Vector3 foot = model.InverseTransformPoint(slot.Bone.position);
                    var local = new Vector3(foot.x + kid.propPosition.x, kid.propPosition.y, foot.z + kid.propPosition.z);
                    slot.Prop.SetPositionAndRotation(model.TransformPoint(local), rotation);
                }
                else
                {
                    slot.Prop.SetPositionAndRotation(slot.Bone.position + model.rotation * kid.propPosition, rotation);
                }
            }
        }

        void OnDestroy()
        {
            if (Texture == null)
                return;
            stageCamera.targetTexture = null;
            Texture.Release();
            Destroy(Texture);
        }
    }
}
