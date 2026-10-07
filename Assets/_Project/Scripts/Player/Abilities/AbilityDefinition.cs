using UnityEngine;

namespace Bouncer.Player
{
    public struct AbilityCast
    {
        public PlayerController Caster;
        public Vector3 Origin;
        public Vector3 Direction;
        public Vector3 Target;
        public int Param;
        public int Seed;
        public bool IsOwner;
        public bool IsAuthority;
    }

    public abstract class AbilityDefinition : ScriptableObject
    {
        [Tooltip("Перезарядка, с")]
        [Min(0.5f)] public float cooldown = 10f;
        [Tooltip("Белый значок для HUD")]
        public Sprite hudIcon;

        public virtual bool Prepare(ref AbilityCast cast) => true;

        public abstract void Cast(in AbilityCast cast);

        protected static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
