# Пересобирает все ассеты Bouncer из скриптов в этом файле (Text Editor -> Run Script).
import bpy, sys
MODULES = ("ba_lib", "ba_enemies", "ba_balls", "ba_yard", "ba_pickups", "ba_shop", "ba_toys", "ba_elites",
           "ba_dusk", "ba_bosses", "ba_dusk_elites", "ba_final", "ba_rink", "ba_site", "ba_kids", "ba_kids_anim",
           "ba_toys2", "ba_bosses2", "ba_bazaar", "ba_kindergarten", "ba_abilities", "ba_export")
for n in MODULES:
    sys.modules[n] = bpy.data.texts[n + ".py"].as_module()
import ba_abilities
import ba_lib, ba_enemies, ba_balls, ba_yard, ba_pickups, ba_shop, ba_toys, ba_elites, ba_dusk, ba_bosses, ba_dusk_elites, ba_final, ba_rink, ba_site, ba_kids, ba_kids_anim, ba_toys2, ba_bosses2, ba_bazaar, ba_kindergarten
ba_lib.build_palettes()
ba_lib.get_material()
ba_lib.get_decal_material()
ba_enemies.build_all(None)
ba_balls.build_all(None)
ba_yard.build_all(None)
ba_pickups.build_all(None)
ba_shop.build_all(None)
ba_toys.build_all(None)
ba_elites.build_all(None)      # после ba_enemies и ba_toys: элитные собираются из них
ba_dusk.build_all(None)
ba_bosses.build_all(None)      # после ba_dusk: боссы — это манекен и чучело в большом масштабе
ba_dusk_elites.build_all(None) # элитки стройки: те же манекен, чучело и тень в своём облике
ba_final.build_all(None)       # финал: «Тот, кто в сумерках» (Бабай с мешком) и ворона из его стаи
ba_rink.build_all(None)
ba_site.build_all(None)
ba_kids.build_all(None)        # дети: скелет Humanoid + один меш, каждая часть привязана к своей кости
ba_kids.build_props(None)      # реквизит для поз на экране выбора (книга, чипсы, скакалка, мяч)
ba_kids_anim.build(None)       # Anim_Kids: эталонный скелет со всеми клипами детей
ba_toys2.build_all(None)       # этап 3: солдатик с крышкой, барабанщик, лягушка, балерина, луноход, машинка, «Денди», плакса
ba_bosses2.build_all(None)     # этап 3: Трансформер («Жигули» / робот, две позы) и Большой плюшевый заяц
ba_bazaar.build_all(None)      # этап 3: Барахолка (прилавки, ларьки, контейнеры, тележки, коробки, гирлянды, ворота)
ba_kindergarten.build_all(None)  # этап 3: Детсад (здание, забор, веранды, карусель, ракета, домик, покрышки, фонари)
ba_abilities.build_all(None)    # карточки 2026-10: баскетбольный, пинг-понг, футбольный мячи и предметы умений
print("Bouncer assets rebuilt")

# Экспорт в Unity (FBX + палитры PNG) — раскомментируй. В заголовке FBX есть время создания, поэтому
# export_all() меняет в git ВСЕ модели. Обычно экспортируй только изменённые:
# import ba_export; ba_export.export_named(["Prop_Kiosk", "Pickup_Coin_1"]); ba_export.export_palettes()
# import ba_export; ba_export.export_all()
# Дети (скелет, свои настройки FBX): import ba_kids, ba_kids_anim; ba_kids.export_kids(); ba_kids_anim.export()
# Трансформер (робот + поза машины): import ba_bosses2; ba_bosses2.export_transformer()
