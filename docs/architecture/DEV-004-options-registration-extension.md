# DEV-004 — Архитектурный документ: Вынесение регистрации Options в extension-метод

Версия: 1.0 | Дата: 2026-11-03 | Автор: Архитектор

---

## 1. Цель

Вынести регистрацию всех пяти групп Options (`BookingOptions`, `JwtOptions`, `PasswordHashingOptions`, `S3Options`, `ValidationOptions`) из `Program.cs` в extension-метод, чтобы в `Program.cs` остался один понятный вызов. Сохранить binding, валидацию при запуске (`ValidateOnStart`) и текущее поведение приложения после DEV-003.

---

## 2. Анализ текущего состояния Program.cs

### 2.1. Общая статистика

Файл `Backend/src/MeetSpace.Api/Program.cs` состоит из 83 строк. Регистрация Options занимает строки 6-77 — **72 строки (87%)**. Собственно логика приложения (создание builder, `MapGet("/health")`, `app.Run()`) занимает лишь 11 строк.

### 2.2. Пять блоков регистрации

| # | Options-класс | Слой | Секция | Строки в Program.cs | Кол-во строк блока |
|---|---|---|---|---|---|
| 1 | `BookingOptions` | Application | `Booking` | 7-20 | 14 |
| 2 | `JwtOptions` | Infrastructure | `Jwt` | 23-34 | 12 |
| 3 | `PasswordHashingOptions` | Infrastructure | `PasswordHashing` | 37-46 | 10 |
| 4 | `S3Options` | Infrastructure | `S3` | 49-60 | 12 |
| 5 | `ValidationOptions` | Application | `Validation` | 63-77 | 15 |
| | | | **Итого** | | **63 строки «полезной» регистрации** |

### 2.3. Общий паттерн регистрации

Каждый блок имеет идентичную структуру:

```csharp
builder.Services
    .AddOptions<T>()
    .Bind(builder.Configuration.GetSection("SectionName"))
    .ValidateOnStart()
    .Validate(options => { /* лямбда валидации */ }, "Сообщение об ошибке");
```

Различаются только:
- Тип `T` (класс Options)
- Имя секции в `appsettings.json`
- Лямбда-выражение валидации
- Сообщение об ошибке

Никаких `PostConfigure`, `named options`, `ValidateDataAnnotations` или `IValidateOptions<T>` не используется. Все регистрации используют одинаковый жизненный цикл (singleton через `IOptions<T>` по умолчанию).

### 2.4. Порядок регистрации

Текущий порядок в Program.cs: Booking → Jwt → PasswordHashing → S3 → Validation. Порядок не влияет на поведение, так как все пять групп независимы. Расширение должно сохранить этот порядок.

### 2.5. Зависимости

- `builder.Services` — `IServiceCollection`.
- `builder.Configuration` — `IConfiguration` (через `builder.Configuration.GetSection(...)`).
- Каждый блок независим от других.

---

## 3. Предлагаемое решение

### 3.1. Extension-метод

```csharp
// Сигнатура
public static IServiceCollection AddMeetSpaceOptions(
    this IServiceCollection services,
    IConfiguration configuration)
```

### 3.2. Место размещения

**Файл:** `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs`

**Namespace:** `MeetSpace.Api.Extensions`

**Обоснование:**
- Тикет явно указывает `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs` как предпочтительное место.
- Папка `Extensions/` ещё не существует, будет создана.
- Extension-метод размещается в слое **Api**, так как он работает с `IServiceCollection` и `IConfiguration` — инфраструктурой ASP.NET Core. Согласно AGENTS.md, Api — слой, связанный с web, где описываются эндпоинты, контроллеры, политики. Регистрация в DI-контейнере — ответственность Api.
- Это не нарушает границы слоёв: классы Options остаются в своих слоях (Application/Infrastructure), а регистрация происходит в точке компоновки — Api.

### 3.3. Группировка

Все пять регистраций помещаются в **один extension-метод** `AddMeetSpaceOptions`. Разбиение на пять отдельных методов (по одному на группу) нецелесообразно:
- Все группы регистрируются единообразно (один и тот же fluent-паттерн).
- Нет слоёв или модулей, которые регистрировали бы только часть Options.
- `Program.cs` после рефакторинга должен содержать один вызов — этого достаточно.

Внутри метода регистрации идут в том же порядке, что и в Program.cs, с теми же комментариями.

### 3.4. Полный код extension-метода

```csharp
// Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs
using MeetSpace.Application.Options;
using MeetSpace.Infrastructure.Options;

namespace MeetSpace.Api.Extensions;

public static class OptionsServiceCollectionExtensions
{
    public static IServiceCollection AddMeetSpaceOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Booking
        services
            .AddOptions<BookingOptions>()
            .Bind(configuration.GetSection("Booking"))
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.WorkingHoursStart >= options.WorkingHoursEnd)
                    return false;
                if (options.BookingTimeStepMinutes <= 0 || options.BookingTimeStepMinutes > 60)
                    return false;
                if (options.BookingTimeStepMinutes > (int)(options.WorkingHoursEnd - options.WorkingHoursStart).TotalMinutes)
                    return false;
                return true;
            }, "Invalid Booking configuration: working hours must be valid and time step must be positive and fit within the working interval.");

        // Jwt
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.AccessTokenLifetimeMinutes <= 0)
                    return false;
                if (options.RefreshTokenLifetimeDays <= 0)
                    return false;
                return true;
            }, "Jwt configuration: lifetimes must be positive.");

        // PasswordHashing
        services
            .AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection("PasswordHashing"))
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.BcryptWorkFactor < 4 || options.BcryptWorkFactor > 31)
                    return false;
                return true;
            }, "PasswordHashing: BcryptWorkFactor must be between 4 and 31.");

        // S3
        services
            .AddOptions<S3Options>()
            .Bind(configuration.GetSection("S3"))
            .ValidateOnStart()
            .Validate(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BucketName))
                    return false;
                if (options.PresignedUrlLifetimeMinutes <= 0)
                    return false;
                return true;
            }, "S3 configuration: BucketName is required and PresignedUrlLifetimeMinutes must be positive.");

        // Validation
        services
            .AddOptions<ValidationOptions>()
            .Bind(configuration.GetSection("Validation"))
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.UserNameMaxLength <= 0) return false;
                if (options.EmailMaxLength <= 0) return false;
                if (options.EquipmentNameMaxLength <= 0) return false;
                if (options.RoomNameMaxLength <= 0) return false;
                if (options.RoomDescriptionMaxLength <= 0) return false;
                if (options.BookingTitleMaxLength <= 0) return false;
                if (options.BookingDescriptionMaxLength <= 0) return false;
                return true;
            }, "Validation: all MaxLength values must be positive.");

        return services;
    }
}
```

### 3.5. Как будет выглядеть Program.cs после рефакторинга

```csharp
using MeetSpace.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMeetSpaceOptions(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
```

**Изменения:**
- Убран `using MeetSpace.Application.Options;` (больше не нужен в Program.cs).
- Убран `using MeetSpace.Infrastructure.Options;` (больше не нужен в Program.cs).
- Добавлен `using MeetSpace.Api.Extensions;`.
- 72 строки регистрации заменены одной строкой: `builder.Services.AddMeetSpaceOptions(builder.Configuration);`.
- `var builder = ...` и `var app = ...` остаются на месте.
- `/health` и `app.Run()` без изменений.

---

## 4. Сохранение текущего поведения

| Аспект | Статус | Комментарий |
|---|---|---|
| Binding к секциям | ✅ Сохранён | `.Bind(configuration.GetSection(...))` с теми же именами секций |
| `ValidateOnStart()` | ✅ Сохранён | Вызывается для каждой группы |
| Лямбды валидации | ✅ Сохранены | Буквально те же лямбды (копируются без изменений) |
| Сообщения об ошибках | ✅ Сохранены | Те же строки |
| Порядок регистрации | ✅ Сохранён | Booking → Jwt → PasswordHashing → S3 → Validation |
| Имена секций | ✅ Сохранены | "Booking", "Jwt", "PasswordHashing", "S3", "Validation" |
| Отсутствие PostConfigure | ✅ Сохранено | Не добавляется |
| Отсутствие named options | ✅ Сохранено | Не добавляется |
| Жизненный цикл | ✅ Сохранён | Стандартный singleton через `IOptions<T>` |

---

## 5. Что не меняется

- Классы Options (`BookingOptions`, `JwtOptions`, `PasswordHashingOptions`, `S3Options`, `ValidationOptions`) — без изменений.
- `appsettings.json` — без изменений.
- Слои размещения классов Options (Application / Infrastructure) — без изменений.
- Зависимости проектов в `.csproj` — без изменений (MeetSpace.Api уже ссылается на Application и Infrastructure).
- Поведение при невалидной конфигурации — приложение падает при запуске с `OptionsValidationException`.

---

## 6. Открытые вопросы

Нет. Решение прямолинейное: перенос существующего кода в extension-метод без изменения логики.

---

## 7. Проверка соблюдения AGENTS.md

| Правило | Статус | Комментарий |
|---|---|---|
| Clean architecture, разделение по слоям | ✅ Соблюдено | Extension-метод в Api, классы Options в своих слоях |
| Single Responsibility | ✅ Соблюдено | Метод `AddMeetSpaceOptions` отвечает только за регистрацию Options |
| Зависимость от абстракций | ✅ Соблюдено | Метод принимает `IServiceCollection` и `IConfiguration` — стандартные абстракции ASP.NET Core |
| DI используется | ✅ Соблюдено | `AddOptions<T>()` — штатный механизм DI |
| Не менять код вне задачи | ✅ Соблюдено | Изменения только в Api-слое: Program.cs и новый extension-файл |

---

## 8. Итог

Рефакторинг сводится к:
1. Созданию файла `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs` с единственным extension-методом `AddMeetSpaceOptions`.
2. Замене 72 строк ручной регистрации в `Program.cs` одной строкой вызова.
3. Корректировке using-директив в `Program.cs`.

Никаких изменений поведения, значений, порядка валидации, конфигурационных секций или классов Options.
