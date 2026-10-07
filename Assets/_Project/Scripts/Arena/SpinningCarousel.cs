using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Arena
{
    public sealed class SpinningCarousel : MonoBehaviour
    {
        [Tooltip("Платформа, которая крутится (Carousel_Deck)")]
        [SerializeField] Transform deck;
        [Tooltip("Градусов в секунду; знак — в какую сторону")]
        [SerializeField] float speed = 40f;
        [Tooltip("Радиус платформы, м")]
        [SerializeField] float radius = 2f;
        [Tooltip("Высота верха платформы над корнем карусели")]
        [SerializeField] float deckHeight = 0.28f;
        [Tooltip("Катает тех, кто не выше этого над платформой (прыжок лягушки не считается)")]
        [SerializeField] float carryHeight = 0.5f;

        float _angle;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            float step = speed * dt;
            _angle = Online.TryHostTime(out double hostTime) ? (float)(hostTime * speed % 360.0) : _angle + step;
            if (deck)
                deck.localRotation = Quaternion.Euler(0f, _angle, 0f);
            if (!GameSession.IsGameplayActive)
                return;

            Vector3 center = transform.position;
            float top = center.y + deckHeight;
            var turn = Quaternion.AngleAxis(step, Vector3.up);

            foreach (var t in Targetable.All)
            {
                if (!t.IsAlive || t.IsFrozen || t.IsRemote || (NetHooks.IsGuest && t.Team == Team.Enemy))
                    continue;
                Vector3 position = t.transform.position;
                if (!OnDeck(position, center, top))
                    continue;
                Vector3 offset = position - center;
                offset.y = 0f;
                Vector3 delta = turn * offset - offset;
                if (t.TryGetComponent(out CharacterController controller) && controller.enabled)
                    controller.Move(delta);
                else if (t.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
                    agent.Move(delta);
                else if (t.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                    body.MovePosition(body.position + delta);
            }

            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose || !OnDeck(ball.Position, center, top))
                    continue;
                if (ball.IsPuppet)
                {
                    ball.CarryPuppet(center, turn);
                    continue;
                }
                Vector3 offset = ball.Position - center;
                offset.y = 0f;
                if (ball.TryGetComponent(out Rigidbody body))
                    body.MovePosition(body.position + (turn * offset - offset));
            }
        }

        bool OnDeck(Vector3 position, Vector3 center, float top)
        {
            Vector3 flat = position - center;
            flat.y = 0f;
            return flat.sqrMagnitude <= radius * radius && position.y >= top - 0.35f && position.y <= top + carryHeight;
        }
    }
}
