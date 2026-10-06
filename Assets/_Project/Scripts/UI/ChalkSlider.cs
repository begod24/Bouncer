using Bouncer.Core;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Slider))]
    public sealed class ChalkSlider : MonoBehaviour
    {
        [SerializeField] TMP_Text valueLabel;

        UnityEngine.UI.Slider _slider;
        int _shownPercent = -1;
        int _step;

        void Awake()
        {
            _slider = GetComponent<UnityEngine.UI.Slider>();
            _slider.onValueChanged.AddListener(OnValueChanged);
        }

        void Update()
        {
            _step = Step;
            int percent = Mathf.RoundToInt(_slider.normalizedValue * 100f);
            if (percent == _shownPercent)
                return;
            _shownPercent = percent;
            if (valueLabel)
                valueLabel.text = percent + "%";
        }

        int Step => Mathf.RoundToInt(_slider.normalizedValue * 10f);

        void OnValueChanged(float value)
        {
            int step = Step;
            if (step == _step)
                return;
            _step = step;
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
        }
    }
}
