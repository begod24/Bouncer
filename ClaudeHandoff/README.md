# ClaudeHandoff — перенос проекта «Bouncer / It's our FIELD» на другой компьютер

Собрано 2026-10-08. Цель: локальный Claude на новом компьютере понимает, на каком этапе игра и что происходит.

## Что внутри
| Путь | Что это |
|---|---|
| `PROJECT_MEMORY.md` | **Главный файл памяти**: что за игра, этап разработки, ближайшие шаги, правила работы с автором, дизайн, архитектура, пайплайны, маркетинг, карта заметок |
| `CLAUDE.md` | Короткие правила + ссылка на главный файл. Класть в корень репозитория, Claude Code подхватывает его сам |
| `memory_full/` | 35 подробных заметок памяти Claude дословно + `MEMORY.md` (индекс). Это та самая автопамять проекта |
| `docs/dev_plan.md` | План разработки по этапам (был в `~/.claude/plans/`) |
| `docs/Vyshibaly_Concept.docx` | Концепт-документ (GDD) |
| `blender/Bouncer.blend` | Главный блендер с ассетами (последнее сохранённое состояние, 2026-10-07 15:56) |
| `blender/Bouncer_Marketing/` | Блендеры для контента (KeyArt, Kids, Bosses) + `scripts/build_marketing.py` |
| `blender/text_blocks/` | Все скрипты-генераторы из Bouncer.blend как обычные файлы + `README_assets.txt` |
| `blender/BLEND_INVENTORY.md` | Опись всех Blender-файлов: сцены, коллекции, объекты, клипы, текстблоки |
| `marketing/` | README и скрипты трейлера, план и скрипты соцсетей (видео и рендеры не включены — большие) |

Репозиторий с Unity-проектом (код, сцены, префабы, арт) сюда не копируется: он едет через git (`git@github.com:begod24/Bouncer.git`). Эта папка лежит в корне репозитория и **не закоммичена** — автор сам решает, коммитить ли её (`*.blend` уйдёт в LFS) или перенести флешкой/AirDrop.

## Как поднять на новом компьютере
1. Клонируй репо: `git clone git@github.com:begod24/Bouncer.git`, затем `git lfs install && git lfs pull`. Нужен Unity **6000.3.24f1** (с модулями под нужные платформы) и Git LFS.
2. Положи эту папку (`ClaudeHandoff`) в корень клона, если её там ещё нет.
3. Скопируй `ClaudeHandoff/CLAUDE.md` в корень репозитория (рядом с `Assets/`).
4. **Память Claude.** Два варианта (можно оба):
   - Достаточно `CLAUDE.md` + `PROJECT_MEMORY.md`: Claude Code читает главный файл по ссылке `@ClaudeHandoff/PROJECT_MEMORY.md`.
   - Чтобы заработала автопамять: открой проект в Claude Code один раз (создастся папка `~/.claude/projects/<путь-проекта-со-слэшами-вместо-дефисов>/memory/`; например для `/Users/me/dev/Bouncer` это `-Users-me-dev-Bouncer`), затем скопируй туда всё из `memory_full/` (включая `MEMORY.md`). Имя папки проверяй командой `ls ~/.claude/projects/`.
5. **Blender 5.1+.** Положи `blender/Bouncer.blend` и папку `blender/Bouncer_Marketing/` в одну общую папку (например, `~/Desktop/projects/`): маркетинговые файлы линкуют ассеты по относительному пути `../Bouncer.blend`.
6. В Bouncer.blend открой Text Editor → блоки `ba_export.py` и `ba_lib.py` и замени строку `REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer"` на путь к клону. Без этого экспорт FBX/палитр пойдёт по старому пути.
7. Подключи MCP: Unity MCP (пакет `com.unity.ai.assistant` уже в manifest) и Blender MCP-аддон.
8. Для трейлерных скриптов: `Assets/_Trailer/TrailerRecorder.cs` содержит путь к ffmpeg от старой машины — поправь на свой (`pip install imageio-ffmpeg` даёт бинарник).
9. Python для `Tools/*.py`: Pillow, numpy (для расчёта лупов музыки ещё scipy).
10. Попроси новый Claude: «Прочитай ClaudeHandoff/PROJECT_MEMORY.md и скажи, на каком мы этапе» — проверка, что всё подхватилось.

## Чего здесь нет (осталось на старом компьютере)
- Видео/рендеры: `~/Desktop/projects/ItsOurField_Trailer/` (mp4 и клипы ≈230 МБ), `ItsOurField_Social/` (фото, рилсы, `src/`), `ItsOurField_Screenshots/`, `MAC builds/`.
- Референс-лист детей `Bouncer_Kids_Sheet.webp` (в памяти упомянут, на диске 2026-10-08 не найден).
- Бэкап `Bouncer.blend1` и независимые проекты (Office, Kazakh Fruit Ninja, The-Office).
