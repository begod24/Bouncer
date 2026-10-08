# Пересобирает сцены Splash (сплэш-скрин) и Icon (иконка приложения) из ассетов этого файла
# (Text Editor -> Run Script). После build_all.py запусти ещё раз: старые копии держат старые меши.
import bpy, sys
for n in ("ba_lib", "ba_branding"):
    sys.modules[n] = bpy.data.texts[n + ".py"].as_module()
import ba_branding
ba_branding.build()
ba_branding.build_icon()
print("Splash and Icon scenes rebuilt")

# Рендер в Cycles — раскомментируй; пути относительно .blend:
# ba_branding.render("//Splash_render.png")                                  # 3840x2160, ~1 мин
# ba_branding.render("//Icon_render.png", scene=ba_branding.ICON_SCENE)     # 2048x2048, прозрачный фон
# Потом из корня репозитория: python3 Tools/branding_art.py splash|icon <путь к рендеру>
