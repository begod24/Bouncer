using System.Collections.Generic;
using Bouncer.Enemies;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    public sealed class AffixLabels : MonoBehaviour
    {
        [Tooltip("Подпись HUD, по которой делаются надписи (шрифт, тень)")]
        [SerializeField] TMP_Text template;
        [Tooltip("Высота надписи над ногами элитки, м")]
        [SerializeField] float height = 2.4f;
        [SerializeField] float fontSize = 30f;
        [SerializeField] Color swiftColor = new(0.45f, 0.9f, 1f);
        [SerializeField] Color commanderColor = new(1f, 0.45f, 0.35f);
        [SerializeField] Color catcherColor = new(0.55f, 0.95f, 0.45f);

        readonly List<TMP_Text> _labels = new();
        RectTransform _canvas;

        void Awake() => _canvas = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;

        void LateUpdate()
        {
            var camera = Camera.main;
            var active = EliteAffix.Active;
            int used = 0;
            if (camera != null && template != null)
            {
                foreach (var affix in active)
                {
                    if (affix == null || affix.Kind == AffixKind.None || affix.Self == null || !affix.Self.IsAlive)
                        continue;
                    Vector3 screen = camera.WorldToScreenPoint(affix.transform.position + Vector3.up * height);
                    if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 local))
                        continue;
                    var label = Label(used++);
                    label.rectTransform.anchoredPosition = local;
                    label.text = Loc.Get(affix.Kind switch
                    {
                        AffixKind.Swift => "affix.swift",
                        AffixKind.Commander => "affix.commander",
                        _ => "affix.catcher",
                    });
                    label.color = affix.Kind switch
                    {
                        AffixKind.Swift => swiftColor,
                        AffixKind.Commander => commanderColor,
                        _ => catcherColor,
                    };
                }
            }
            for (int i = 0; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf != i < used)
                    _labels[i].gameObject.SetActive(i < used);
        }

        TMP_Text Label(int index)
        {
            while (_labels.Count <= index)
            {
                var label = Instantiate(template, transform);
                foreach (var behaviour in label.GetComponents<MonoBehaviour>())
                    if (behaviour != null && behaviour != label && behaviour.GetType().Name == "LocalizeStringEvent")
                        Destroy(behaviour);
                label.name = $"Affix_{_labels.Count + 1}";
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(320f, 50f);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = fontSize;
                label.raycastTarget = false;
                _labels.Add(label);
            }
            return _labels[index];
        }
    }
}
