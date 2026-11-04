# Запуск и публикация GuessWord

Все команды выполняются из корня проекта, если не указано иное.
Для разработки нужны .NET SDK из [`global.json`](../../global.json), PostgreSQL с pgvector
и внешний файл векторов. Python 3 нужен для проверочных скриптов, nginx — для опубликованного
клиента с общим адресом API. Скрипты подготовки словаря не требуются для обычного запуска.

## БД, словарь и настройки

Создайте отдельную БД и пользователя с правами на её схему и миграции. Установите pgvector
на сервер PostgreSQL и включите `CREATE EXTENSION IF NOT EXISTS vector;` в выбранной БД
(см. [`init.sql`](../../deploy/postgres/init.sql)). API применяет миграции при старте. Перед обновлением
существующей БД сохраните резервную копию и проверьте восстановление и миграции на отдельной БД.
Не запускайте `reset_game_dictionary_data.py` на рабочей базе.

Поместите векторы размерности 800 в `src/GuessWord.Api/Resources/Data/sociation2vec800.vec`
или задайте абсолютный путь через `Dictionary__SociationVecPath`. Файл не входит в Git
и publish-пакет. Списки слов из `Resources/*.txt` поставляются с API.
Пустой словарь импортируется атомарно; при наличии слов импорт пропускается.
Неполный словарь автоматически не заменяется и не проходит проверку готовности.
В файле `.vec` должны присутствовать все слова из поставляемого `all-words.txt`;
заголовок содержит число векторов и размерность. Ссылки для загрузки модели в проекте нет:
файл нужно получить отдельно. При ошибке полного импорта транзакция откатывается.

Создайте `src/GuessWord.Api/appsettings.Development.json` по соседнему
[`appsettings.Development.example.json`](../../src/GuessWord.Api/appsettings.Development.example.json),
не перезаписывая существующий файл.
Укажите подключение к БД и случайный JWT-ключ не короче 32 символов; на Linux задайте файлу
права `600`. Серверные настройки секретны, JSON клиента содержит только публичный адрес API.

| Настройка | Переменная окружения | Назначение |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Подключение API к подготовленной БД |
| `Jwt:Key` | `Jwt__Key` | Ключ подписи токенов; замените значение из примера |
| `Dictionary:SociationVecPath` | `Dictionary__SociationVecPath` | Внешний `.vec`; по умолчанию `Resources/Data/sociation2vec800.vec` |
| `Dictionary:AllWordsPath` | `Dictionary__AllWordsPath` | Полный список слов; по умолчанию `Resources/all-words.txt` |
| `Dictionary:SecretWordsPath` | `Dictionary__SecretWordsPath` | Слова для загадывания; по умолчанию `Resources/secret-words.txt` |
| `Api:BaseUrl` (Client) | — | Публичный адрес API в `wwwroot/appsettings*.json`; пустой — origin сайта |

Относительные пути словаря разрешаются от content root API. Для внешних файлов удобнее
абсолютные пути. Параметры `AllWordsPath`/`SecretWordsPath` обычно менять не нужно;
проверка готовности сверяет БД именно с этими списками.
Статический Blazor-клиент читает свой JSON; переменные окружения API его не настраивают.

## Разработка

```sh
dotnet restore GuessWord.slnx
dotnet build GuessWord.slnx -c Release --no-restore
dotnet run --project src/GuessWord.Api -c Release --no-build --launch-profile http
# В другом терминале:
dotnet run --project src/GuessWord.Client -c Release --no-build --launch-profile http
```

Клиент — `http://localhost:5101`, API — `http://localhost:5280`, Swagger — `/swagger` на API
только в Development. Профили `http` устанавливают это окружение даже при сборке Release.
Development JSON клиента задаёт общий `Api:BaseUrl` для HTTP и SignalR.
Для команд EF Core сначала выполните `dotnet tool restore`.

После запуска откройте клиент, зарегистрируйтесь и отправьте слово в одиночной игре.
Для сетевой игры войдите вторым аккаунтом в отдельном браузерном профиле, присоединитесь
по коду комнаты и запустите раунд от имени хозяина.

Проверить готовность можно без аккаунта:

```sh
curl --fail http://localhost:5280/health/live
curl --fail http://localhost:5280/health/ready
```

Ожидаются HTTP 200 и статусы `live`/`ready`. Пока идут миграции и первый импорт,
HTTP ещё недоступен. Если `/ready` вернул 503, проверьте подключение к БД, списки слов
и векторы; `/live` подтверждает только то, что процесс принимает запросы.

## Локальный релиз: общий адрес

```sh
dotnet publish src/GuessWord.Api -c Release -o artifacts/release/api
dotnet publish src/GuessWord.Client -c Release -o artifacts/release/client
python tools/testing/check_release.py
```

В publish нет Development JSON; пустой `Api:BaseUrl` клиента означает origin сайта.
`check_release.py` проверяет состав пакета и записывает `artifacts/release/SHA256SUMS`.
Он не переносит БД и не заменяет проверку игры. Тестовые проекты не входят в публичное
решение; успешная сборка `GuessWord.slnx` сама по себе не означает прохождения тестов.
Для API задайте через защищённое окружение `ConnectionStrings__DefaultConnection`, `Jwt__Key`
и `Dictionary__SociationVecPath` (абсолютный путь). Не сохраняйте реальные секреты в истории команд.

Из каталога публикации, с `ASPNETCORE_ENVIRONMENT=Production`:

```sh
cd artifacts/release/api
dotnet GuessWord.Api.dll --urls http://127.0.0.1:5280
```

Для nginx используйте копии `deploy/nginx/nginx.conf`, `security.conf` и `locations.conf`:
настройте абсолютные include-пути, root на `artifacts/release/client/wwwroot`, upstream
с `api:8080` на `127.0.0.1:5280`, listen на `127.0.0.1:8088`. Подключите стандартный
`mime.types` с JS, CSS и `application/wasm`. Исходные конфигурации предназначены для Compose.

`/health/live` проверяет процесс; `/health/ready` — доступность БД, точный состав словаря
и секретных слов, размерность и ненулевую норму векторов (200 либо 503).
API начинает слушать после миграций и импорта. Проверка готовности не заменяет проверку игры.

## Локальный Docker Compose

Подготовьте файл векторов по стандартному пути и локальный `.env` по `.env.example`:
задайте `POSTGRES_PASSWORD` и `JWT_KEY`. Сначала опубликуйте клиент в `artifacts/release/client`.

```sh
docker compose --env-file .env -f deploy/docker/compose.local.yml config -q
docker compose --env-file .env -f deploy/docker/compose.local.yml up --build
```

Сайт — `http://localhost:8088` (`GUESSWORD_LOCAL_PORT` меняет порт).
БД и API не публикуют порты хоста; Compose ждёт готовности БД, векторы монтируются read-only.
Имя проекта — `guessword-local-release`, ключ volume в Compose — `local-release-data`.
Для остановки используйте ту же команду Compose с `stop`; `down -v` удаляет данные.
Контейнерная сборка и запуск требуют отдельной проверки: ранее был проверен только синтаксис Compose.

## Конфигурация публичного сервера

`deploy/docker/docker-compose.server.yml` использует отдельный проект `guess-word` и volume `pgdata`.
В отличие от локального варианта, он ожидает клиент в `publish/client/wwwroot`:

```sh
dotnet publish src/GuessWord.Client -c Release -o publish/client
```

В защищённом `.env` задаются `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `JWT_KEY`
и `GUESSWORD_TLS_DIRECTORY` — абсолютный путь к каталогу сертификата вне репозитория.
Нужны `fullchain.pem` и `privkey.pem` для реального домена; без них nginx не запустится.
`nginx.server.conf` перенаправляет HTTP на HTTPS и обслуживает сайт на 443.
Выдача/обновление сертификата и перенос БД требуют настройки конкретного сервера.

Оба варианта nginx используют общие `security.conf` и `locations.conf`:
журналы не сохраняют query string, error log хаба отключён для защиты токена.
Login/register ограничены общим лимитом 10 запросов/мин на IP с burst=10,
`/health/ready` — 1 запрос/с с burst=2; превышение возвращает 429.
БД/API не открываются наружу; при внешнем прокси отдельно настройте доверенные адреса.
Проверка конфигураций установленным nginx с поддержкой SSL/rewrite:

```sh
python tools/testing/check_proxy.py --nginx /absolute/path/to/nginx
```

Скрипт использует временный сертификат и вымышленные данные, проверяет HTTPS, лимиты и журналы.
Это не проверка контейнерного развёртывания или доверия публичному сертификату.

Порядок запуска на сервере, резервирования и отката — в [инструкции развёртывания](deployment.md).

## Если запуск не удался

| Симптом | Что проверить |
| --- | --- |
| API завершается до открытия порта | Доступ к БД, права миграций, наличие расширения `vector` и полного `.vec` при пустом словаре |
| `/health/live` — 200, `/health/ready` — 503 | Доступность БД, точное совпадение слов и признаков секрета со списками, наличие ненулевых vector(800) |
| Клиент открыт, запросы API не проходят | `Api:BaseUrl` в Development; в публикации — проксирование `/api`, `/health` и `/hubs/game` одним nginx |
| Compose не принимает `.env` или nginx не запускается | Обязательные переменные, пути файлов/каталогов и наличие обоих TLS-файлов в серверном варианте |
