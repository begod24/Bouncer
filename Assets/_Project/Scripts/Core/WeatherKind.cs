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
}
