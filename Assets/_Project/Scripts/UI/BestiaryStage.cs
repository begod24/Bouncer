using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.UI
{
    public sealed class BestiaryStage : MonoBehaviour
    {
        [SerializeField] Camera stageCamera;
        [Tooltip("Вокруг него крутится модель")]
        [SerializeField] Transform pivot;
        [Tooltip("Меловой круг под моделью")]
        [SerializeField] CircleLine ring;
        [SerializeField] float spinSpeed = 28f;
        [Tooltip("С какого угла модель показывается сначала (0 — лицом в камеру)")]
        [SerializeField] float startAngle = -30f;
        [Tooltip("Запас по краям кадра")]
        [SerializeField] float margin = 1.08f;
        [SerializeField] Color silhouetteColor = new(0.05f, 0.06f, 0.09f);

        static readonly List<Component> s_components = new();

        GameObject _model;
        MaterialPropertyBlock _block;
        float _angle;

        public RenderTexture Texture { get; private set; }

        public void Build(int width, int height)
        {
            _block = new MaterialPropertyBlock();
            Texture = new RenderTexture(Mathf.Max(64, width), Mathf.Max(64, height), 24, RenderTextureFormat.ARGB32)
            {
                name = "BestiaryStage",
                antiAliasing = 1,
            };
            stageCamera.targetTexture = Texture;
            stageCamera.aspect = (float)Texture.width / Texture.height;
        }

        public void Show(GameObject prefab, bool silhouette)
        {
            if (_model != null)
                Destroy(_model);
            _model = null;
            _angle = startAngle;
            pivot.localRotation = Quaternion.Euler(0f, _angle, 0f);
            if (prefab == null)
                return;

            var source = prefab.transform.Find("Visual");
            if (source == null)
                source = prefab.transform;
            var holder = new GameObject(prefab.name);
            holder.SetActive(false);
            holder.transform.SetParent(pivot, false);
            var copy = Instantiate(source.gameObject, holder.transform, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            Strip(copy);
            holder.SetActive(true);
            _model = holder;

            var renderers = holder.GetComponentsInChildren<Renderer>();
            if (!Bounds(renderers, out var bounds))
                return;
            Vector3 offset = pivot.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.transform.position += offset;
            bounds.center += offset;
            Frame(bounds);
            if (silhouette)
                Tint(renderers);
        }

        static void Strip(GameObject root)
        {
            StripPass(root, c => c is MonoBehaviour or Joint or ParticleSystem);
            StripPass(root, c => c is Collider or Rigidbody or Light or AudioSource or ParticleSystemRenderer);
        }

        static void StripPass(GameObject root, System.Predicate<Component> match)
        {
            root.GetComponentsInChildren(true, s_components);
            foreach (var component in s_components)
                if (component && match(component))
                    DestroyImmediate(component);
            s_components.Clear();
        }

        static bool Bounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer or LineRenderer or TrailRenderer)
                    continue;
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return any;
        }

        void Frame(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float height = size.y;
            float width = Mathf.Sqrt(size.x * size.x + size.z * size.z);
            float halfV = stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * stageCamera.aspect);
            float distance = Mathf.Max(height * 0.5f / Mathf.Tan(halfV), width * 0.5f / Mathf.Tan(halfH)) * margin + width * 0.3f;
            var cam = stageCamera.transform;
            Vector3 look = bounds.center;
            Vector3 back = transform.forward;
            cam.position = look + back * distance + Vector3.up * (distance * 0.07f);
            cam.rotation = Quaternion.LookRotation(look - cam.position, Vector3.up);
            stageCamera.nearClipPlane = Mathf.Max(0.05f, distance - width * 2f);
            stageCamera.farClipPlane = distance + width * 2f + 2f;
            if (ring)
            {
                ring.transform.position = pivot.position + Vector3.up * 0.03f;
                ring.Radius = Mathf.Max(0.35f, width * 0.42f);
            }
        }

        void Tint(Renderer[] renderers)
        {
            foreach (var r in renderers)
            {
                if (!r || !PaletteShader.Supports(r.sharedMaterial))
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(PaletteShader.FlashColor, PaletteShader.Tint(silhouetteColor, 1f));
                r.SetPropertyBlock(_block);
            }
        }

        void Update()
        {
            _angle += spinSpeed * Time.unscaledDeltaTime;
            pivot.localRotation = Quaternion.Euler(0f, _angle, 0f);
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
