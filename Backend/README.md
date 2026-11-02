# MeetSpace — Backend

Backend системы бронирования переговорных комнат на ASP.NET Core.

## Технологии

- .NET 10
- ASP.NET Core Web API

> Стек будет расширяться в последующих тикетах (EF Core/Npgsql,
> JWT, S3/MinIO, AutoMapper, Swagger и др.).

## Структура

```
Backend/
├── MeetSpace.slnx
└── src/
    ├── MeetSpace.Api/              # точка входа Web API
    ├── MeetSpace.Domain/           # сущности, перечисления, правила
    ├── MeetSpace.Application/      # бизнес-логика, абстракции
    └── MeetSpace.Infrastructure/   # внешние сервисы (JWT, S3, BCrypt)
```

## Требования

- Установленный .NET 10 SDK (`dotnet --version`).

## Сборка

Из каталога `Backend/`:

```bash
dotnet restore
dotnet build
```

Или одной командой для конкретного проекта:

```bash
dotnet build src/MeetSpace.Api/MeetSpace.Api.csproj
```

## Запуск

Из каталога `Backend/`:

```bash
dotnet run --project src/MeetSpace.Api
```

По умолчанию используется launch-профиль `http` (см.
`Properties/launchSettings.json`), приложение слушает:

- http://localhost:5231

## Проверка

Health-check:

```bash
curl http://localhost:5231/health
```

Ожидаемый ответ: `HTTP 200`, тело `{"status":"healthy"}`.

## Окружение

- Переменная `ASPNETCORE_ENVIRONMENT` устанавливается в `Development`
  через launch-профиль.
- Локальные секреты и пользовательские настройки хранятся в
  `appsettings.Development.json`, который не попадает в Git
  (см. `Backend/.gitignore`).

## Конфигурация

Настройки приложения хранятся в `appsettings.json` в типизированных секциях.
Изменения применяются **после перезапуска** приложения (hot reload не требуется).
Невалидная конфигурация приводит к ошибке запуска с понятным сообщением.

Значения можно переопределить через переменные окружения с двойным подчёркиванием
(стандартный провайдер .NET), например:

```bash
Booking__BookingTimeStepMinutes=30
Jwt__AccessTokenLifetimeMinutes=30
S3__BucketName=custom-bucket
```

### Секции конфигурации

| Секция | Класс Options | Единицы измерения |
| --- | --- | --- |
| `Booking` | `BookingOptions` | `WorkingHoursStart`, `WorkingHoursEnd` — `TimeSpan` (`hh:mm:ss`); `BookingTimeStepMinutes` — целые минуты |
| `Jwt` | `JwtOptions` | `AccessTokenLifetimeMinutes` — целые минуты; `RefreshTokenLifetimeDays` — целые дни |
| `PasswordHashing` | `PasswordHashingOptions` | `BcryptWorkFactor` — целое число (логарифмический фактор BCrypt, допустимый диапазон 4–31) |
| `S3` | `S3Options` | `BucketName` — строка; `PresignedUrlLifetimeMinutes` — целые минуты |
| `Validation` | `ValidationOptions` | Все поля `*MaxLength` — целые числа (количество символов) |

### Ограничения изменения MaxLength

Значения `MaxLength` в секции `Validation` управляют **программной валидацией**
(входные DTO и сеттеры сущностей Domain). Они **не меняют** схему БД автоматически.
Ограничения столбцов БД (`varchar(N)`) задаются независимо в EF-конфигурациях
и синхронизированы со значениями по умолчанию в `appsettings.json`.

Если администратор увеличит лимит в `appsettings.json`, входная валидация пропустит
более длинные строки, но БД может отвергнуть запись (исключение PostgreSQL о
превышении длины). Для согласованного изменения лимитов требуется ручная миграция БД.
