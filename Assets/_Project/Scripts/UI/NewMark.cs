using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>Меловое «новое!» у кнопки или вкладки: чуть покачивается, чтобы его заметили.</summary>
    public sealed class NewMark : MonoBehaviour
    {
        [SerializeField] float speed = 4f;
        [SerializeField, Range(0f, 0.3f)] float amount = 0.08f;

        Vector3 _scale;

        void Awake() => _scale = transform.localScale;

        void Update() => transform.localScale = _scale * (1f + amount * Mathf.Sin(Time.unscaledTime * speed));
    }
}
