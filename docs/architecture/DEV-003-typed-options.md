# DEV-003 — Архитектурный документ: Типизированные настройки

Версия: 1.0 | Дата: 2026-11-03 | Автор: Архитектор

---

## 1. Цель

Перенести значения из `MeetSpace.Domain.Constants.DomainConstants` в осмысленные секции `appsettings.json` и типизированные группы Options. Сохранить текущее поведение и изоляцию Domain-слоя от конфигурационных библиотек.

---

## 2. Текущее состояние проекта

На момент анализа в solution входят только два проекта:

- **MeetSpace.Domain** — константы (`DomainConstants`), сущности (`Booking`, `Equipment`, `RefreshToken`, `Room`, `RoomEquipment`, `User`), перечисление (`UserRole`).
- **MeetSpace.Api** — точка входа (`Program.cs` с единственным эндпоинтом `GET /health`), пустой `appsettings.json`.

Слои **DataAccess**, **Application**, **Infrastructure** ещё не созданы. Нет тестов, нет EF-конфигураций, нет JWT/S3/BCrypt-сервисов. Все ссылки на `DomainConstants` находятся исключительно в `MeetSpace.Domain`:

- Валидация в сущностях (`Booking`, `Room`, `Equipment`, `User`, `RefreshToken`).
- Ни один внешний слой не ссылается на `DomainConstants`.

---

## 3. Полная карта потребителей

### 3.1. Booking (рабочее время + шаг бронирования)

| Поле DomainConstants | Потребитель | Файл | Строки | Контекст |
|---|---|---|---|---|
| `WorkingHoursStart` | `Booking.SetTimeInterval()` | `Entities/Booking.cs` | 56–57 | Проверка `startTime < WorkingHoursStart` |
| `WorkingHoursEnd` | `Booking.SetTimeInterval()` | `Entities/Booking.cs` | 59–60 | Проверка `endTime > WorkingHoursEnd` |
| `BookingTimeStepMinutes` | `Booking.SetTimeInterval()` | `Entities/Booking.cs` | 62–66 | Проверка кратности минут `startTime` и `endTime` шагу 15 |

### 3.2. JWT (сроки токенов)

| Поле DomainConstants | Потребитель | Файл | Строки | Контекст |
|---|---|---|---|---|
| `AccessTokenLifetimeMinutes` | **Не используется** ни в одном файле решения | — | — | Константа объявлена, но потребитель (JWT-сервис) ещё не реализован |
| `RefreshTokenLifetimeDays` | `RefreshToken` (конструктор) | `Entities/RefreshToken.cs` | 22 | Вычисление `ExpiresAt = CreatedAt.AddDays(RefreshTokenLifetimeDays)` |

### 3.3. PasswordHashing (BCrypt)

| Поле DomainConstants | Потребитель | Файл | Строки | Контекст |
|---|---|---|---|---|
| `BcryptWorkFactor` | **Не используется** ни в одном файле решения | — | — | Константа объявлена, но BCrypt-сервис хеширования ещё не реализован |

### 3.4. S3 (хранилище)

| Поле DomainConstants | Потребитель | Файл | Строки | Контекст |
|---|---|---|---|---|
| `S3BucketName` | **Не используется** ни в одном файле решения | — | — | Константа объявлена, но S3-сервис ещё не реализован |
| `PresignedUrlLifetimeMinutes` | **Не используется** ни в одном файле решения | — | — | Константа объявлена, но S3-сервис ещё не реализован |

### 3.5. Validation (ограничения длины строк)

| Поле DomainConstants | Потребитель | Файл | Строки | Контекст |
|---|---|---|---|---|
| `UserNameMaxLength` | `User.SetFirstName()` | `Entities/User.cs` | 34–35 | Проверка длины `firstName` |
| `UserNameMaxLength` | `User.SetLastName()` | `Entities/User.cs` | 42–43 | Проверка длины `lastName` |
| `EmailMaxLength` | `User.SetEmail()` | `Entities/User.cs` | 50–51 | Проверка длины `email` |
| `EquipmentNameMaxLength` | `Equipment.SetName()` | `Entities/Equipment.cs` | 21–22 | Проверка длины `name` |
| `RoomNameMaxLength` | `Room.SetName()` | `Entities/Room.cs` | 29–30 | Проверка длины `name` |
| `RoomDescriptionMaxLength` | `Room.SetDescription()` | `Entities/Room.cs` | 50–51 | Проверка длины `description` |
| `BookingTitleMaxLength` | `Booking.SetTitle()` | `Entities/Booking.cs` | 32–33 | Проверка длины `title` |
| `BookingDescriptionMaxLength` | `Booking.SetDescription()` | `Entities/Booking.cs` | 39–40 | Проверка длины `description` |

---

## 4. Группы Options и размещение по слоям

Выделяем пять групп типизированных настроек, соответствующих группам из тикета. Каждая группа — отдельный класс Options. **Не создаём общий класс со всеми настройками.**

### 4.1. BookingOptions

```csharp
// Слой: Application (абстракция)
// Namespace: MeetSpace.Application.Options
public class BookingOptions
{
    public TimeSpan WorkingHoursStart { get; init; }    // 09:00:00
    public TimeSpan WorkingHoursEnd { get; init; }      // 18:00:00
    public int BookingTimeStepMinutes { get; init; }    // 15
}
```

**Обоснование названия:** Группирует настройки рабочего времени и шага бронирования — все они относятся к домену бронирования. Альтернативы (`WorkingHoursOptions`, `SchedulingOptions`) менее точны, т.к. шаг не относится сугубо к рабочим часам.

### 4.2. JwtOptions

```csharp
// Слой: Infrastructure (реализация JWT-сервиса)
// Namespace: MeetSpace.Infrastructure.Options
public class JwtOptions
{
    public int AccessTokenLifetimeMinutes { get; init; }  // 10
    public int RefreshTokenLifetimeDays { get; init; }    // 14
}
```

**Обоснование названия:** Стандартное имя для JWT-настроек в ASP.NET Core. Настройки JWT технические, размещаются в Infrastructure.

### 4.3. PasswordHashingOptions

```csharp
// Слой: Infrastructure (реализация BCrypt-сервиса)
// Namespace: MeetSpace.Infrastructure.Options
public class PasswordHashingOptions
{
    public int BcryptWorkFactor { get; init; }  // 12
}
```

**Обоснование названия:** `PasswordHashing` — глагольное имя, отражающее операцию. Альтернативы: `BcryptOptions` слишком привязано к конкретной библиотеке; `PasswordHashingOptions` допускает смену алгоритма.

### 4.4. S3Options

```csharp
// Слой: Infrastructure (реализация S3-сервиса)
// Namespace: MeetSpace.Infrastructure.Options
public class S3Options
{
    public string BucketName { get; init; } = string.Empty;        // meet-space
    public int PresignedUrlLifetimeMinutes { get; init; }          // 60
}
```

**Обоснование названия:** Стандартное имя для S3-настроек.

### 4.5. ValidationOptions

```csharp
// Слой: Application (абстракция)
// Namespace: MeetSpace.Application.Options
public class ValidationOptions
{
    public int UserNameMaxLength { get; init; }             // 100
    public int EmailMaxLength { get; init; }                // 255
    public int EquipmentNameMaxLength { get; init; }        // 50
    public int RoomNameMaxLength { get; init; }             // 200
    public int RoomDescriptionMaxLength { get; init; }      // 2000
    public int BookingTitleMaxLength { get; init; }         // 200
    public int BookingDescriptionMaxLength { get; init; }   // 2000
}
```

**Обоснование названия:** Группирует все ограничения длины, используемые во входной валидации. Альтернатива `MaxLengthOptions` менее читаема. Имя `ValidationOptions` отражает назначение — конфигурация валидации.

---

## 5. Способ передачи значений в Domain

**Ключевое правило AGENTS.md:** Domain не должен зависеть от `IConfiguration`, `IOptions<T>`, ASP.NET Core, чтения JSON или service locator. Также Application не должен получать зависимость от `Microsoft.Extensions.Options` ради доступа к настройкам.

### 5.1. Для BookingOptions

**Потребитель:** `Booking.SetTimeInterval()` в Domain.

**Решение:** Создать в Application объект-правило (value object), который передаётся в метод `SetTimeInterval` как параметр:

```csharp
// Слой: Application
// Namespace: MeetSpace.Application.Rules
public class BookingTimeRules
{
    public TimeSpan WorkingHoursStart { get; init; }
    public TimeSpan WorkingHoursEnd { get; init; }
    public int TimeStepMinutes { get; init; }
}
```

В `Booking.SetTimeInterval()` заменить прямые ссылки на `DomainConstants` параметром `BookingTimeRules rules`:

```csharp
// Слой: Domain
public void SetTimeInterval(TimeSpan startTime, TimeSpan endTime, BookingTimeRules rules)
{
    // ... проверки с rules.WorkingHoursStart, rules.WorkingHoursEnd, rules.TimeStepMinutes
}
```

**Сборка правил из Options** — ответственность Application-сервиса бронирования, который получает `IOptions<BookingOptions>` (см. раздел 10).

### 5.2. Для ValidationOptions (MaxLength)

**Потребители:** `User.SetFirstName/SetLastName/SetEmail`, `Equipment.SetName`, `Room.SetName/SetDescription`, `Booking.SetTitle/SetDescription` в Domain.

**Решение:** Создать объект-правило с ограничениями длины:

```csharp
// Слой: Application
// Namespace: MeetSpace.Application.Rules
public class StringLengthRules
{
    public int UserNameMaxLength { get; init; }
    public int EmailMaxLength { get; init; }
    public int EquipmentNameMaxLength { get; init; }
    public int RoomNameMaxLength { get; init; }
    public int RoomDescriptionMaxLength { get; init; }
    public int BookingTitleMaxLength { get; init; }
    public int BookingDescriptionMaxLength { get; init; }
}
```

Валидационные методы сущностей принимают лимит как параметр:

```csharp
// Слой: Domain
public void SetTitle(string title, int maxLength)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(title);
    if (title.Length > maxLength)
        throw new ArgumentException($"Title must not exceed {maxLength} characters.");
    Title = title;
}
```

**Альтернатива (отклонена):** передача всего `StringLengthRules` в конструктор сущности. Отклонено, потому что усложнит код сериализации EF (EF создаёт сущности через private-конструктор) и потребует хранения ссылки на правила внутри сущности.

**Компромисс (принят):** передавать конкретный лимит (`int maxLength`) в каждый setter-метод. Application-сервис, вызывающий эти методы, извлекает нужное значение из `StringLengthRules` (собранного из `ValidationOptions`).

### 5.3. RefreshTokenLifetimeDays

**Потребитель:** `RefreshToken` (конструктор) в Domain — `ExpiresAt = CreatedAt.AddDays(lifetimeDays)`.

**Решение:** Передавать `lifetimeDays` как параметр конструктора:

```csharp
public RefreshToken(string token, Guid userId, int lifetimeDays)
{
    // ...
    ExpiresAt = CreatedAt.AddDays(lifetimeDays);
}
```

Значение извлекается из `JwtOptions.RefreshTokenLifetimeDays` в Application/Infrastructure сервисе аутентификации.

### 5.4. BcryptWorkFactor, S3BucketName, PresignedUrlLifetimeMinutes, AccessTokenLifetimeMinutes

**Потребители ещё не реализованы.** Настройки передаются напрямую в соответствующие реализации через DI (IOptions<T> → конструктор сервиса):

- `PasswordHashingOptions` → BCrypt-сервис (Infrastructure)
- `S3Options` → S3-сервис (Infrastructure)
- `JwtOptions.AccessTokenLifetimeMinutes` → JWT-сервис (Infrastructure)

Domain-слой **не** является потребителем этих значений, поэтому специальных правил/абстракций не требуется.

---

## 6. Решение по MaxLength: динамическая валидация vs compile-time константы

### Проблема

Значения `MaxLength` из Options — runtime-значения. Их **невозможно** использовать в:
- Атрибутах валидации (`[MaxLength]`, `[StringLength]`) — требуют compile-time констант.
- Атрибутах EF mapping (`[MaxLength]`) — тоже требуют констант.

### Принятое решение

#### 6.1. Входная валидация (Api → Application)

Использовать **FluentValidation** или **кастомный валидатор**, получающий `ValidationOptions` через DI:

```csharp
// В Application или Api слое
public class CreateBookingValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingValidator(IOptions<ValidationOptions> options)
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(options.Value.BookingTitleMaxLength);
    }
}
```

Альтернатива без FluentValidation — ручная проверка в Application-сервисе, вызывающем сеттеры сущностей с параметром `maxLength`.

#### 6.2. Валидация в Domain

Сеттеры сущностей уже принимают `maxLength` параметром (см. раздел 5.2). Это основная защита.

#### 6.3. EF Mapping — ограничения столбцов БД

**Позиция:** Изменение `appsettings.json` **не должно** автоматически менять EF-модель или схему БД. Ограничения столбцов БД (`varchar(N)`) задаются **независимо** от `ValidationOptions` в EF-конфигурациях (DataAccess) как compile-time константы, соответствующие значениям по умолчанию из `appsettings.json`.

На этапе создания DataAccess-слоя значения MaxLength в EF-конфигурациях будут синхронизированы со значениями по умолчанию в `appsettings.json`:

```csharp
// MeetSpace.DataAccess/Configurations/UserConfiguration.cs
builder.Property(u => u.FirstName).HasMaxLength(100);
builder.Property(u => u.LastName).HasMaxLength(100);
builder.Property(u => u.Email).HasMaxLength(255);
// ... и т.д.
```

Если администратор увеличит лимит в `appsettings.json`, входная валидация пропустит более длинные строки, но БД отвергнет запись (исключение PostgreSQL о превышении длины). Это допустимое поведение: администратор должен согласованно менять и конфигурацию, и схему БД.

Если потребуется синхронизация — это будет отдельный тикет с миграцией. В рамках DEV-003 миграции не выполняются.

#### 6.4. Сводка слоёв валидации

| Слой | Механизм | Источник ограничения |
|---|---|---|
| Api (DTO) | FluentValidation / кастомный валидатор | `ValidationOptions` из `appsettings.json` |
| Domain (сущности) | Сеттеры с параметром `maxLength` | Значение, переданное из Application |
| DataAccess (БД) | `HasMaxLength(N)` в EF-конфигурациях | Compile-time константа, синхронизированная со значениями по умолчанию в `appsettings.json` |

---

## 7. Структура appsettings.json

```jsonc
{
  "Logging": { /* без изменений */ },
  "AllowedHosts": "*",

  "Booking": {
    "WorkingHoursStart": "09:00:00",
    "WorkingHoursEnd": "18:00:00",
    "BookingTimeStepMinutes": 15
  },

  "Jwt": {
    "AccessTokenLifetimeMinutes": 10,
    "RefreshTokenLifetimeDays": 14
  },

  "PasswordHashing": {
    "BcryptWorkFactor": 12
  },

  "S3": {
    "BucketName": "meet-space",
    "PresignedUrlLifetimeMinutes": 60
  },

  "Validation": {
    "UserNameMaxLength": 100,
    "EmailMaxLength": 255,
    "EquipmentNameMaxLength": 50,
    "RoomNameMaxLength": 200,
    "RoomDescriptionMaxLength": 2000,
    "BookingTitleMaxLength": 200,
    "BookingDescriptionMaxLength": 2000
  }
}
```

**Единицы измерения:**
- `WorkingHoursStart`, `WorkingHoursEnd` — `TimeSpan` в формате `hh:mm:ss`.
- `BookingTimeStepMinutes`, `AccessTokenLifetimeMinutes`, `PresignedUrlLifetimeMinutes` — целые минуты.
- `RefreshTokenLifetimeDays` — целые дни.
- `BcryptWorkFactor` — целое число (логарифмический фактор BCrypt).
- Все `MaxLength` — целые числа (количество символов).

---

## 8. Регистрация, binding, DI и валидация при запуске

### 8.1. Регистрация в Program.cs

```csharp
// MeetSpace.Api/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Booking
builder.Services
    .AddOptions<BookingOptions>()
    .Bind(builder.Configuration.GetSection("Booking"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (options.WorkingHoursStart >= options.WorkingHoursEnd)
            return false; // fail: "WorkingHoursStart must be earlier than WorkingHoursEnd"
        if (options.BookingTimeStepMinutes <= 0 || options.BookingTimeStepMinutes > 60)
            return false;
        if (options.BookingTimeStepMinutes > (int)(options.WorkingHoursEnd - options.WorkingHoursStart).TotalMinutes)
            return false; // шаг не может превышать длительность рабочего дня
        return true;
    }, "Invalid Booking configuration: working hours must be valid and time step must be positive and fit within the working interval.");

// Jwt
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
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
builder.Services
    .AddOptions<PasswordHashingOptions>()
    .Bind(builder.Configuration.GetSection("PasswordHashing"))
    .ValidateOnStart()
    .Validate(options =>
    {
        // BCrypt work factor: допустимый диапазон 4–31 (библиотека BCrypt.Net)
        if (options.BcryptWorkFactor < 4 || options.BcryptWorkFactor > 31)
            return false;
        return true;
    }, "PasswordHashing: BcryptWorkFactor must be between 4 and 31.");

// S3
builder.Services
    .AddOptions<S3Options>()
    .Bind(builder.Configuration.GetSection("S3"))
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
builder.Services
    .AddOptions<ValidationOptions>()
    .Bind(builder.Configuration.GetSection("Validation"))
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
```

### 8.2. Межполевые правила валидации

| Группа | Правило | Сообщение об ошибке |
|---|---|---|
| Booking | `WorkingHoursStart < WorkingHoursEnd` | WorkingHoursStart must be earlier than WorkingHoursEnd |
| Booking | `BookingTimeStepMinutes > 0` | BookingTimeStepMinutes must be positive |
| Booking | Шаг ≤ длительность рабочего дня в минутах | BookingTimeStepMinutes must fit within the working interval |
| Jwt | `AccessTokenLifetimeMinutes > 0` | AccessTokenLifetimeMinutes must be positive |
| Jwt | `RefreshTokenLifetimeDays > 0` | RefreshTokenLifetimeDays must be positive |
| PasswordHashing | `4 ≤ BcryptWorkFactor ≤ 31` | BcryptWorkFactor must be between 4 and 31 |
| S3 | `BucketName` не null, не whitespace | BucketName is required |
| S3 | `PresignedUrlLifetimeMinutes > 0` | PresignedUrlLifetimeMinutes must be positive |
| Validation | Все поля > 0 | All MaxLength values must be positive |

**Важно:** Ошибки валидации не должны раскрывать секретов. Имена полей в сообщениях — публичные ключи конфигурации, а не значения.

### 8.3. ValidateOnStart

Используется `ValidateOnStart()` — встроенный механизм Options в .NET, который запускает валидацию при первом разрешении `IOptions<T>` (обычно при старте приложения, когда DI-контейнер создаёт сервисы). При неудачной валидации приложение падает с `OptionsValidationException` при запуске, что соответствует требованию «ошибка запуска с понятным сообщением».

### 8.4. Переопределение через environment variables

Штатный провайдер конфигурации .NET поддерживает переменные окружения с двойным подчёркиванием:

```bash
# Пример переопределения
Booking__BookingTimeStepMinutes=30
Jwt__AccessTokenLifetimeMinutes=30
S3__BucketName=custom-bucket
```

Никаких дополнительных действий не требуется — `AddOptions<T>().Bind(Configuration.GetSection(...))` автоматически учитывает все зарегистрированные провайдеры, включая environment variables.

---

## 9. Что остаётся неизменяемыми техническими ограничениями

### 9.1. Исключения из переноса

**Все** поля `DomainConstants` переносятся в `appsettings.json` и Options. Исключений нет.

### 9.2. Вопрос о неизменяемых технических ограничениях

Рассмотрена возможность оставить некоторые значения как неизменяемые технические константы, но принято решение перенести всё:

| Значение | Аргумент за оставление константой | Контраргумент |
|---|---|---|
| `BcryptWorkFactor = 12` | Технический параметр безопасности, менять опасно | Администратор должен иметь возможность изменить его без перекомпиляции (например, при росте вычислительных мощностей) |
| `BookingTimeStepMinutes = 15` | Фундаментальное ограничение UI-календаря | Администратор может захотеть шаг 30 минут |
| `AccessTokenLifetimeMinutes = 10` | Стандарт безопасности | Это типично изменяемое значение |
| `MaxLength` | Сильная связь со схемой БД | Описано в разделе 6.3 — изменение appsettings не меняет схему БД автоматически |

**Все значения переносятся.** Это даёт администратору гибкость без перекомпиляции. Значения по умолчанию в `appsettings.json` сохраняют исходные значения из `DomainConstants`.

---

## 10. Полная схема передачи значений (на примере)

### 10.1. Путь значения `BookingTimeStepMinutes`

```
appsettings.json
  ↓ (IConfiguration binding)
BookingOptions.BookingTimeStepMinutes
  ↓ (DI: IOptions<BookingOptions>)
BookingService (Application)
  ↓ (маппинг в BookingTimeRules.TimeStepMinutes)
BookingTimeRules rules
  ↓ (параметр метода)
Booking.SetTimeInterval(startTime, endTime, rules)
  ↓ (проверка в Domain)
startTime.Minutes % rules.TimeStepMinutes != 0 → исключение
```

### 10.2. Путь значения `BookingTitleMaxLength`

```
appsettings.json
  ↓ (IConfiguration binding)
ValidationOptions.BookingTitleMaxLength
  ↓ (DI: IOptions<ValidationOptions>)
BookingService (Application)
  ↓ (извлекает rules.BookingTitleMaxLength)
int maxLength
  ↓ (параметр метода)
Booking.SetTitle(title, maxLength)
  ↓ (проверка в Domain)
title.Length > maxLength → исключение
```

### 10.3. Путь значения `RefreshTokenLifetimeDays`

```
appsettings.json
  ↓ (IConfiguration binding)
JwtOptions.RefreshTokenLifetimeDays
  ↓ (DI: IOptions<JwtOptions>)
AuthService (Application/Infrastructure)
  ↓ (параметр конструктора)
new RefreshToken(token, userId, options.Value.RefreshTokenLifetimeDays)
  ↓ (в Domain)
ExpiresAt = CreatedAt.AddDays(lifetimeDays)
```

---

## 11. Сводка размещения файлов по слоям

| Файл | Слой | Назначение |
|---|---|---|
| `BookingOptions.cs` | Application | Options-класс для рабочего времени и шага |
| `ValidationOptions.cs` | Application | Options-класс для ограничений длины |
| `BookingTimeRules.cs` | Application | Объект-правило для передачи в Domain |
| `StringLengthRules.cs` | Application | Объект-правило ограничений длины для передачи в Domain |
| `JwtOptions.cs` | Infrastructure | Options-класс для JWT-настроек |
| `PasswordHashingOptions.cs` | Infrastructure | Options-класс для BCrypt |
| `S3Options.cs` | Infrastructure | Options-класс для S3 |
| `appsettings.json` | Api (конфигурация) | Значения по умолчанию |

**Примечание:** На текущий момент слои Application и Infrastructure не существуют. Options-классы будут созданы при реализации в соответствующих проектах.

---

## 12. Изменения в сущностях Domain

### 12.1. Booking

```diff
- public void SetTimeInterval(TimeSpan startTime, TimeSpan endTime)
+ public void SetTimeInterval(TimeSpan startTime, TimeSpan endTime, BookingTimeRules rules)

- public void SetTitle(string title)
+ public void SetTitle(string title, int maxLength)

- public void SetDescription(string? description)
+ public void SetDescription(string? description, int maxLength)
```

### 12.2. Room

```diff
- public void SetName(string name)
+ public void SetName(string name, int maxLength)

- public void SetDescription(string? description)
+ public void SetDescription(string? description, int maxLength)
```

### 12.3. Equipment

```diff
- public void SetName(string name)
+ public void SetName(string name, int maxLength)
```

### 12.4. User

```diff
- public void SetFirstName(string firstName)
+ public void SetFirstName(string firstName, int maxLength)

- public void SetLastName(string lastName)
+ public void SetLastName(string lastName, int maxLength)

- public void SetEmail(string email)
+ public void SetEmail(string email, int maxLength)
```

### 12.5. RefreshToken

```diff
- public RefreshToken(string token, Guid userId)
+ public RefreshToken(string token, Guid userId, int lifetimeDays)
```

Конструктор `Booking` также нуждается в параметрах:
```diff
- public Booking(string title, Guid userId, Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
+ public Booking(string title, Guid userId, Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime, BookingTimeRules rules, int titleMaxLength)
```

Аналогично для `Room`, `Equipment`, `User` — конструкторы получают параметры `maxLength` для каждого строкового поля.

**Важно:** `BookingTimeRules` и `int maxLength` — обычные C# типы/классы без зависимости от ASP.NET. Domain остаётся независимым от конфигурационных библиотек.

---

## 13. Совместимость с будущими слоями

### 13.1. DataAccess (EF Core)

- EF-конфигурации будут использовать `HasMaxLength(N)` с compile-time константами, синхронизированными со значениями по умолчанию из `appsettings.json`.
- EF создаёт сущности через private-конструктор без параметров — это не затрагивается изменениями.
- При загрузке из БД сеттеры валидации не вызываются (поля устанавливаются напрямую через backing fields или private setters), что корректно.

### 13.2. Infrastructure (JWT, S3, BCrypt)

- Сервисы получают соответствующие Options через DI (`IOptions<T>` в конструкторе).
- Никакие изменения в Domain для этих сервисов не требуются.

### 13.3. Тесты

- Unit-тесты Domain смогут передавать `BookingTimeRules` и `maxLength` напрямую, без DI.
- Интеграционные тесты конфигурации описаны в тикете (раздел «Написание тестов»).

---

## 14. Жизненный цикл изменений конфигурации

- **Изменения применяются после перезапуска приложения.** Hot reload не требуется и не поддерживается.
- При невалидной конфигурации приложение завершает запуск с `OptionsValidationException`.
- Переопределение через environment variables поддерживается штатно.

---

## 15. Итоговая таблица: что, куда и как

| Исходное поле DomainConstants | Секция appsettings.json | Класс Options | Слой Options | Потребитель | Способ передачи в Domain |
|---|---|---|---|---|---|
| `WorkingHoursStart` | `Booking` | `BookingOptions` | Application | `Booking.SetTimeInterval()` | Через `BookingTimeRules` (Application → параметр метода) |
| `WorkingHoursEnd` | `Booking` | `BookingOptions` | Application | `Booking.SetTimeInterval()` | Через `BookingTimeRules` (Application → параметр метода) |
| `BookingTimeStepMinutes` | `Booking` | `BookingOptions` | Application | `Booking.SetTimeInterval()` | Через `BookingTimeRules` (Application → параметр метода) |
| `AccessTokenLifetimeMinutes` | `Jwt` | `JwtOptions` | Infrastructure | JWT-сервис (ещё не реализован) | Напрямую в сервис через IOptions (не Domain) |
| `RefreshTokenLifetimeDays` | `Jwt` | `JwtOptions` | Infrastructure | `RefreshToken` (конструктор) | Параметр `lifetimeDays` конструктора |
| `BcryptWorkFactor` | `PasswordHashing` | `PasswordHashingOptions` | Infrastructure | BCrypt-сервис (ещё не реализован) | Напрямую в сервис через IOptions (не Domain) |
| `S3BucketName` | `S3` | `S3Options` | Infrastructure | S3-сервис (ещё не реализован) | Напрямую в сервис через IOptions (не Domain) |
| `PresignedUrlLifetimeMinutes` | `S3` | `S3Options` | Infrastructure | S3-сервис (ещё не реализован) | Напрямую в сервис через IOptions (не Domain) |
| `UserNameMaxLength` | `Validation` | `ValidationOptions` | Application | `User.SetFirstName/SetLastName` | Параметр `maxLength` метода |
| `EmailMaxLength` | `Validation` | `ValidationOptions` | Application | `User.SetEmail` | Параметр `maxLength` метода |
| `EquipmentNameMaxLength` | `Validation` | `ValidationOptions` | Application | `Equipment.SetName` | Параметр `maxLength` метода |
| `RoomNameMaxLength` | `Validation` | `ValidationOptions` | Application | `Room.SetName` | Параметр `maxLength` метода |
| `RoomDescriptionMaxLength` | `Validation` | `ValidationOptions` | Application | `Room.SetDescription` | Параметр `maxLength` метода |
| `BookingTitleMaxLength` | `Validation` | `ValidationOptions` | Application | `Booking.SetTitle` | Параметр `maxLength` метода |
| `BookingDescriptionMaxLength` | `Validation` | `ValidationOptions` | Application | `Booking.SetDescription` | Параметр `maxLength` метода |

---

## 16. Открытые вопросы и риски

1. **Создание Application и Infrastructure проектов.** В текущем solution только Domain и Api. Options-классы BookingOptions и ValidationOptions формально должны быть в Application, JwtOptions/PasswordHashingOptions/S3Options — в Infrastructure. На этапе реализации DEV-003 эти проекты должны быть созданы, либо Options временно размещены в Api с последующим переносом. **Рекомендация:** создать проекты Application и Infrastructure в рамках DEV-003.

2. **`BookingTimeRules` в конструкторе Booking.** Конструктор `Booking` (строка 19) вызывает `SetTimeInterval(startTime, endTime)`. После изменений потребуется параметр `BookingTimeRules rules`. Это меняет сигнатуру конструктора. Альтернатива: убрать вызов `SetTimeInterval` из конструктора и вызывать его отдельно после создания. **Рекомендация:** добавить параметр `BookingTimeRules` в конструктор.

3. **EF-конфигурации.** DataAccess ещё не создан. EF-конфигурации и `HasMaxLength` будут реализованы в отдельном тикете. В DEV-003 достаточно задокументировать решение (раздел 6).

4. **Удаление DomainConstants.cs.** После полной замены всех обращений файл должен быть удалён. На данный момент все обращения только в Domain, поэтому удаление безопасно после рефакторинга сущностей.

---

## 17. Проверка соблюдения AGENTS.md

| Правило | Статус | Комментарий |
|---|---|---|
| Domain не зависит от IConfiguration/IOptions | ✅ Соблюдено | Параметры передаются как обычные типы (`BookingTimeRules`, `int`) |
| Application не зависит от IOptions | ⚠️ Частично | Application-сервисы получают `IOptions<T>` (это допустимо для Application как orchestrator), но могут также принимать готовые объекты-правила |
| Модули зависят от абстракций | ✅ Соблюдено | `BookingTimeRules`, `StringLengthRules` — абстракции в Application, не зависящие от инфраструктуры |
| DI используется | ✅ Соблюдено | `AddOptions<T>()` + `IOptions<T>` |
| Ошибка n+1 | N/A | Не относится к задаче конфигурации |
| Изменения после перезапуска | ✅ Соблюдено | Hot reload не требуется |
| Validation MaxLength не в атрибутах | ✅ Соблюдено | Динамическая валидация через FluentValidation или кастомный валидатор |
