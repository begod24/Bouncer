namespace Bouncer.Core
{
    public enum WeatherKind
    {
        Clear,
        Rain,
        Storm,
        Fog,
    }

    public static class Weather
    {
        public const float WetBallChance = 0.3f;

        public static WeatherKind Current { get; set; }
        public static bool Rainy => Current is WeatherKind.Rain or WeatherKind.Storm;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = WeatherKind.Clear;
    }
}
