# Чек-лист выпуска версии

## Выбранные платформы

| Платформа | Формат | Где публикуется | Почему |
|---|---|---|---|
| Windows (x64) | .exe (папка) | itch.io, GitHub Releases | основная аудитория ПК, простое распространение |
| Браузер (WebGL) | папка index.html | itch.io (HTML5), Яндекс Игры | запуск без установки — ниже порог входа |
| Android | .apk (тест) / .aab (магазин) | RuStore, Google Play | мобильная аудитория, есть сенсорное управление |

## 1. Перед сборкой
- [ ] Все автотесты проходят: Window → General → Test Runner → EditMode и PlayMode → Run All
- [ ] Ручной чек-лист из кейс-задачи № 4 пройден (Docs/Case4_Testing_Optimization.md)
- [ ] В консоли нет ошибок и предупреждений от собственных скриптов
- [ ] Версия повышена: Tools → Release → «Повысить версию» или Player Settings → Version (формат major.minor.patch)
- [ ] Обновлены `Assets/Resources/changelog.txt` (окно «Что нового») и `Docs/Release/CHANGELOG.md`
- [ ] Проверены значения `Config/remote_config.json` (баланс по умолчанию, новости, latestVersion)

## 2. Сборка
- [ ] Tools → Release → Build Windows (x64)
- [ ] Tools → Release → Build WebGL
- [ ] Tools → Release → Build Android (APK) — для теста на телефоне; AAB — для Google Play
- [ ] Для Google Play: Player Settings → Publishing Settings → собственный keystore (хранить вне репозитория!)
- [ ] Проверить `Builds/<платформа>/build_report.txt`: результат Succeeded, 0 ошибок, размер не вырос неожиданно
- [ ] Альтернатива: тег `vX.Y.Z` → GitHub Actions соберёт все платформы и создаст Release (нужны секреты Unity)

## 3. Проверка сборок
- [ ] Windows: запуск на чистой машине, полноэкранный и оконный режим, выход по кнопке
- [ ] WebGL: локальный сервер или черновик itch.io, загрузка < 15 с, кнопки «Выход» нет
- [ ] Android: установка APK, сенсорное управление, вырез экрана (Safe Area), сворачивание/разворачивание
- [ ] Окно согласия на статистику появляется при первом запуске; при отказе события не пишутся
- [ ] Отзыв из паузы открывает GitHub Issues с заполненным текстом
- [ ] В главном меню видна правильная версия и номер сборки

## 4. Публикация
- [ ] Загружены сборки, скриншоты, описание (Docs/Release/STORE_LISTING.md)
- [ ] Указана ссылка на политику конфиденциальности (Docs/Release/PRIVACY_POLICY.md)
- [ ] Возрастной рейтинг заполнен (насилия нет, «взрывы» мультяшные — 6+ / Everyone)
- [ ] Создан GitHub Release с архивами и списком изменений
- [ ] В `Config/remote_config.json` обновлены `latestVersion`, `updateUrl`, новость о выходе версии

## 5. После выпуска (первые 7 дней)
- [ ] Ежедневно: новые обращения в GitHub Issues (метки bug / feedback / enhancement), отзывы в магазинах
- [ ] Через 2–3 дня: панель Tools → Release → Analytics Dashboard по присланным/выгруженным данным
- [ ] Критические ошибки → исправление → patch-версия (X.Y.Z+1)
- [ ] Проблемы баланса → правка `Config/remote_config.json` без выпуска новой версии
- [ ] Итог недели → план следующего обновления (Docs/Release/LIVEOPS.md, раздел «Цикл обновления»)
