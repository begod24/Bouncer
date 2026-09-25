using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>Числа «Того, кто в сумерках». Тройки чисел (x, y, z) — по фазам босса: первая, вторая, третья.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Dusk Boss Definition", fileName = "Enemy_DuskBoss")]
    public sealed class DuskBossDefinition : ScriptableObject
    {
        public string displayName = "Тот, кто в сумерках";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 120;

        [Header("Фазы: новая — по жизням или по времени боя, что наступит раньше")]
        [Range(0f, 1f)] public float phase2At = 0.7f;
        [Range(0f, 1f)] public float phase3At = 0.35f;
        [Tooltip("Секунд боя до второй и до третьей фазы")]
        public float phase2Time = 55f;
        public float phase3Time = 115f;
        [Tooltip("Пауза между умениями в каждой фазе, с")]
        public Vector3 abilityInterval = new(7f, 6f, 5f);
        [Tooltip("Первое умение — через столько секунд после появления")]
        public float firstAbility = 5f;

        [Header("Ходьба: скользит к игроку и держит дистанцию")]
        public float moveSpeed = 2.6f;
        public float keepDistance = 7f;
        [Tooltip("Скорость поворота, градусов в секунду: медленная — его можно обойти сбоку")]
        public float turnSpeed = 75f;
        [Tooltip("Сколько секунд вылезает из-под земли в начале боя")]
        public float appearTime = 1.4f;

        [Header("Клюкой вблизи: замах → удар → восстановление")]
        public float swipeRange = 3.4f;
        public float swipeWindup = 0.6f;
        public float swipeTime = 0.2f;
        public float swipeRecover = 0.6f;
        public float swipeCooldown = 1.8f;
        public float swipeReach = 4f;
        [Range(0f, 180f)] public float swipeHalfAngle = 80f;
        [Min(0)] public int swipeDamage = 1;
        public float swipeKnockback = 14f;

        [Header("Сильный мяч издалека")]
        public float throwMinRange = 5f;
        public float throwMaxRange = 22f;
        public float throwWindup = 0.5f;
        public float throwCooldown = 4f;
        public float ballSpeed = 18f;
        public float ballGravity = 2f;
        [Min(0)] public int ballDamage = 1;
        public float ballKnockback = 8f;

        [Header("«Ловец»: хватает мячи спереди, даже заряженные, и отвечает веером сильных")]
        [Tooltip("Ловит мяч, пролетающий ближе этого, м")]
        public float catchRadius = 2.4f;
        [Range(0f, 180f)] public float catchHalfAngle = 70f;
        [Min(1)] public int maxHeld = 2;
        [Tooltip("Через сколько секунд после ловли бросает веер")]
        public float answerDelay = 0.5f;
        [Min(1)] public int answerBalls = 3;
        [Tooltip("Веер: угол между крайними мячами, градусы")]
        public float answerSpread = 30f;
        public float answerSpeed = 16f;

        [Header("«Мешок»: собирает мячи с земли")]
        [Min(1)] public int sackCapacity = 12;
        [Tooltip("Сгребает мячи в этом радиусе вокруг себя, м")]
        public float sweepRadius = 4f;
        [Tooltip("Сколько куч мячей обходит за раз")]
        [Min(1)] public int sweepStops = 2;
        public float sweepCrouch = 0.5f;
        public float sweepSpeedMultiplier = 1.7f;
        [Tooltip("Дольше этого к куче не идёт — сгребает там, где стоит")]
        public float sweepTimeout = 3f;
        [Tooltip("Умение берётся, только если на земле лежит столько мячей")]
        [Min(1)] public int sweepMinBalls = 2;
        [Tooltip("Насколько больше становится мешок с каждым мячом")]
        public float sackGrowth = 0.06f;
        [Tooltip("Попали в мешок — мячи высыпались, босс растерялся на столько секунд")]
        public float spillStagger = 1.2f;

        [Header("«Прыжок на шесте»")]
        public float vaultWindup = 0.65f;
        public float vaultTime = 0.85f;
        public float vaultHeight = 4f;
        [Tooltip("Радиус удара при приземлении, м")]
        public float vaultRadius = 3.2f;
        [Min(0)] public int vaultDamage = 1;
        public float vaultKnockback = 13f;
        [Tooltip("Сколько секунд после приземления стоит с воткнутым шестом — бить можно с любой стороны")]
        public float vaultStuck = 1.3f;
        [Tooltip("Прыжков подряд в каждой фазе")]
        public Vector3Int vaultCount = new(1, 2, 3);
        [Tooltip("Упреждение: куда прыгнуть, с")]
        public float vaultLead = 0.35f;

        [Header("«Карусель»: спираль мячей, все можно поймать")]
        public float carouselWindup = 0.6f;
        public float carouselTime = 3f;
        [Tooltip("Пауза между залпами, с")]
        public float carouselInterval = 0.2f;
        [Tooltip("Рукавов спирали в каждой фазе")]
        public Vector3Int carouselArms = new(2, 2, 3);
        [Tooltip("На сколько градусов поворачивается спираль с каждым залпом")]
        public float carouselTurn = 26f;
        public float carouselBallSpeed = 9f;
        public float carouselBallGravity = 1.5f;
        [Tooltip("Сколько секунд шатается после карусели")]
        public float carouselDizzy = 1.3f;

        [Header("«Считалочка»")]
        [Tooltip("Сколько секунд считает — столько есть, чтобы спрятаться")]
        public float countTime = 5f;
        [Tooltip("Сколько секунд оборачивается и ищет")]
        public float searchTime = 0.8f;
        [Tooltip("Кого увидел — в того столько сильных мячей")]
        [Min(1)] public int foundVolley = 3;
        public float foundInterval = 0.25f;
        [Tooltip("Никого не нашёл — растерянно озирается столько секунд")]
        public float confusedTime = 2f;
        [Tooltip("Высота глаз над землёй, м: отсюда проверяется, видно ли игрока")]
        public float eyeHeight = 4.2f;

        [Header("«Гасит свет»")]
        public float lightsCast = 1f;
        public float lightsOutTime = 9f;
        [Tooltip("Теней в каждой фазе")]
        public Vector3Int shadowCount = new(2, 3, 4);
        [Tooltip("На каком расстоянии от игрока выходят тени, м")]
        public Vector2 shadowDistance = new(6f, 10f);

        [Header("«Вороньё»")]
        public float crowsCast = 0.8f;
        [Tooltip("Ворон в каждой фазе")]
        public Vector3Int crowCount = new(3, 4, 6);

        [Header("«Прятки»")]
        public float hideVanish = 0.8f;
        public float hideRise = 0.8f;
        [Tooltip("Сколько секунд прячется, если его не нашли")]
        public float hideTime = 9f;
        [Tooltip("Ложных чучел в каждой фазе")]
        public Vector3Int decoyCount = new(2, 3, 4);
        [Tooltip("Чучела встают не ближе этого к игроку, м")]
        public float decoyMinDistance = 6f;
        [Tooltip("И не ближе этого друг к другу, м")]
        public float decoySpacing = 5f;
        [Tooltip("Во сколько раз больнее попадание, когда настоящего нашли")]
        [Min(1)] public int revealDamageMultiplier = 2;
        public float revealStagger = 1.5f;
        [Tooltip("Глаза настоящего вспыхивают: сколько горят и сколько тёмные, с")]
        public Vector2 eyePulse = new(0.5f, 1.1f);
        [Tooltip("Из разбитого ложного чучела вылетает столько ворон")]
        [Min(0)] public int decoyCrows = 3;

        [Header("Мама позвала: босс в ярости, прыгает на игрока, пока тот бежит к подъезду")]
        public float rageVaultInterval = 2.2f;
        public float rageVaultWindup = 0.45f;
        public float rageStagger = 1.2f;

        [Header("Смерть")]
        public float debrisForce = 6f;

        /// <summary>Значение тройки для фазы 0, 1 или 2.</summary>
        public static float ByPhase(Vector3 values, int phase) => phase <= 0 ? values.x : phase == 1 ? values.y : values.z;

        public static int ByPhase(Vector3Int values, int phase) => phase <= 0 ? values.x : phase == 1 ? values.y : values.z;
    }
}
