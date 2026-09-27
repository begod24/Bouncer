namespace Bouncer.Core
{
    /// <summary>Погода на арене — случайное событие прогулки.</summary>
    public enum WeatherKind
    {
        Clear,
        /// <summary>Дождь: мокрая палитра, лужи гасят мячи и замедляют.</summary>
        Rain,
        /// <summary>Гроза: дождь и молнии, которые на миг освещают всё.</summary>
        Storm,
        /// <summary>Туман: видно метров на двенадцать.</summary>
        Fog,
    }

    /// <summary>Какая погода сейчас на арене — для тех, кто не знает про WeatherController (мокрые мячи).</summary>
    public static class Weather
    {
        /// <summary>В дождь и грозу такая доля мячей врагов летит мокрыми.</summary>
        public const float WetBallChance = 0.3f;

        public static WeatherKind Current { get; set; }
        public static bool Rainy => Current is WeatherKind.Rain or WeatherKind.Storm;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = WeatherKind.Clear;
    }
}
