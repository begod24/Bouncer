using Bouncer.Balls;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    // Шкалы на карточке мяча: урон, скорость, рикошеты, площадь, контроль — по 5 точек.
    public sealed class BallStatBars : MonoBehaviour
    {
        public const int Rows = 5;
        public const int Pips = 5;

        static readonly string[] Keys = { "card.stat.damage", "card.stat.speed", "card.stat.ricochets", "card.stat.area", "card.stat.control" };

        [SerializeField] TMP_Text[] labels = new TMP_Text[Rows];
        [Tooltip("Точки по строкам: первые 5 — урон, следующие 5 — скорость и т. д.")]
        [SerializeField] UnityEngine.UI.Image[] pips = new UnityEngine.UI.Image[Rows * Pips];
        [SerializeField] Color filledColor = new(0.16f, 0.15f, 0.2f, 0.95f);
        [SerializeField] Color emptyColor = new(0.16f, 0.15f, 0.2f, 0.16f);

        public void Show(in BallRatings ratings)
        {
            gameObject.SetActive(true);
            int[] values = { ratings.damage, ratings.speed, ratings.ricochets, ratings.area, ratings.control };
            for (int row = 0; row < Rows; row++)
            {
                if (row < labels.Length && labels[row])
                    labels[row].text = Loc.Get(Keys[row]);
                for (int i = 0; i < Pips; i++)
                {
                    int index = row * Pips + i;
                    if (index < pips.Length && pips[index])
                        pips[index].color = i < values[row] ? filledColor : emptyColor;
                }
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
