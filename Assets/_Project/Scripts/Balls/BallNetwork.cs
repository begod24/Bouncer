using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>
    /// Мячи по сети (её даёт Bouncer.Net, см. <see cref="Ball.Network"/>). Мячи считает только хозяин комнаты: у него
    /// настоящие мячи, а у гостей — копии (<see cref="Ball.IsPuppet"/>), которые двигаются по присланному хозяином.
    /// Что гость делает с копией — бросает, подбирает, ловит, роняет, тянет хватом, — уходит хозяину, а гость сразу
    /// видит итог у себя (если хозяин не согласится, мяч вернётся на место).
    /// </summary>
    public interface IBallNetwork
    {
        /// <summary>Мячи считает этот компьютер: хозяин комнаты. У гостя — false.</summary>
        bool IsAuthority { get; }

        /// <summary>Гость бросил мяч: бросок уходит хозяину, а пока виден предсказанный мяч-копия.</summary>
        Ball Throw(Ball prefab, in BallThrow t);

        /// <summary>Гость взял копию в руки (подобрал или поймал): копия пропадает, хозяин отдаёт настоящий мяч.</summary>
        void Take(Ball puppet);

        /// <summary>Гость уронил копию (выбило из рук, руки заняты): мяч ложится у его ног.</summary>
        void Drop(Ball puppet, Vector3 position, Vector3 velocity);

        /// <summary>Гость тянет лежащую копию хватом. false — нельзя.</summary>
        bool Summon(Ball puppet, GameObject taker);

        /// <summary>
        /// У хозяина: мяч вернулся в руки игроку другого компьютера (бумеранг, резинка, хват). true — мяч ушёл ему
        /// (настоящий забран), false — у него руки заняты.
        /// </summary>
        bool GiveToRemote(Ball ball, GameObject player);
    }
}
