using TMPro;
using UnityEngine;

namespace Bouncer.Net
{
    /// <summary>Имя над чужим игроком — мелом цвета его номера, всегда лицом к камере.</summary>
    public sealed class NamePlate : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [Tooltip("Цвета игроков по номеру: 1-й, 2-й, 3-й, 4-й")]
        [SerializeField] Color[] slotColors =
        {
            new(0.98f, 0.85f, 0.36f),
            new(0.45f, 0.78f, 0.96f),
            new(0.96f, 0.56f, 0.71f),
            new(0.56f, 0.86f, 0.49f),
        };

        Camera _camera;

        /// <summary>Показать имя (пусто — спрятать) цветом этого номера.</summary>
        public void Show(string playerName, int slot)
        {
            bool visible = !string.IsNullOrEmpty(playerName);
            gameObject.SetActive(visible);
            if (!visible || label == null)
                return;
            label.text = playerName;
            if (slotColors.Length > 0)
                label.color = slotColors[Mathf.Abs(slot) % slotColors.Length];
        }

        void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera != null)
                transform.rotation = _camera.transform.rotation;
        }
    }
}
