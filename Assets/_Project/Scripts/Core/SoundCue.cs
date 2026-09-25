namespace Bouncer.Core
{
    /// <summary>
    /// Что прозвучало в игре. Геймплей только сообщает событие (<see cref="GameEvents.PlaySound"/>),
    /// а какой клип, громкость и разброс высоты — решает банк звуков (Bouncer.Audio).
    /// </summary>
    public enum SoundCue
    {
        // Игрок и мяч
        Throw,
        ThrowCharged,
        ThrowCandle,
        Catch,
        CatchCandle,
        CatchMiss,
        Pickup,
        Dash,
        PlayerHurt,
        PlayerKnockedOut,
        BallWall,
        AreaThud,
        SwingBat,

        // Враги
        EnemyHit,
        EnemyHitStrong,
        RolyPolyChime,
        RolyPolyPop,
        PupsikSqueak,
        PupsikPop,
        SoldierThrow,
        SoldierPop,
        BossSplit,
        SpawnWarning,

        // Забег и интерфейс
        LevelUp,
        CardPick,
        UiMove,
        Victory,
        GameOver,

        // Прогулка (новые — только в конец: банк звуков хранит номера)
        Coin,
        Portfolio,
        KioskOpen,
        Purchase,
        NotEnoughCoins,
        EliteSpawn,
        Explosion,
        ShieldBlock,
        BallStuck,

        // Коробка, стройка и погода
        /// <summary>Свисток Физрука: новое правило раунда.</summary>
        Whistle,
        Thunder,
        /// <summary>Манекен замер в новой позе — пластиковый щелчок.</summary>
        MannequinPose,
        /// <summary>Тень выходит из темноты или плотнеет для удара.</summary>
        ShadowHiss,
        /// <summary>Чучело поймало мяч.</summary>
        ScarecrowCatch,
        /// <summary>Фонарь погас.</summary>
        LampOut,
        /// <summary>«Второе дыхание»: игрока не выбило.</summary>
        SecondWind,
    }
}
