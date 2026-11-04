# Архитектура GuessWord

## Проекты

- `src/GuessWord.Api` — ASP.NET Core API и SignalR; только этот проект обращается к БД.
- `src/GuessWord.Client` — Blazor WebAssembly: интерфейс, HTTP, SignalR и localStorage.
- `src/GuessWord.Contracts` — общие DTO и перечисления без зависимости от API или EF Core.

Клиент обращается к API по HTTP и получает уведомления через `/hubs/game`.
PostgreSQL хранит состояние; после изменения данных SignalR уведомляет участников.
Векторные представления слов хранятся в pgvector, размерность — 800.
Зависимости направлены из API и Client в Contracts; прямой зависимости клиента от API нет.

## Словарь и ранги

`Resources/all-words.txt` задаёт допустимые слова, `secret-words.txt` — подмножество
для загадывания. При пустой таблице `Words` API загружает их векторы из внешнего `.vec`
в одной транзакции. Существующий словарь автоматически не заменяется.

`RankService` читает векторы из БД, вычисляет косинусную близость в C# и сортирует слова
от наиболее близкого к секрету. Позиция в списке с нуля — ранг; это не процент сходства.
Ранжирование кешируется в памяти API на три часа для каждого секрета.
Совпадение ответа с секретом сервер проверяет по ID слова.

`WordNormalizer` убирает пробелы по краям, приводит слово к нижнему регистру и заменяет
`ё` на `е`. Повтор принятого слова не увеличивает счётчик; слово вне словаря возвращается
как невалидная попытка и не сохраняется в истории попыток.

## Сервер

Код сгруппирован по функциям в `Features`: авторизация, пользователи, рейтинг,
словарь, игры, комнаты, история и проверка готовности.

В `Features/Games` сервис `GameService` объединяет команды одиночной и сетевой игры,
запросы состояния и формирование ответов. `GuessHandler` обрабатывает попытки,
`GameMutation` управляет транзакциями и блокировками, `GameDataAccess` — доступом к
игровым данным, `GameResponseBuilder` — составом ответов для конкретного пользователя.
`ApiExceptionHandler` возвращает безопасные ошибки в формате ProblemDetails.

Сущности находятся в `Domain/Entities`, контекст и миграции — в `Infrastructure/Persistence`,
JWT — в `Infrastructure/Security`, хаб — в `Realtime`.

- `RoomMember` хранит текущий состав комнаты, `GamePlayer` — историческое участие.
- `ActiveGameParticipation` ограничивает пользователя одним активным матчем.
- Сервер проверяет участие, вместимость комнаты и победителя; блокировки берутся
  в порядке Room → Game → Users.
- Ответ содержит собственные попытки и сводки соперников. Секрет открывается после
  завершения раунда, а сдавшемуся — сразу, пока остальные продолжают играть.
- Разрыв соединения не означает выход. Повторная подписка восстанавливает снимок из БД.

Команды игры выполняются по HTTP. Хаб передаёт уведомления `RoomUpdated`,
`MultiplayerGameStarted` и `GameUpdated`; клиент после них запрашивает состояние API.
Членство проверяется по БД при подписке на группы хаба. После переподключения клиент
повторяет подписку и загружает снимок, поэтому состояние не зависит от памяти SignalR.

## HTTP и SignalR

Ниже — основные маршруты. Полные формы запросов и ответов доступны в Swagger
на `/swagger` в Development и в DTO проекта Contracts.

| Назначение | Маршруты | Доступ |
| --- | --- | --- |
| Регистрация и вход | `POST /api/User/register`, `POST /api/User/login` | Без JWT |
| Профиль и рейтинг | `GET /api/User/profile`, `GET /api/User/leaderboard` | Профиль — JWT; рейтинг — публичный |
| Одиночная игра | `POST /api/Game/single/start`, `GET /api/Game/single/current`, `POST /api/Game/single/guess` | JWT |
| Сдача в одиночной игре | `POST /api/Game/single/{gameId}/giveup` | JWT |
| Комнаты | `POST /api/room/create`, `POST /api/room/join`, `GET /api/room/current`, `GET /api/room/{code}`, `POST /api/room/{code}/leave` | JWT |
| Сетевой раунд | `POST /api/Game/multiplayer/start/{roomCode}`, `POST /api/Game/multiplayer/guess`, `GET /api/games/{gameId}` | JWT |
| Сдача в сетевой игре | `POST /api/games/{gameId}/multiplayer/give-up` | JWT |
| Текущая игра и история | `GET /api/Game/active`, `GET /api/Game/history` | JWT |
| Словарь | `GET /api/Dictionary/exists?word=...`, `GET /api/Dictionary/random-secret` | Публичный |
| Состояние сервиса | `GET /health/live`, `GET /health/ready` | Публичный |
| Уведомления | `/hubs/game` | JWT и проверка участия |

HTTP использует `Authorization: Bearer <token>`. Query-параметр `access_token`
обрабатывается аутентификацией только для пути хаба.
Игровые исключения преобразуются в ProblemDetails с кодом ошибки; некоторые
контроллеры также возвращают строку или пустой ответ при ошибке.

## Клиент

Страницы находятся в `Features`, общие игровые элементы — в `Components/Game`,
каркас и навигация — в `Components/Layout`, диалог — в `Components/UI`.
Код страниц вынесен в `.razor.cs`, стили компонентов — в `.razor.css`.
Общие стили и ресурсы находятся в `wwwroot`.

`Infrastructure/Http` содержит клиентские сервисы, `Infrastructure/Realtime` —
подключение и подписки SignalR, `Infrastructure/Storage` — работу с localStorage.
`State/UserStateService` хранит клиентское состояние пользователя.
Настройка `Api:BaseUrl` общая для HTTP и SignalR; в публикации используется origin сайта.

## Запуск и эксплуатация

`deploy/` содержит Dockerfile, два варианта Compose и настройки nginx/pgvector.
`tools/dictionary/` — подготовку словаря, `tools/testing/` — проверки пакета и прокси.
Команды и требования приведены в [инструкции запуска](../operations/local-run.md).
