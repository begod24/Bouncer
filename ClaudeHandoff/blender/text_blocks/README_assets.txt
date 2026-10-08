BOUNCER / «Вышибалы» — ассеты (враги, мячи, двор, этап 1, заготовки на коробку и стройку)

КАК УСТРОЕНЫ ЦВЕТА
- У всех моделей материал M_Palette (+ M_Decals, если на модели есть надписи, см. ниже).
- Цвет берётся из текстуры-палитры 64x64: сетка 8x8, каждая ячейка 8x8 px.
  Каждая грань целиком лежит UV-координатами внутри одной ячейки.
- Текстуры (упакованы в этот .blend):
    T_Palette_Day      — дневная палитра (основная)
    T_Palette_Morning  — утренняя (теплее, мягче), та же раскладка ячеек
    T_Palette_Evening / T_Palette_Dusk / T_Palette_Night — вечер, сумерки, ночь (ba_lib.EXTRA_PALETTES)
    T_Palette_Emission — свечение: светятся только ячейки window_lit и lamp
- Смена времени суток = подмена текстуры палитры в материале (в Unity — TimeOfDay_* профили).
- Перекрасить конкретную грань: в UV Editor перенести её UV в другую ячейку.
  Перекрасить готовую модель кодом: ba_elites.recolor(root, {"red": "gold"}).
- В Unity: Filter Mode = Point, без mip-map, без сжатия.

НАДПИСИ И РИСУНКИ — ТОЛЬКО ТЕКСТУРОЙ (не геометрией!)
- Вывески, цифры на монетках, граффити, звёздочки на обёртках — прямоугольник из 2 треугольников
  (Builder.decal) с материалом M_Decals. UV0 («UVMap») — ячейка палитры (цвет, смена времени суток),
  UV1 («UVDecal») — место в атласе-маске T_Decals.
- Атлас рисует Tools/decal_art.py в репозитории: Assets/_Project/Art/Decals/T_Decals.png + Tools/decal_atlas.json.
  Новая надпись: добавить строку в конец ENTRIES, запустить скрипт, здесь ba_lib.reload_decals().
- В Unity шейдер Bouncer/PaletteDecal (маска отрезает всё лишнее, цвет — из той же палитры).
- Буквы из шрифта в геометрию НЕ переводить: это сотни треугольников на надпись вместо двух.

РАСКЛАДКА ПАЛИТРЫ (строка сверху вниз, ячейки слева направо)
 0: red_dark red red_light pink pink_light maroon coral orange_red
 1: orange orange_light ochre yellow cream sand sand_dark gold
 2: green_dark green green_light lime olive teal mint bottle
 3: navy blue blue_light sky cyan steel blue_pale indigo
 4: purple lavender skin_light skin rubber rubber_dark lips eye_blue
 5: wood_dark wood wood_light leather leather_dark rust rust_light brown
 6: black grey_dark grey grey_light concrete concrete_light asphalt white
 7: tin tin_dark panel_beige panel_blue window window_lit lamp roof

СОГЛАШЕНИЯ
- Метры, Z вверх, перед персонажей смотрит в -Y (при экспорте в Unity станет +Z).
- Точка отсчёта (origin) ассета: у земли в центре. У мячей и монеток — центр.
- Мячи нормированы к диаметру 1 м (как сфера-примитив Unity): в префабе Ball масштаб меша = 2 * radius (0.44).
- Пикапы и предметы (монетки, портфель, жвачки, лимонад, бутерброд) — сразу в игровом размере (крупнее настоящих).
- Env_* (земля двора, коробки, стройки; борта коробки) — в координатах арены Unity (x — восток, y — север),
  перед экспортом повёрнуты на 180°, чтобы север в Unity остался севером.
- Все грани плоские (flat shading). N-угольники заранее разбиты на треугольники.
- Светящиеся места (фонари, окна, огонь, глаза тени) — ячейки lamp / window_lit: горят вечером и ночью.

ИЕРАРХИИ И ОСИ ВРАЩЕНИЯ (для процедурной анимации кодом, без скелета)
- Enemy_RolyPoly: RolyPoly_Body (pivot y=0.45) + RolyPoly_Head (pivot y=1.05).
  Те же две части годятся для обломков (RolyPolyDebris). Размеры как у коллайдеров префаба.
- Boss_BigRolyPoly: та же модель x3 (~4 м), трещины, облупленная краска, злые брови.
- Enemy_Pupsik (~0.8 м): Pupsik_Body -> ArmL/ArmR (пивот в плече), LegL/LegR (пивот в бедре).
- Enemy_TinSoldier (~1.4 м): TinSoldier_Body -> ArmL, ArmR -> TinSoldier_Ball, LegL/LegR.
- Enemy_Bear (~1.8 м, танк): Bear_Body -> Bear_Head (шея), ArmL/R (плечи), LegL/R (бёдра),
  Bear_BallSocket (пустышка: сюда встаёт застрявший мяч, в дыре на животе).
- Enemy_Chick (~0.9 м, камикадзе): Chick_Body -> Chick_Head (клюёт), Chick_Key (крутится вокруг своей оси +Y),
  LegL/R. Части = обломки взрыва.
- Enemy_Top (юла, ~1.25 м): Top_Body (крутится вокруг Z, пивот — остриё на земле) -> Top_Handle (качается вверх-вниз).
- Enemy_RockingHorse (~1.5 м): одна часть RockingHorse_Body, пивот посередине полозьев на земле.
- Enemy_Mannequin (~1.9 м): Mannequin_Body -> Head (шея), ArmL/R, LegL/R.
- Enemy_Scarecrow (~2 м, на шесте): Scarecrow_Body -> Head, ArmL/R (перекладина в рукавах).
- Enemy_Shadow (~1.9 м): Shadow_Body -> Head, ArmL/R. Дым — VFX в Unity.
- Boss_Fizruk = манекен x2.2 в олимпийке (те же части).
- Boss_Dusk («Тот, кто в сумерках», ba_final.py, ~5 м) — Бабай с мешком: Dusk_Body (пивот z=0.6) -> Dusk_Head (шея)
  -> Dusk_Eyes (горящие глаза отдельно: у ложных чучел «Пряток» их нет), Dusk_ArmL/R (плечи),
  Dusk_ArmR -> Dusk_Staff (клюка, пивот в кулаке), Dusk_Sack (мешок на спине, пивот в завязке — растёт от мячей),
  Dusk_CrowL/R (вороны на плечах: улетают стаей).
- Enemy_Crow (~1.4 м в размахе, ba_final.py): Crow_Body (origin — центр тела, она летает) -> Crow_WingL/R (машут вокруг Y).
- Elite_* — те же части, что у обычного врага (имена с приставкой Elite_): золотая матрёшка в кокошнике,
  пупс в чепчике с соской, солдатик-офицер с саблей, мишка-моряк, петушок, юла-спутник, конь-огонь.
  Элитки стройки (ba_dusk_elites.py): манекен из универмага (красное платье, шляпа, жемчуг; мяч в руке — в Unity),
  чучело в ватнике и ушанке (глаза светятся, две вороны), тень в шляпе (длинные когти).
  Крупнее делается масштабом префаба.
- Prop_Swings -> Prop_Swings_Seat (качается вокруг X). Prop_Clothesline -> Prop_Clothesline_Laundry.

ДЕТИ (ba_kids.py, ba_kids_anim.py) — у них, в отличие от врагов, есть скелет
- Kid_Otlichnik, Kid_Tolstyak, Kid_Melkaya, Kid_Huligan: Armature с именами костей Humanoid (Hips, Spine, Neck,
  Head, LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand, LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes и Right*)
  в T-позе. Один меш на ребёнка, каждая часть на 100% привязана к своей кости, суставы прикрыты шариками.
- Все одного роста (~1.43 м, крупные головы, как на листе «Дети двора»); разные ширина плеч/бёдер и одежда.
- Anim_Kids — эталонный скелет со всеми клипами (действия): Idle, Run_F/B/L/R, Charge_Start/Full, Throw, Catch,
  Caught, Hurt, Dash, Slide, Down, Cheer, Pose_Think/Cheer/Ready/Tough; 30 кадров/с. Позы заданы поворотами в осях
  покоя скелета (ba_kids.delta_quat), поэтому одинаково ложатся на любого ребёнка. В Unity — Humanoid.
- Экспорт: ba_kids.export_kids(), ba_kids_anim.export() -> Art/Models/Kids (тот же поворот на 180°: меш и кости).
- Реквизит (коллекция KidProps): Prop_Book («ФИЗИКА»), Prop_Chips («ЧИПСЫ»), Prop_JumpRope, Prop_Football
  -> Art/Models/KidProps через ba_export.export_named.

СПИСОК
 Враги:   Enemy_RolyPoly, Enemy_Pupsik, Enemy_TinSoldier, Enemy_Bear, Enemy_Chick, Enemy_Top, Enemy_RockingHorse,
          Enemy_Mannequin, Enemy_Scarecrow, Enemy_Shadow, Elite_* (7 игрушек + Elite_Mannequin, Elite_Scarecrow,
          Elite_Shadow), Boss_BigRolyPoly, Boss_Fizruk, Boss_Dusk, Enemy_Crow
 Мячи:    Ball_Rubber, Ball_Volleyball, Ball_Medicine, Ball_Tennis, Ball_Deflated
 Пикапы:  Pickup_Coin_1 (1 тиын), Pickup_Coin_5 (5 тиын), Pickup_Coin_Tenge (1 тенге), Pickup_Portfel
 Ларёк:   Prop_Kiosk («Союзпечать»), Item_Gum_Common/Rare/Gold, Item_Lemonade, Item_Sandwich
 Двор:    Prop_Sandbox, Prop_Swings, Prop_Rocket, Prop_Bench, Prop_Bench_NoBack, Prop_Clothesline, Prop_StreetLamp,
          Prop_Garage_A/B/C, Prop_Curb, Prop_FenceLow, Prop_TrashBin, Prop_Tree_Birch, Prop_Tree_Poplar, Prop_Bush,
          Prop_Hedge, Prop_Panelka_5F, Prop_Panelka_9F, Env_YardGround
 Коробка: Env_RinkGround, Env_RinkBoards (40 x 26 м, R 6), Rink_Goal, Rink_Floodlight, Rink_TeamBench,
          Rink_Board_2m, Rink_Board_2m_Graffiti
 Дети:    Kid_Otlichnik, Kid_Tolstyak, Kid_Melkaya, Kid_Huligan, Anim_Kids (клипы); Prop_Book, Prop_Chips,
          Prop_JumpRope, Prop_Football
 Стройка: Env_SiteGround, Site_SlabStack, Site_SlabTilted, Site_ColumnRebar, Site_RebarBundle, Site_Ring,
          Site_Ring_Lying, Site_RingsStack, Site_SandPile, Site_GravelPile, Site_BrickPallet, Site_Pallet,
          Site_Barrel, Site_FireBarrel, Site_CableReel, Site_Cabin, Site_Fence, Site_Fence_Broken, Site_Frame,
          Site_Crane, Site_WorkLight, Site_LampPost
 Новые враги (ba_toys2): Enemy_ShieldSoldier (часть Lid — крышка), Enemy_Drummer, Enemy_Frog, Enemy_Ballerina
          (Base + Body), Enemy_Lunokhod (Lid, 8 колёс, Muzzle), Enemy_RCCar, Enemy_DendyGun, Enemy_CryDoll
 Боссы 2 (ba_bosses2): Boss_Transformer (робот) + Boss_Transformer_Car (та же иерархия в позе машины),
          Boss_Transformer_Battery, Boss_Hare (часть Stuffing — вата из шва, Carrot — морковка)
 Барахолка (ba_bazaar): Env_BazaarGround, Bazaar_Stall_Toys/Cassettes/Clothes/Kitchen (Table, Rack, Awning,
          Goods), Bazaar_Kiosk_Toys (Door)/Cassettes/Exchange, Bazaar_Container/_Blue/_Open, Bazaar_Cart,
          Bazaar_BoxPile/_B (по коробке на часть), Bazaar_LightPole, Bazaar_Gate («РЫНОК»)
 Детсад (ba_kindergarten): Env_KindergartenGround, Kg_Building, Kg_Fence, Kg_Carousel (Base + Deck),
          Kg_RocketSlide, Kg_Veranda/_B, Kg_Playhouse, Kg_Lamp, Kg_TyreBed, Kg_TyreSwan, Kg_Bench

ПЕРЕСБОРКА
- Все модели сгенерированы скриптами ba_*.py (лежат тут же, в Text Editor). build_all.py пересобирает всё
  с нуля (сиды фиксированы, результат тот же). Ручные правки в мешах при этом затрутся.
- Коллекция _Preview: камера и солнце только для превью, в экспорт не идут.
- Экспорт: ba_export.export_named([...]) — только нужные модели; export_all() трогает все FBX.
- Трансформер: детали построены в координатах машины, поза машины лежит в root["car_pose"];
  ba_bosses2.set_pose(root, car) показывает позу, ba_bosses2.export_transformer() пишет обе FBX
  (робот и машина). В Unity TransformerRig берёт позу машины из Boss_Transformer_Car по именам частей.

СПЛЭШ-СКРИН И ИКОНКА (сцены Splash и Icon)
- Splash: вид с нашей половины меловой площадки на закате, мячи на центральной линии, напротив армия
  игрушек во главе с большой неваляшкой, за ними двор и панельки. Небо сверху оставлено под название.
- Icon: крупный план злой большой неваляшки и летящий волейбольный мяч, рендер на прозрачном фоне.
- Объекты сцен — связанные копии ассетов (общие меши, префикс SPL_, у иконки ещё суффикс _Icon),
  материал SPL_Palette — копия M_Palette с дымкой по расстоянию. В экспорт в Unity ничего не попадает.
- Скрипты: ba_branding.py (расстановка, свет, небо, рендер), build_branding.py (пересборка обеих сцен).
  После build_all.py запусти build_branding.py ещё раз, иначе в сценах останутся старые меши.
- Рендер в Cycles: Splash 3840x2160, Icon 2048x2048. Финальные PNG (сплэш 1920x1080 с названием EN/RU,
  иконка 1024x1024 в форме иконки macOS) собирает Tools/branding_art.py в репозитории и кладёт
  в Assets/_Project/Art/UI/Branding.
