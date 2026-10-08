# Bouncer — опись Blender-файлов (снято 2026-10-08, Blender 5.1.2, только чтение)

Файлы лежат в этой папке в `blender/`. **Bouncer.blend** — источник всех игровых 3D-ассетов (модели, палитры, скелеты и анимации детей) и скриптов-генераторов. **Bouncer_Marketing/** — отдельные файлы под контент (трейлер, соцсети), ассеты в них подключены линком из `../Bouncer.blend` (поэтому на новом компьютере положи `Bouncer_Marketing` рядом с `Bouncer.blend` в одну папку: `Bouncer.blend` и `Bouncer_Marketing/` на одном уровне).

Все скрипты из текстовых блоков Bouncer.blend выгружены в `blender/text_blocks/` как обычные файлы (для чтения и diff; в самом .blend они остаются встроенными и именно оттуда запускаются).

> Важно: `ba_export.py` и `ba_lib.py` содержат жёстко прописанный путь `REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer"`. На новом компьютере замени его на путь к клону репозитория (в самом .blend — в Text Editor, блоки `ba_export.py` и `ba_lib.py`), иначе экспорт FBX и палитр уйдёт по старому пути.

## Bouncer.blend
- Blender 5.1.2; scenes: Icon, Scene, Splash
- objects 487, meshes 316, armatures 5, actions 19, materials M_Decals, M_Palette, SPL_Palette
- images: T_Decals, T_Palette_Day, T_Palette_Dusk, T_Palette_Emission, T_Palette_Evening, T_Palette_Morning, T_Palette_Night

### Scenes
- **Icon**: frames 1-250 @ 24 fps, 2048x2048, engine CYCLES, camera SPL_IconCam, objects 7, output `//`
- **Scene**: frames 1-250 @ 24 fps, 500x500, engine BLENDER_EEVEE, camera _PreviewCam, objects 367, output `/private/tmp/claude-501/-Users-bekbolataldiyarov-Desktop-projects-Game-Projects-Bouncer/fc434d48-58b6-4d09-853a-5049fb14e684/scratchpad/battery.png`
- **Splash**: frames 1-250 @ 24 fps, 3840x2160, engine CYCLES, camera SPL_Cam, objects 113, output `//`

### Collections (root objects only)
- **_Preview** (2 objects): _PreviewCam, _PreviewSun
- **AbilityProps** (8 objects): Prop_CameraSmena, Prop_CapGun, Prop_Cassette, Prop_FencePlank, Prop_FenceRail, Prop_Magnet, Prop_Tamagotchi, Prop_Walkie
- **Balls** (8 objects): Ball_Basketball, Ball_Deflated, Ball_Football, Ball_Medicine, Ball_PingPong, Ball_Rubber, Ball_Tennis, Ball_Volleyball
- **Bazaar** (48 objects): Bazaar_BoxPile, Bazaar_BoxPile_B, Bazaar_Cart, Bazaar_Container, Bazaar_Container_Blue, Bazaar_Container_Open, Bazaar_Gate, Bazaar_Kiosk_Cassettes, Bazaar_Kiosk_Exchange, Bazaar_Kiosk_Toys, Bazaar_LightPole, Bazaar_Stall_Cassettes, Bazaar_Stall_Clothes, Bazaar_Stall_Kitchen, Bazaar_Stall_Toys, Env_BazaarGround
- **Collection** (0 objects): 
- **Enemies** (213 objects): Boss_BigRolyPoly, Boss_Dusk, Boss_Fizruk, Boss_Hare, Boss_Transformer, Boss_Transformer_Battery, Elite_Bear, Elite_Chick, Elite_Mannequin, Elite_Pupsik, Elite_RockingHorse, Elite_RolyPoly, Elite_Scarecrow, Elite_Shadow, Elite_TinSoldier, Elite_Top, Enemy_Ballerina, Enemy_Bear, Enemy_Chick, Enemy_Crow, Enemy_CryDoll, Enemy_DendyGun, Enemy_Drummer, Enemy_Frog, Enemy_Lunokhod, Enemy_Mannequin, Enemy_Pupsik, Enemy_RCCar, Enemy_RockingHorse, Enemy_RolyPoly, Enemy_Scarecrow, Enemy_Shadow, Enemy_ShieldSoldier, Enemy_TinSoldier, Enemy_Top
- **Icon** (7 objects): SPL_Ball_Volleyball_Icon, SPL_Boss_BigRolyPoly_Icon, SPL_IconCam, SPL_IconRim, SPL_IconSun
- **KidProps** (4 objects): Prop_Book, Prop_Chips, Prop_Football, Prop_JumpRope
- **Kids** (9 objects): Anim_Kids, Kid_Huligan, Kid_Melkaya, Kid_Otlichnik, Kid_Tolstyak
- **Kindergarten** (14 objects): Env_KindergartenGround, Kg_Bench, Kg_Building, Kg_Carousel, Kg_Fence, Kg_Lamp, Kg_Playhouse, Kg_RocketSlide, Kg_TyreBed, Kg_TyreSwan, Kg_Veranda, Kg_Veranda_B
- **Pickups** (4 objects): Pickup_Coin_1, Pickup_Coin_5, Pickup_Coin_Tenge, Pickup_Portfel
- **Rink** (7 objects): Env_RinkBoards, Env_RinkGround, Rink_Board_2m, Rink_Board_2m_Graffiti, Rink_Floodlight, Rink_Goal, Rink_TeamBench
- **Shop** (6 objects): Item_Gum_Common, Item_Gum_Gold, Item_Gum_Rare, Item_Lemonade, Item_Sandwich, Prop_Kiosk
- **Site** (22 objects): Env_SiteGround, Site_Barrel, Site_BrickPallet, Site_Cabin, Site_CableReel, Site_ColumnRebar, Site_Crane, Site_Fence, Site_Fence_Broken, Site_FireBarrel, Site_Frame, Site_GravelPile, Site_LampPost, Site_Pallet, Site_RebarBundle, Site_Ring, Site_Ring_Lying, Site_RingsStack, Site_SandPile, Site_SlabStack, Site_SlabTilted, Site_WorkLight
- **Splash** (113 objects): SPL_Backdrop, SPL_Ball_Deflated, SPL_Ball_Medicine, SPL_Ball_Rubber, SPL_Ball_Tennis, SPL_Ball_Volleyball, SPL_Boss_BigRolyPoly, SPL_Cam, SPL_Cloud.0, SPL_Cloud.1, SPL_Cloud.2, SPL_Cloud.3, SPL_Enemy_Pupsik.0, SPL_Enemy_Pupsik.1, SPL_Enemy_Pupsik.2, SPL_Enemy_Pupsik.3, SPL_Enemy_RolyPoly.0, SPL_Enemy_RolyPoly.1, SPL_Enemy_TinSoldier.0, SPL_Enemy_TinSoldier.1, SPL_Enemy_TinSoldier.2, SPL_Enemy_TinSoldier.3, SPL_Env_YardGround, SPL_Prop_Bench, SPL_Prop_Bush.0, SPL_Prop_Bush.1, SPL_Prop_Bush.2, SPL_Prop_Clothesline, SPL_Prop_FenceLow.0, SPL_Prop_FenceLow.1, SPL_Prop_FenceLow.2, SPL_Prop_FenceLow.3, SPL_Prop_FenceLow.4, SPL_Prop_Garage_A.0, SPL_Prop_Garage_A.3, SPL_Prop_Garage_B.1, SPL_Prop_Garage_B.4, SPL_Prop_Garage_C.2, SPL_Prop_Hedge.0, SPL_Prop_Hedge.1, SPL_Prop_Hedge.2, SPL_Prop_Hedge.3, SPL_Prop_Hedge.4, SPL_Prop_Hedge.5, SPL_Prop_Hedge.6, SPL_Prop_Panelka_5F.0, SPL_Prop_Panelka_5F.1, SPL_Prop_Panelka_9F.0, SPL_Prop_Panelka_9F.1, SPL_Prop_Rocket, SPL_Prop_Sandbox, SPL_Prop_StreetLamp, SPL_Prop_Swings, SPL_Prop_TrashBin, SPL_Prop_Tree_Birch.0, SPL_Prop_Tree_Birch.3, SPL_Prop_Tree_Birch.5, SPL_Prop_Tree_Poplar.1, SPL_Prop_Tree_Poplar.2, SPL_Prop_Tree_Poplar.4, SPL_Sun
- **Yard** (22 objects): Env_YardGround, Prop_Bench, Prop_Bench_NoBack, Prop_Bush, Prop_Clothesline, Prop_Curb, Prop_FenceLow, Prop_Garage_A, Prop_Garage_B, Prop_Garage_C, Prop_Hedge, Prop_Panelka_5F, Prop_Panelka_9F, Prop_Rocket, Prop_Sandbox, Prop_StreetLamp, Prop_Swings, Prop_TrashBin, Prop_Tree_Birch, Prop_Tree_Poplar

### Actions
Catch, Caught, Charge_Full, Charge_Start, Cheer, Dash, Down, Hurt, Idle, Pose_Cheer, Pose_Ready, Pose_Think, Pose_Tough, Run_B, Run_F, Run_L, Run_R, Slide, Throw

### Text blocks (scripts embedded in the file)
- `ba_abilities.py` (10505 chars)
- `ba_balls.py` (5607 chars)
- `ba_bazaar.py` (30119 chars)
- `ba_bosses.py` (6506 chars)
- `ba_bosses2.py` (32226 chars)
- `ba_branding.py` (21728 chars)
- `ba_dusk.py` (18012 chars)
- `ba_dusk_elites.py` (6255 chars)
- `ba_elites.py` (16398 chars)
- `ba_enemies.py` (16613 chars)
- `ba_export.py` (5183 chars)
- `ba_final.py` (18130 chars)
- `ba_kids.py` (37562 chars)
- `ba_kids_anim.py` (18618 chars)
- `ba_kindergarten.py` (22507 chars)
- `ba_lib.py` (32063 chars)
- `ba_pickups.py` (6742 chars)
- `ba_rink.py` (15716 chars)
- `ba_shop.py` (15704 chars)
- `ba_site.py` (26258 chars)
- `ba_toys.py` (21318 chars)
- `ba_toys2.py` (51052 chars)
- `ba_yard.py` (28091 chars)
- `build_all.py` (2841 chars)
- `build_branding.py` (760 chars)
- `README_assets.txt` (10689 chars)

---

## Bouncer_Marketing/KeyArt.blend
- Blender 5.1.2; scenes: KeyArt_4x5, KeyArt_9x16, Splash_16x9, Trailer_EndCard_16x9
- objects 136, meshes 49, armatures 4, actions 5, materials SPL_Palette, M_Palette
- linked libraries: ['//../Bouncer.blend']
- images: T_Palette_Day, T_Palette_Emission

### Scenes
- **KeyArt_4x5**: frames 119-119 @ 30 fps, 1080x1350, engine CYCLES, camera Cam_KeyArt_4x5, objects 126, output `//renders/KeyArt_4x5_`
- **KeyArt_9x16**: frames 0-119 @ 30 fps, 1080x1920, engine CYCLES, camera Cam_KeyArt_9x16, objects 126, output `//renders/KeyArt_9x16/`
- **Splash_16x9**: frames 1-250 @ 24 fps, 3840x2160, engine CYCLES, camera SPL_Cam, objects 113, output `//renders/Splash_16x9_`
- **Trailer_EndCard_16x9**: frames 0-119 @ 30 fps, 1920x1080, engine CYCLES, camera Cam_EndCard_16x9, objects 126, output `//renders/Trailer_EndCard_16x9/`

### Collections (root objects only)
- **EndCardKids** (8 objects): Kid_Huligan_EndCardKids, Kid_Melkaya_EndCardKids, Kid_Otlichnik_EndCardKids, Kid_Tolstyak_EndCardKids
- **KeyArt_Rigs** (4 objects): Focus_KeyArt, Look_Cam_EndCard_16x9, Look_Cam_KeyArt_4x5, Look_Cam_KeyArt_9x16
- **KeyKids** (8 objects): Kid_Huligan_KeyKids, Kid_Melkaya_KeyKids, Kid_Otlichnik_KeyKids, Kid_Tolstyak_KeyKids
- **Splash** (86 objects): SPL_Backdrop, SPL_Ball_Deflated, SPL_Ball_Medicine, SPL_Ball_Rubber, SPL_Ball_Tennis, SPL_Ball_Volleyball, SPL_Cam, SPL_Cloud.0, SPL_Cloud.1, SPL_Cloud.2, SPL_Cloud.3, SPL_Enemy_RolyPoly.0, SPL_Enemy_RolyPoly.1, SPL_Enemy_TinSoldier.0, SPL_Enemy_TinSoldier.1, SPL_Enemy_TinSoldier.2, SPL_Enemy_TinSoldier.3, SPL_Env_YardGround, SPL_Prop_Bench, SPL_Prop_Bush.0, SPL_Prop_Bush.1, SPL_Prop_Bush.2, SPL_Prop_Clothesline, SPL_Prop_FenceLow.0, SPL_Prop_FenceLow.1, SPL_Prop_FenceLow.2, SPL_Prop_FenceLow.3, SPL_Prop_FenceLow.4, SPL_Prop_Garage_A.0, SPL_Prop_Garage_A.3, SPL_Prop_Garage_B.1, SPL_Prop_Garage_B.4, SPL_Prop_Garage_C.2, SPL_Prop_Hedge.0, SPL_Prop_Hedge.1, SPL_Prop_Hedge.2, SPL_Prop_Hedge.3, SPL_Prop_Hedge.4, SPL_Prop_Hedge.5, SPL_Prop_Hedge.6, SPL_Prop_Panelka_5F.0, SPL_Prop_Panelka_5F.1, SPL_Prop_Panelka_9F.0, SPL_Prop_Panelka_9F.1, SPL_Prop_Rocket, SPL_Prop_Sandbox, SPL_Prop_StreetLamp, SPL_Prop_Swings, SPL_Prop_TrashBin, SPL_Prop_Tree_Birch.0, SPL_Prop_Tree_Birch.3, SPL_Prop_Tree_Birch.5, SPL_Prop_Tree_Poplar.1, SPL_Prop_Tree_Poplar.2, SPL_Prop_Tree_Poplar.4, SPL_Sun
- **Splash_Front** (27 objects): SPL_Boss_BigRolyPoly, SPL_Enemy_Pupsik.0, SPL_Enemy_Pupsik.1, SPL_Enemy_Pupsik.2, SPL_Enemy_Pupsik.3

### Actions
Cam_EndCard_16x9Action, Cam_KeyArt_4x5Action, Cam_KeyArt_9x16Action, Idle, SPL_Enemy_Pupsik.2Action


---

## Bouncer_Marketing/Kids.blend
- Blender 5.1.2; scenes: KidCard_4x5_Huligan, KidCard_4x5_Melkaya, KidCard_4x5_Otlichnik, KidCard_4x5_Tolstyak, KidIntro_9x16_Huligan, KidIntro_9x16_Melkaya, KidIntro_9x16_Otlichnik, KidIntro_9x16_Tolstyak, Trailer_KidIntro_16x9_Huligan, Trailer_KidIntro_16x9_Melkaya, Trailer_KidIntro_16x9_Otlichnik, Trailer_KidIntro_16x9_Tolstyak
- objects 141, meshes 53, armatures 4, actions 16, materials SPL_Palette, M_Decals, M_Palette
- linked libraries: ['//../Bouncer.blend']
- images: T_Decals, T_Palette_Day, T_Palette_Emission

### Scenes
- **KidCard_4x5_Huligan**: frames 60-60 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_KidIntro_Huligan, objects 119, output `//renders/KidCard_4x5_Huligan_`
- **KidCard_4x5_Melkaya**: frames 60-60 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_KidIntro_Melkaya, objects 119, output `//renders/KidCard_4x5_Melkaya_`
- **KidCard_4x5_Otlichnik**: frames 60-60 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_KidIntro_Otlichnik, objects 119, output `//renders/KidCard_4x5_Otlichnik_`
- **KidCard_4x5_Tolstyak**: frames 60-60 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_KidIntro_Tolstyak, objects 119, output `//renders/KidCard_4x5_Tolstyak_`
- **KidIntro_9x16_Huligan**: frames 0-59 @ 30 fps, 1080x1920, engine CYCLES, camera Cam_KidIntro_Huligan, objects 119, output `//renders/KidIntro_9x16_Huligan/`
- **KidIntro_9x16_Melkaya**: frames 0-59 @ 30 fps, 1080x1920, engine CYCLES, camera Cam_KidIntro_Melkaya, objects 119, output `//renders/KidIntro_9x16_Melkaya/`
- **KidIntro_9x16_Otlichnik**: frames 0-59 @ 30 fps, 1080x1920, engine CYCLES, camera Cam_KidIntro_Otlichnik, objects 119, output `//renders/KidIntro_9x16_Otlichnik/`
- **KidIntro_9x16_Tolstyak**: frames 0-59 @ 30 fps, 1080x1920, engine CYCLES, camera Cam_KidIntro_Tolstyak, objects 119, output `//renders/KidIntro_9x16_Tolstyak/`
- **Trailer_KidIntro_16x9_Huligan**: frames 0-41 @ 30 fps, 1920x1080, engine CYCLES, camera Cam_TrailerIntro_Huligan, objects 119, output `//renders/Trailer_KidIntro_16x9_Huligan/`
- **Trailer_KidIntro_16x9_Melkaya**: frames 0-41 @ 30 fps, 1920x1080, engine CYCLES, camera Cam_TrailerIntro_Melkaya, objects 119, output `//renders/Trailer_KidIntro_16x9_Melkaya/`
- **Trailer_KidIntro_16x9_Otlichnik**: frames 0-41 @ 30 fps, 1920x1080, engine CYCLES, camera Cam_TrailerIntro_Otlichnik, objects 119, output `//renders/Trailer_KidIntro_16x9_Otlichnik/`
- **Trailer_KidIntro_16x9_Tolstyak**: frames 0-41 @ 30 fps, 1920x1080, engine CYCLES, camera Cam_TrailerIntro_Tolstyak, objects 119, output `//renders/Trailer_KidIntro_16x9_Tolstyak/`

### Collections (root objects only)
- **Huligan** (5 objects): Focus_Huligan, Kid_Huligan, Look_Huligan, SPL_Prop_Football
- **Melkaya** (5 objects): Focus_Melkaya, Kid_Melkaya, Look_Melkaya, SPL_Prop_JumpRope
- **Otlichnik** (5 objects): Focus_Otlichnik, Kid_Otlichnik, Look_Otlichnik, SPL_Prop_Book
- **Splash** (86 objects): SPL_Backdrop, SPL_Ball_Deflated, SPL_Ball_Medicine, SPL_Ball_Rubber, SPL_Ball_Tennis, SPL_Ball_Volleyball, SPL_Cam, SPL_Cloud.0, SPL_Cloud.1, SPL_Cloud.2, SPL_Cloud.3, SPL_Enemy_RolyPoly.0, SPL_Enemy_RolyPoly.1, SPL_Enemy_TinSoldier.0, SPL_Enemy_TinSoldier.1, SPL_Enemy_TinSoldier.2, SPL_Enemy_TinSoldier.3, SPL_Env_YardGround, SPL_Prop_Bench, SPL_Prop_Bush.0, SPL_Prop_Bush.1, SPL_Prop_Bush.2, SPL_Prop_Clothesline, SPL_Prop_FenceLow.0, SPL_Prop_FenceLow.1, SPL_Prop_FenceLow.2, SPL_Prop_FenceLow.3, SPL_Prop_FenceLow.4, SPL_Prop_Garage_A.0, SPL_Prop_Garage_A.3, SPL_Prop_Garage_B.1, SPL_Prop_Garage_B.4, SPL_Prop_Garage_C.2, SPL_Prop_Hedge.0, SPL_Prop_Hedge.1, SPL_Prop_Hedge.2, SPL_Prop_Hedge.3, SPL_Prop_Hedge.4, SPL_Prop_Hedge.5, SPL_Prop_Hedge.6, SPL_Prop_Panelka_5F.0, SPL_Prop_Panelka_5F.1, SPL_Prop_Panelka_9F.0, SPL_Prop_Panelka_9F.1, SPL_Prop_Rocket, SPL_Prop_Sandbox, SPL_Prop_StreetLamp, SPL_Prop_Swings, SPL_Prop_TrashBin, SPL_Prop_Tree_Birch.0, SPL_Prop_Tree_Birch.3, SPL_Prop_Tree_Birch.5, SPL_Prop_Tree_Poplar.1, SPL_Prop_Tree_Poplar.2, SPL_Prop_Tree_Poplar.4, SPL_Sun
- **Splash_Front** (27 objects): SPL_Boss_BigRolyPoly, SPL_Enemy_Pupsik.0, SPL_Enemy_Pupsik.1, SPL_Enemy_Pupsik.2, SPL_Enemy_Pupsik.3
- **Tolstyak** (5 objects): Focus_Tolstyak, Kid_Tolstyak, Look_Tolstyak, SPL_Prop_Chips

### Actions
Cam_KidIntro_HuliganAction, Cam_KidIntro_MelkayaAction, Cam_KidIntro_OtlichnikAction, Cam_KidIntro_TolstyakAction, Cam_TrailerIntro_HuliganAction, Cam_TrailerIntro_MelkayaAction, Cam_TrailerIntro_OtlichnikAction, Cam_TrailerIntro_TolstyakAction, Pose_Cheer, Pose_Ready, Pose_Think, Pose_Tough, SPL_Prop_BookAction, SPL_Prop_ChipsAction, SPL_Prop_FootballAction, SPL_Prop_JumpRopeAction


---

## Bouncer_Marketing/Bosses.blend
- Blender 5.1.2; scenes: BossPoster_4x5_BigRolyPoly, BossPoster_4x5_Dusk, BossPoster_4x5_Fizruk, BossPoster_4x5_Hare, BossPoster_4x5_Transformer
- objects 178, meshes 84, armatures 0, actions 0, materials SPL_Palette, M_Decals, M_Palette
- linked libraries: ['//../Bouncer.blend']
- images: T_Decals, T_Palette_Day, T_Palette_Emission

### Scenes
- **BossPoster_4x5_BigRolyPoly**: frames 0-0 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_Boss_BigRolyPoly, objects 94, output `//renders/BossPoster_4x5_BigRolyPoly_`
- **BossPoster_4x5_Dusk**: frames 0-0 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_Boss_Dusk, objects 101, output `//renders/BossPoster_4x5_Dusk_`
- **BossPoster_4x5_Fizruk**: frames 0-0 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_Boss_Fizruk, objects 98, output `//renders/BossPoster_4x5_Fizruk_`
- **BossPoster_4x5_Hare**: frames 0-0 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_Boss_Hare, objects 102, output `//renders/BossPoster_4x5_Hare_`
- **BossPoster_4x5_Transformer**: frames 0-0 @ 30 fps, 1440x1800, engine CYCLES, camera Cam_Boss_Transformer, objects 108, output `//renders/BossPoster_4x5_Transformer_`

### Collections (root objects only)
- **BigRolyPoly** (5 objects): Focus_BigRolyPoly, Look_BigRolyPoly, SPL_Boss_BigRolyPoly.001
- **BossLights** (2 objects): SOC_Fill, SOC_Rim
- **Dusk** (12 objects): Focus_Dusk, Look_Dusk, SPL_Boss_Dusk
- **Fizruk** (9 objects): Focus_Fizruk, Look_Fizruk, SPL_Boss_Fizruk
- **Hare** (13 objects): Focus_Hare, Look_Hare, SPL_Boss_Hare
- **Splash** (86 objects): SPL_Backdrop, SPL_Ball_Deflated, SPL_Ball_Medicine, SPL_Ball_Rubber, SPL_Ball_Tennis, SPL_Ball_Volleyball, SPL_Cam, SPL_Cloud.0, SPL_Cloud.1, SPL_Cloud.2, SPL_Cloud.3, SPL_Enemy_RolyPoly.0, SPL_Enemy_RolyPoly.1, SPL_Enemy_TinSoldier.0, SPL_Enemy_TinSoldier.1, SPL_Enemy_TinSoldier.2, SPL_Enemy_TinSoldier.3, SPL_Env_YardGround, SPL_Prop_Bench, SPL_Prop_Bush.0, SPL_Prop_Bush.1, SPL_Prop_Bush.2, SPL_Prop_Clothesline, SPL_Prop_FenceLow.0, SPL_Prop_FenceLow.1, SPL_Prop_FenceLow.2, SPL_Prop_FenceLow.3, SPL_Prop_FenceLow.4, SPL_Prop_Garage_A.0, SPL_Prop_Garage_A.3, SPL_Prop_Garage_B.1, SPL_Prop_Garage_B.4, SPL_Prop_Garage_C.2, SPL_Prop_Hedge.0, SPL_Prop_Hedge.1, SPL_Prop_Hedge.2, SPL_Prop_Hedge.3, SPL_Prop_Hedge.4, SPL_Prop_Hedge.5, SPL_Prop_Hedge.6, SPL_Prop_Panelka_5F.0, SPL_Prop_Panelka_5F.1, SPL_Prop_Panelka_9F.0, SPL_Prop_Panelka_9F.1, SPL_Prop_Rocket, SPL_Prop_Sandbox, SPL_Prop_StreetLamp, SPL_Prop_Swings, SPL_Prop_TrashBin, SPL_Prop_Tree_Birch.0, SPL_Prop_Tree_Birch.3, SPL_Prop_Tree_Birch.5, SPL_Prop_Tree_Poplar.1, SPL_Prop_Tree_Poplar.2, SPL_Prop_Tree_Poplar.4, SPL_Sun
- **Transformer** (19 objects): Focus_Transformer, Look_Transformer, SPL_Boss_Transformer


---

Пересборка маркетинговых файлов: `blender/Bouncer_Marketing/scripts/build_marketing.py` (`Blender -b --factory-startup --python scripts/build_marketing.py -- all|KeyArt|Kids|Bosses`; перезаписывает файл, поэтому новые шоты добавлять в скрипт или в новый файл).
