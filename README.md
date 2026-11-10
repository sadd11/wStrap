<picture>
  <source media="(prefers-color-scheme: dark)" srcset=".github/assets/wstrap-dark.png">
  <img src=".github/assets/wstrap-light.png" alt="wStrap">
</picture>

# wStrap

Альтернативный бутстраппер Roblox — форк [Fishstrap](https://github.com/fishstrap/fishstrap) (который форк [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap)) с тем, чего мне не хватало: оверлей с живым пингом, профили настроек и статистика наигранных часов.

## Возможности

- **Оверлей поверх игры** — круглая пилюля: пинг до сервера в реальном времени, таймер сессии, цветовой индикатор качества связи. Клик — меню с rejoin, скриншотом и инвайтом
- **Профили настроек** — сохранённые наборы FastFlags и темы, переключение в один клик + мастер настройки
- **Статистика** — часы по дням, неделям и месяцам, тепловая карта активности, рекорды сессий, стрик и локальные достижения
- **Скриншоты** — кадр игры в буфер обмена и в папку «Изображения\wStrap» прямо из оверлея
- **Друзья онлайн** — список друзей из трея, джойн к другу в игре одной кнопкой
- **FastFlags по-русски** — тултипы с описанием популярных флагов, сброс всех флагов, мастер настройки
- **Высокий приоритет процесса** — запуск Roblox с приоритетом High
- **Режим стримера** — скрывает userId и аккаунт в Rich Presence
- **Мелочи** — удаление логов после сессии, уведомление об обновлении Roblox, звук при входе, счётчик запусков

Плюс всё из Fishstrap: FastFlags, моды, кастомные темы, Discord Rich Presence, смена канала.

## Установка

1. Скачай `wStrap.exe` со страницы [Releases](https://github.com/sadd11/wStrap/releases)
2. Запусти и следуй мастеру установки
3. Готово — ярлыки и протоколы Roblox настроятся автоматически

> Если установлены Fishstrap или Bloxstrap — лучше сначала удалить их, чтобы не конфликтовали ярлыками.

## Сборка из исходников

- [.NET SDK 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)
- Git

```bash
git clone --recursive https://github.com/sadd11/wStrap.git
cd wStrap
dotnet build Fishstrap.sln
```

Готовая сборка лежит в `Bloxstrap/bin/Debug/net6.0-windows/`.

## Благодарности

- [Fishstrap](https://github.com/fishstrap/fishstrap) — база этого форка
- [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) — оригинальный проект
- [WPF UI](https://github.com/lepoco/wpfui) — библиотека интерфейса

## Отказ от ответственности

wStrap не связан с Roblox Corporation. Использование стороннего ПО — на твой страх и риск.
