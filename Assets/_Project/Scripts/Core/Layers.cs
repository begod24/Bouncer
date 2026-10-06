namespace Bouncer.Core
{
    public static class Layers
    {
        public const int Player = 6;
        public const int Ball = 7;
        public const int Enemy = 8;
        public const int Environment = 9;
        public const int Ragdoll = 10;
        public const int BallWall = 11;

        public const int PlayerMask = 1 << Player;
        public const int BallMask = 1 << Ball;
        public const int EnemyMask = 1 << Enemy;
        public const int EnvironmentMask = 1 << Environment;
        public const int RagdollMask = 1 << Ragdoll;
        public const int BallWallMask = 1 << BallWall;
        public const int BallSolidMask = EnvironmentMask | BallWallMask;

        public static bool IsBallSolid(int layer) => layer is Environment or BallWall;

        public static int CharacterMask(Team team) => team switch
        {
            Team.Player => PlayerMask,
            Team.Enemy => EnemyMask,
            _ => 0,
        };

        public static int LiveBallMask(Team thrower) => thrower switch
        {
            Team.Player => BallSolidMask | EnemyMask,
            Team.Enemy => BallSolidMask | PlayerMask,
            _ => BallSolidMask | PlayerMask | EnemyMask,
        };
    }
}
