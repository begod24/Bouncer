# It's Our Field: трейлер «Мама зовёт домой»

Две версии, обе 1920×1080, 60 fps, H.264 + AAC:
- `ItsOurField_Trailer_v1_no_mom.mp4` (39 с): музыка из игры `SFX_Music4` (106.67 BPM).
- `ItsOurField_Trailer_v2_SpaceCadet.mp4` (35,8 с): «Space Cadet Training Montage» от Zane Little Music, лицензия CC0 ([opengameart.org/node/138918](https://opengameart.org/node/138918), подробности в `music/LICENSE.txt`), 130 BPM. Трек склеен по фразам: вступление, дроп и первая фраза, затем кульминация и концовка. Финальный удар трека совпадает со сменой кадра на лого.

В обеих версиях убран игровой звук «мама зовёт» в финальной сцене.

## Что в ролике (тайминг v1 / v2)
| v1 | v2 | Кадр | Откуда |
|---|---|---|---|
| 0:00 | 0:00 | Заставка Neon Yurt: неон загорается с мерцанием и гулом | NeonYurtBanner.png + `tools/splash.py` |
| 0:02.6 | 0:02.6 | Двор на закате, мяч катится, Хулиган подбирает. «— Саша, домой!» | Unity, Yard |
| 0:06.1 | 0:06.1 | Четверо спиной к нам, появляются игрушки. «— Ещё 5 минуточек!» | Unity, Yard |
| 0:08.6 | 0:08.6 | ДРОП музыки: Отличник, Толстяк, Мелкая, Хулиган с именами | Blender, сцена Splash |
| 0:13.1 | 0:14.1 | КИДАЙ! / ЛОВИ! / ВЫЖИВАЙ! / ВМЕСТЕ: 1–4 ИГРОКА | Unity |
| 0:22.1 | 0:21.5 | НОВЫЕ ДВОРЫ: каток, барахолка, стройка (огненная лошадка), детсад | Unity |
| 0:24.3 | 0:23.4 | Боссы: деление матрёшки (только v1), таран Трансформера, Бабай «Тот, кто в сумерках», замедленная ловля | Unity |
| 0:31.1 | 0:28.0 | «МАМА ЗОВЁТ ДОМОЙ!»: дети бегут к подъезду | Unity, финальная арена |
| 0:35.0 | 0:31.5 | Дети перед армией игрушек, лого IT'S OUR FIELD, «СКОРО / COMING SOON» | Blender, сцена Splash |

Склейки стоят на долях трека. Звуки собраны из звуковых событий, записанных во время съёмки, и WAV-файлов игры (`SoundBank`), с панорамой по положению камеры.

## Как пересобрать
- `tools/edit.py`: монтаж (кадры и точки входа в `build_segments()`, титры, звук, план музыки в `MUSICS`). Нужны Pillow, numpy, imageio-ffmpeg (`pip install pillow numpy imageio-ffmpeg`). Запуск из этой папки:
  - `TRAILER_ROOT=. python tools/edit.py clips ItsOurField_Trailer_v1_no_mom.mp4 --music=game`
  - `TRAILER_ROOT=. python tools/edit.py clips ItsOurField_Trailer_v2_SpaceCadet.mp4 --music=spacecadet`
  - флаг `--fast` даёт быстрый черновик.
  Рендеры Blender лежат упакованными в `blender/kids/*.mp4` и `blender/endcard.mp4`, заставка в `out/00_splash.*`, трек v2 в `music/`.
- `tools/chalk.py`: меловые титры (шрифты игры Neucha и Caveat).
- `tools/splash.py`: заставка студии.
- `tools/blender/kids_shot.py` и `tools/blender/endcard.py`: рендеры Blender. Запуск: `Blender -b Bouncer.blend --python <script> -- anim`. Файл .blend скрипты не сохраняют.
- Съёмка в Unity: папка `Assets/_Trailer` в проекте. Сборка `Bouncer.Trailer` компилируется только в редакторе и в билд игры не попадает. В Play Mode: `Bouncer.Trailer.TrailerDirector.Run("open,throw,...")`. Клипы и логи звуков пишутся в `TrailerRecorder.OutDir`.
- `clips/`: исходные клипы Unity (mp4 + `.sounds.tsv`).
