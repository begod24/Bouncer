using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Player Stats", fileName = "PlayerStats_")]
    public sealed class PlayerStats : ScriptableObject
    {
        [Header("Движение")]
        public float moveSpeed = 7f;
        public float acceleration = 70f;
        public float deceleration = 90f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 1440f;
        [Range(0f, 1f)] public float chargingMoveMultiplier = 0.55f;
        [Range(0f, 1f)] public float catchingMoveMultiplier = 0.8f;

        [Header("Рывок-уклонение")]
        public float dashDistance = 4.5f;
        public float dashDuration = 0.16f;
        public float dashCooldown = 0.9f;
        [Tooltip("Перезарядка рывка не короче этого, как бы карточки её ни сокращали")]
        public float dashCooldownMin = 0.6f;
        [Tooltip("Сколько секунд от начала рывка мячи и удары проходят сквозь игрока")]
        public float dashInvulnerability = 0.22f;

        [Header("Жизни")]
        [Min(1)] public int maxLives = 3;
        [Tooltip("Больше стольких сердец не бывает, сколько бы карточек ни прибавляли («Бабушкины пирожки»)")]
        [Min(1)] public int maxLivesCap = 4;
        public float hurtInvulnerability = 1.2f;
        [Tooltip("Как быстро гаснет отброс после удара")]
        public float knockbackDamping = 10f;

        [Header("Мячи")]
        [Tooltip("Больше не используется: на арене игрок сразу со всеми своими мячами (maxBalls)")]
        [Min(0)] public int startBalls = 2;
        [Tooltip("Сколько своих мячей у игрока (и сколько помещается в руки)")]
        [Min(1)] public int maxBalls = 3;
        public float pickupRadius = 1.5f;
        [Tooltip("Хват ПКМ: лежащий мяч ближе этого (м) летит в руки, если рядом нет мяча, который можно поймать")]
        public float grabRadius = 2.5f;
        [Tooltip("Хват не срабатывает, если ближе этого (м) летит мяч, который можно поймать: тогда ПКМ — ловля")]
        public float grabBlockRadius = 5f;
        [Tooltip("Свой мяч, пропавший не в руках (выпал за арену и т.п.), возвращается через столько секунд")]
        public float lostBallReturnDelay = 1.5f;
        [Tooltip("Высота, с которой вылетает мяч")]
        public float throwHeight = 1.1f;
        public float throwForwardOffset = 0.6f;
        [Tooltip("Отпустил быстрее — обычный бросок. Дольше — начинается заряд.")]
        public float tapThreshold = 0.15f;
        [Tooltip("Время от начала заряда до полного")]
        public float chargeTime = 0.7f;
        public float throwCooldown = 0.18f;

        [Header("Ловля")]
        [Tooltip("Сколько секунд после нажатия открыто окно ловли")]
        public float catchWindow = 0.25f;
        [Tooltip("Мяч пойман за столько секунд после нажатия — ловля идеальная (в последний момент). " +
                 "Только она лечит и удерживает сильный мяч")]
        public float perfectCatchWindow = 0.1f;
        public float catchRadius = 1.5f;
        [Tooltip("Летящий мяч ловится только спереди: половина угла сектора от взгляда, градусы. 180 — со всех сторон")]
        [Range(0f, 180f)] public float catchHalfAngle = 90f;
        [Tooltip("Перезарядка после промаха")]
        public float catchMissCooldown = 0.45f;
        [Tooltip("Прибавка к перезарядке за каждый промах подряд — против нажатий наугад")]
        public float catchMissStreakPenalty = 0.3f;
        [Tooltip("После скольких промахов подряд перезарядка перестаёт расти")]
        [Min(0)] public int catchMissStreakMax = 3;
        [Tooltip("Столько секунд не нажимал ловлю после перезарядки — промахи подряд забыты")]
        public float catchMissStreakReset = 1f;
        public float catchSuccessCooldown = 0.1f;
        [Tooltip("До какой высоты над полом можно поймать «свечку»")]
        public float candleCatchHeight = 3.2f;
        [Tooltip("Сколько жизней даёт мяч врага, пойманный идеально")]
        [Min(0)] public int catchHeal = 1;
        [Tooltip("Сколько жизней даёт мяч врага, пойманный рано (не идеально)")]
        [Min(0)] public int earlyCatchHeal;
        [Tooltip("Идеальная ловля лечит не чаще раза в столько секунд")]
        public float catchHealCooldown = 10f;
        [Tooltip("Сильный мяч, пойманный идеально, отталкивает назад: скорость отброса (15 ≈ 1,5 м)")]
        public float strongCatchPush = 15f;
        [Tooltip("Медбол Физрука: пойманный сбивает с ног на столько секунд")]
        public float heavyKnockdown = 0.8f;
        [Tooltip("Медбол: скорость отброса пойманного")]
        public float heavyPush = 12f;
        [Tooltip("С какой скоростью сильный мяч, выбитый из рук, отскакивает вперёд и вверх")]
        public Vector2 fumbleBounce = new(2.5f, 3.5f);

        [Header("Карточки")]
        [Tooltip("Подкат: на каком расстоянии рывок сбивает врагов, м")]
        public float tackleRadius = 1.1f;
        [Tooltip("Подкат: сила отброса сбитого врага")]
        public float tackleKnockback = 14f;
        [Tooltip("Подкат: сбитый враг оглушён столько секунд (урона нет)")]
        public float tackleStun = 0.8f;
        [Tooltip("Подкат срабатывает не чаще раза в столько секунд")]
        public float tackleCooldown = 0.8f;
        [Tooltip("«Чехарда»: прыжок через врага длится столько секунд…")]
        public float leapTime = 0.45f;
        [Tooltip("…на такой высоте, м")]
        public float leapHeight = 1.8f;
        [Tooltip("…и приземляется за врагом на таком расстоянии от него, м")]
        public float leapBeyond = 1.6f;
        [Tooltip("«Чехарда»: того, через кого прыгнул, оглушает на столько секунд")]
        public float leapStun = 1.5f;
        [Tooltip("«Чехарда»: приземление сбивает с ног всех в таком радиусе, м…")]
        public float leapLandRadius = 2f;
        [Tooltip("…на столько секунд")]
        public float leapLandStun = 0.8f;
        [Tooltip("«Домино»: на каком расстоянии выбитый враг сбивает соседей, м")]
        public float dominoRadius = 2.3f;
        [Tooltip("«Домино»: через сколько секунд падает следующая костяшка")]
        public float dominoDelay = 0.08f;
        [Tooltip("«Домино»: сила отброса сбитых соседей")]
        public float dominoKnockback = 8f;

        [Header("Сумерки")]
        [Tooltip("Взгляд: манекены замирают в секторе с такой половиной угла от направления взгляда, градусы")]
        [Range(0f, 180f)] public float gazeHalfAngle = 35f;
        [Tooltip("«Замри!»: радиус, в котором идеальная ловля замораживает врагов, м")]
        public float whistleRadius = 6f;
        [Tooltip("«Замри!»: каждая следующая карточка прибавляет к радиусу, м…")]
        public float whistleStackRadius = 3f;
        [Tooltip("…и к заморозке, с")]
        public float whistleStackTime = 0.5f;
        [Tooltip("«Второе дыхание»: выбитый игрок встаёт сам через столько секунд")]
        public float secondWindDelay = 3f;
        [Tooltip("«Второе дыхание»: сколько секунд игрок неуязвим, когда встал")]
        public float secondWindInvulnerability = 2f;
        [Tooltip("Тёмный мяч Бабая: попадание замедляет на столько секунд…")]
        public float darkSlowTime = 2f;
        [Tooltip("…до такой доли скорости")]
        [Range(0.1f, 1f)] public float darkSlowMultiplier = 0.55f;

        [Header("Новые карточки")]
        [Tooltip("«Тамагочи»: столько пойманных мячей, чтобы питомец наелся")]
        [Min(1)] public int tamagotchiFood = 5;
        [Tooltip("«Тамагочи»: сколько секунд игрок неуязвим после спасения")]
        public float tamagotchiInvulnerability = 1.5f;
        [Tooltip("«Круговая порука»: радиус волны «замри», м")]
        public float circleGuardRadius = 4f;
        [Tooltip("«Круговая порука»: враги в волне замирают на столько секунд")]
        public float circleGuardFreeze = 1.5f;
        [Tooltip("«Круговая порука»: неуязвимость того, кто поднял, с")]
        public float circleGuardInvulnerability = 1f;

        [Header("Прицел")]
        [Tooltip("Высота плоскости, на которую проецируется курсор (уровень груди врагов)")]
        public float aimPlaneHeight = 0.8f;
        public float stickDeadzone = 0.25f;
        public float autoAimRange = 18f;
        [Tooltip("Половина угла конуса автоприцела, градусы")]
        public float autoAimAngle = 35f;
    }
}
