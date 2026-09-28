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
        /// <summary>Невидимая стенка только для мячей (перед веранды детсада): игрок и враги проходят сквозь неё.</summary>
        public const int BallWall = 11;

        public const int PlayerMask = 1 << Player;
        public const int BallMask = 1 << Ball;
        public const int EnemyMask = 1 << Enemy;
        public const int EnvironmentMask = 1 << Environment;
        public const int RagdollMask = 1 << Ragdoll;
        public const int BallWallMask = 1 << BallWall;
        /// <summary>Во что упирается и от чего отскакивает мяч: окружение и стенки только для мячей.</summary>
        public const int BallSolidMask = EnvironmentMask | BallWallMask;

        /// <summary>Слой из <see cref="BallSolidMask"/>: для мяча это стена, а не персонаж.</summary>
        public static bool IsBallSolid(int layer) => layer is Environment or BallWall;

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
            Team.Player => BallSolidMask | EnemyMask,
            Team.Enemy => BallSolidMask | PlayerMask,
            _ => BallSolidMask | PlayerMask | EnemyMask,
        };
    }
}
