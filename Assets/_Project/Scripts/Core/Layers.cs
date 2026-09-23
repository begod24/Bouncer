namespace Bouncer.Core
{
    /// <summary>Слои из ProjectSettings/TagManager (заведены на этапе 0).</summary>
    public static class Layers
    {
        public const int Player = 6;
        public const int Ball = 7;
        public const int Enemy = 8;
        public const int Environment = 9;
        public const int Ragdoll = 10;

        public const int PlayerMask = 1 << Player;
        public const int BallMask = 1 << Ball;
        public const int EnemyMask = 1 << Enemy;
        public const int EnvironmentMask = 1 << Environment;
        public const int RagdollMask = 1 << Ragdoll;

        /// <summary>Слой персонажей команды (в них летят чужие мячи).</summary>
        public static int CharacterMask(Team team) => team switch
        {
            Team.Player => PlayerMask,
            Team.Enemy => EnemyMask,
            _ => 0,
        };

        /// <summary>С чем сталкивается «живой» мяч команды: окружение + персонажи противников.</summary>
        public static int LiveBallMask(Team thrower) => thrower switch
        {
            Team.Player => EnvironmentMask | EnemyMask,
            Team.Enemy => EnvironmentMask | PlayerMask,
            _ => EnvironmentMask | PlayerMask | EnemyMask,
        };
    }
}
