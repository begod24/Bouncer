using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Окна панелек в финале — часы до зова мамы: к ночи люди приходят домой, и окна загораются одно за другим
    /// в случайном порядке, пока не загорится последнее — наше, с мамой в окне. Накладки лежат поверх окон
    /// модели: выключенная — окно как есть. Когда босс гасит свет (<see cref="LightsOut"/>), гаснут все окна.
    /// Сколько прошло, задаёт финал арены (<see cref="Progress01"/>).
    /// </summary>
    public sealed class WindowClock : MonoBehaviour
    {
        [Tooltip("Светящиеся накладки на окна (без нашего)")]
        [SerializeField] Renderer[] windows;
        [Tooltip("Наше окно: загорается последним, когда мама зовёт")]
        [SerializeField] Renderer ourWindow;
        [Tooltip("Мама в нашем окне")]
        [SerializeField] GameObject momSilhouette;
        [Tooltip("Какая доля окон уже горит в начале финала")]
        [SerializeField, Range(0f, 1f)] float litAtStart = 0.3f;
        [Tooltip("Порядок, в котором загораются окна")]
        [SerializeField] int seed = 7;

        int[] _order;
        int _shown = -1;
        bool _ourShown;
        bool _ourWanted;

        /// <summary>0 — начало финала, 1 — время зова: горят все окна, кроме нашего.</summary>
        public float Progress01 { get; set; }

        /// <summary>Наше окно горит, в нём мама.</summary>
        public bool OurWindowLit
        {
            get => _ourWanted;
            set => _ourWanted = value;
        }

        public int Count => windows != null ? windows.Length : 0;

        void Awake()
        {
            int count = Count;
            _order = new int[count];
            for (int i = 0; i < count; i++)
                _order[i] = i;
            var random = new System.Random(seed);
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            _shown = -1;
            _ourShown = true;
            Apply(0, false);
        }

        void LateUpdate()
        {
            bool dark = LightsOut.Dark01 > 0.5f;
            int lit = dark ? 0 : Mathf.FloorToInt(Mathf.Lerp(litAtStart, 1f, Mathf.Clamp01(Progress01)) * Count);
            Apply(lit, _ourWanted && !dark);
        }

        void Apply(int lit, bool our)
        {
            if (lit != _shown && windows != null)
            {
                _shown = lit;
                for (int i = 0; i < _order.Length; i++)
                {
                    var window = windows[_order[i]];
                    if (window && window.enabled != i < lit)
                        window.enabled = i < lit;
                }
            }
            if (our == _ourShown)
                return;
            _ourShown = our;
            if (ourWindow)
                ourWindow.enabled = our;
            if (momSilhouette)
                momSilhouette.SetActive(our);
        }
    }
}
