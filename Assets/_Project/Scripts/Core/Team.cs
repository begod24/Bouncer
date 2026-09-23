namespace Bouncer.Core
{
    public enum Team
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2,
    }

    public static class TeamExtensions
    {
        public static bool IsHostileTo(this Team a, Team b) =>
            a != Team.Neutral && b != Team.Neutral && a != b;
    }
}
