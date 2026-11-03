# DEV-005 — Архитектурный документ: Бизнес-логика бронирований в Application

Версия: 1.0 | Дата: 2026-11-03 | Автор: Архитектор

---

## 1. Цель

Реализовать сервисы Application для сценариев бронирования. Сервисы используют Domain и абстракции репозиториев/внешних зависимостей. На этом этапе не реализовывать DataAccess, Infrastructure и HTTP API.

---

## 2. Исследование существующего кода

### 2.1. Структура Solution и проектов

| Проект | .csproj путь | Назначение | Ссылки на |
|---|---|---|---|
| `MeetSpace.Domain` | `Backend/src/MeetSpace.Domain/` | Сущности, правила, перечисления | Нет (без зависимостей) |
| `MeetSpace.Application` | `Backend/src/MeetSpace.Application/` | Бизнес-логика, абстракции, Options | `MeetSpace.Domain` + пакеты `Microsoft.Extensions.Options`, `DI.Abstractions` |
| `MeetSpace.Infrastructure` | `Backend/src/MeetSpace.Infrastructure/` | Реализации внешних сервисов | Пакет `Microsoft.Extensions.Options` (не ссылается на Domain/Application!) |
| `MeetSpace.Api` | `Backend/src/MeetSpace.Api/` | Точка входа, DI, HTTP | `MeetSpace.Application`, `MeetSpace.Infrastructure` |
| `MeetSpace.Tests` | `Backend/tests/MeetSpace.Tests/` | Тесты (xUnit) | Все 4 проекта |

**Важно:** `MeetSpace.Infrastructure` не ссылается на `MeetSpace.Application` и `MeetSpace.Domain` — это означает, что Infrastructure изолирован и в него нельзя добавлять реализации, зависящие от Domain-типов без добавления ссылок на проекты. Для DEV-005 это не критично, так как реализации репозиториев будут в DataAccess (ещё не создан), а реализации `ICurrentUserContext`/`ITimeProvider` — в Infrastructure (позже).

### 2.2. Сущности Domain

#### Booking (`MeetSpace.Domain.Entities.Booking`)

```csharp
public class Booking
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoomId { get; private set; }
    public DateTime Date { get; private set; }           // Дата: DateTimeKind.Unspecified, время = 00:00:00
    public TimeSpan StartTime { get; private set; }      // Время начала
    public TimeSpan EndTime { get; private set; }        // Время окончания

    public User User { get; private set; } = null!;
    public Room Room { get; private set; } = null!;
}
```

- **Конструктор:** `Booking(title, userId, roomId, date, startTime, endTime, BookingTimeRules rules, int titleMaxLength, string? description, int? descriptionMaxLength)`
- **Методы мутации:**
  - `SetTitle(title, maxLength)` — валидация длины
  - `SetDescription(description, maxLength)` — валидация длины
  - `SetDate(date)` — нормализует `DateTimeKind` к `Unspecified`, отсекает время
  - `SetTimeInterval(startTime, endTime, rules)` — все проверки рабочего времени и шага
- **EF-совместимость:** private-конструктор без параметров присутствует
- **Инварианты Domain (уже реализованы):**
  - `EndTime > StartTime`
  - `startTime >= WorkingHoursStart`
  - `endTime <= WorkingHoursEnd`
  - Кратность минут `StartTime` и `EndTime` шагу из `BookingTimeRules.TimeStepMinutes`

#### Room (`MeetSpace.Domain.Entities.Room`)

```csharp
public class Room
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public int Floor { get; private set; }
    public string? Description { get; private set; }
    public string? PhotoKey { get; private set; }
    public ICollection<RoomEquipment> RoomEquipments { get; private set; }
    public ICollection<Booking> Bookings { get; private set; }
}
```

#### User (`MeetSpace.Domain.Entities.User`)

```csharp
public class User
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string? ProfilePhotoKey { get; private set; }
    public UserRole Role { get; private set; }            // UserRole.User (1) или UserRole.Manager (2)
    public ICollection<Booking> Bookings { get; private set; }
    public ICollection<RefreshToken> RefreshTokens { get; private set; }
}
```

#### UserRole (`MeetSpace.Domain.Enums.UserRole`)

```csharp
public enum UserRole { User = 1, Manager = 2 }
```

#### BookingTimeRules (`MeetSpace.Domain.Rules.BookingTimeRules`)

```csharp
public class BookingTimeRules
{
    public TimeSpan WorkingHoursStart { get; init; }
    public TimeSpan WorkingHoursEnd { get; init; }
    public int TimeStepMinutes { get; init; }
}
```

**Примечание:** `BookingTimeRules` фактически находится в **Domain**, а не в Application, как планировалось в DEV-003. Это допустимо — класс не зависит от `IConfiguration`/`IOptions`, является plain C# объектом. В DEV-005 будем собирать его из `IOptions<BookingOptions>` в Application-сервисе и передавать в `Booking` конструктор/сеттеры.

### 2.3. Настройки/Options

#### BookingOptions (`MeetSpace.Application.Options`)

```csharp
public class BookingOptions
{
    public TimeSpan WorkingHoursStart { get; set; }       // 09:00:00
    public TimeSpan WorkingHoursEnd { get; set; }         // 18:00:00
    public int BookingTimeStepMinutes { get; set; }       // 15
}
```

Зарегистрированы через `AddMeetSpaceOptions`, валидируются при старте.

#### ValidationOptions (`MeetSpace.Application.Options`)

```csharp
public class ValidationOptions
{
    public int BookingTitleMaxLength { get; set; }        // 200
    public int BookingDescriptionMaxLength { get; set; }  // 2000
    // ... остальные поля для User, Room, Equipment, Email
}
```

#### StringLengthRules (`MeetSpace.Application.Rules`)

```csharp
public class StringLengthRules
{
    public int BookingTitleMaxLength { get; init; }
    public int BookingDescriptionMaxLength { get; init; }
    // ... остальные MaxLength-поля
}
```

Передаётся в сущности как параметр для валидации длины строк.

### 2.4. Абстракции в Application

**На данный момент в Application нет ни одной абстракции (интерфейса).** Есть только:
- `Options/BookingOptions.cs`
- `Options/ValidationOptions.cs`
- `Rules/StringLengthRules.cs`

Нет интерфейсов репозиториев, нет `ICurrentUserContext`, нет `ITimeProvider`, нет сервисов.

### 2.5. Обработка ошибок

**На данный момент в проекте нет пользовательских исключений.** Domain-сущности используют стандартные `ArgumentException` / `ArgumentNullException` для валидации инвариантов. Для DEV-005 необходимо создать собственные типы исключений в Application, различающие бизнес-ситуации:
- `NotFoundException` — комната/бронирование не найдены
- `AccessDeniedException` — недостаточно прав
- `ValidationException` — невалидные данные (время в прошлом, некорректный формат и т.д.)
- `BookingConflictException` — конфликт пересечения броней

### 2.6. DI и регистрация

- `Program.cs`: `builder.Services.AddMeetSpaceOptions(builder.Configuration)` — регистрирует все 5 групп Options
- Extension-метод: `MeetSpace.Api.Extensions.OptionsServiceCollectionExtensions.AddMeetSpaceOptions`
- Все Options валидируются при старте через `ValidateOnStart()`
- Тикет DEV-005 **не требует** регистрации новых сервисов в DI (это будет отдельный этап связывания)

---

## 3. Проектирование Application-сервисов

### 3.1. Общий подход

- Создаётся один сервис `BookingService` в `MeetSpace.Application`.
- Сервис получает зависимости через конструктор (DI-ready).
- Все методы асинхронные с `CancellationToken`.
- Входные модели — простые DTO/record в Application.
- Выходные модели — DTO без ссылок на Domain-сущности (кроме `BookingDto`, который может содержать вложенные данные, если они согласованы).
- Бизнес-правила валидации времени используют `BookingTimeRules`, собранный из `IOptions<BookingOptions>`.
- Ограничения длины строк — из `StringLengthRules`, собранного из `IOptions<ValidationOptions>`.
- Идентификатор пользователя и роль получаются из `ICurrentUserContext`, **не** из параметров запроса.

### 3.2. BookingService — методы

```csharp
namespace MeetSpace.Application.Services;

public class BookingService
{
    public BookingService(
        IRoomRepository roomRepository,
        IBookingRepository bookingRepository,
        ICurrentUserContext currentUserContext,
        ITimeProvider timeProvider,
        IOptions<BookingOptions> bookingOptions,
        IOptions<ValidationOptions> validationOptions)
    { /* ... */ }

    // Создание бронирования текущим пользователем
    public Task<BookingDto> CreateBookingAsync(
        BookingRequestDto request,
        CancellationToken ct = default);

    // Изменение брони (User — только своей; Manager — любой)
    public Task<BookingDto> UpdateBookingAsync(
        Guid bookingId,
        BookingRequestDto request,
        CancellationToken ct = default);

    // Удаление брони (User — только своей; Manager — любой)
    public Task DeleteBookingAsync(
        Guid bookingId,
        CancellationToken ct = default);

    // Получение списка броней текущего пользователя
    public Task<IReadOnlyCollection<BookingDto>> GetMyBookingsAsync(
        CancellationToken ct = default);

    // Получение броней календаря за диапазон дат
    public Task<IReadOnlyCollection<CalendarBookingDto>> GetCalendarBookingsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    // Проверка доступности комнаты для интервала
    public Task<AvailabilityResult> CheckAvailabilityAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        CancellationToken ct = default);
}
```

### 3.3. Пошаговые сценарии

#### 3.3.1. CreateBookingAsync

```
1. Получить текущего пользователя: currentUserContext.GetCurrentUserIdAsync()
   — если пользователь не аутентифицирован → исключение (UnauthorizedException или AccessDeniedException)
2. Проверить существование комнаты: roomRepository.ExistsAsync(request.RoomId, ct)
   — если нет → NotFoundException("Room not found")
3. Построить BookingTimeRules из BookingOptions
4. Построить maxLength из ValidationOptions → StringLengthRules
5. Валидация времени:
   a. Проверить, что date.Date (нормализованный) >= timeProvider.GetCurrentTime().Date
      — если в прошлом → ValidationException("Cannot book in the past")
   b. Построить Booking (конструктор сам проверит инварианты: end>start, рабочие часы, шаг)
      — при ArgumentException → обернуть в ValidationException
6. Проверить доступность: bookingRepository.FindOverlappingAsync(roomId, date, startTime, endTime, excludeBookingId: null, ct)
   — если есть пересечения → BookingConflictException
7. Сохранить: bookingRepository.AddAsync(booking, ct)
8. Вернуть BookingDto (отмапленный из сохранённой сущности)
```

#### 3.3.2. UpdateBookingAsync

```
1. Получить текущего пользователя
2. Загрузить существующую бронь: bookingRepository.GetByIdAsync(bookingId, ct)
   — если нет → NotFoundException("Booking not found")
3. Проверить права:
   — если currentUser.Role != Manager && booking.UserId != currentUser.Id → AccessDeniedException
4. Проверить существование комнаты (если комната изменилась)
5. Построить BookingTimeRules, maxLength
6. Валидация времени:
   a. date >= today (аналогично созданию)
   b. Обновить поля через сеттеры сущности: SetTitle, SetDescription, SetDate, SetTimeInterval
7. Проверить доступность: bookingRepository.FindOverlappingAsync(roomId, date, startTime, endTime, excludeBookingId: bookingId, ct)
   — исключаем текущую бронь из проверки
   — если есть пересечения → BookingConflictException
8. Сохранить: bookingRepository.UpdateAsync(booking, ct)
9. Вернуть BookingDto
```

#### 3.3.3. DeleteBookingAsync

```
1. Получить текущего пользователя
2. Загрузить бронь: bookingRepository.GetByIdAsync(bookingId, ct)
   — если нет → NotFoundException
3. Проверить права: Manager или владелец
4. Удалить: bookingRepository.DeleteAsync(booking, ct)
```

#### 3.3.4. GetMyBookingsAsync

```
1. Получить currentUserId
2. Загрузить: bookingRepository.GetByUserIdAsync(currentUserId, ct)
3. Вернуть список BookingDto
```

#### 3.3.5. GetCalendarBookingsAsync

```
1. Получить текущего пользователя (для определения видимости)
2. Загрузить: bookingRepository.GetByDateRangeAsync(startDate, endDate, ct)
3. Для каждой брони построить CalendarBookingDto:
   — Свои брони: полная информация (title, description, user name, room name)
   — Чужие брони: ограниченная видимость (только факт занятости — roomId, date, startTime, endTime)
     Конкретный состав уточнить у владельца, но как минимум:
     - Для своих: id, title, description, userId, roomId, roomName, date, startTime, endTime
     - Для чужих: id, roomId, date, startTime, endTime (без title/description/user)
4. Вернуть список
```

#### 3.3.6. CheckAvailabilityAsync

```
1. Проверить существование комнаты
2. Проверить, что date >= today
3. Проверить корректность интервала через BookingTimeRules (end>start, рабочие часы, шаг)
4. bookingRepository.FindOverlappingAsync(roomId, date, startTime, endTime, excludeBookingId: null, ct)
5. Вернуть AvailabilityResult { IsAvailable: bool, ConflictingBookings?: ... }
   (Конкретный состав — только факт доступности + опционально список конфликтующих)
```

---

## 4. Модели данных (DTO)

Все модели размещаются в `MeetSpace.Application/Models/`.

### 4.1. BookingRequestDto

```csharp
// MeetSpace.Application/Models/BookingRequestDto.cs
public class BookingRequestDto
{
    public Guid RoomId { get; init; }
    public DateTime Date { get; init; }            // Только дата, время игнорируется / нормализуется
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}
```

**Примечания:**
- `userId` не принимается — он получается из `ICurrentUserContext`
- `Date` должен быть нормализован: `.Date` отбрасывает время, `DateTimeKind` → `Unspecified`
- Все поля обязательны при создании; при редактировании используется та же модель
- Валидация на null/whitespace для Title — в сервисе

### 4.2. BookingDto

```csharp
// MeetSpace.Application/Models/BookingDto.cs
public class BookingDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid UserId { get; init; }
    public string UserFirstName { get; init; } = string.Empty;
    public string UserLastName { get; init; } = string.Empty;
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}
```

**Примечания:**
- Не содержит `PasswordHash`, `RefreshTokens`, навигационные коллекции
- Включает минимальную информацию о пользователе (имя) и комнате (название) для отображения в UI
- Возвращается для своих броней (GetMyBookings, Create/Update результат)

### 4.3. CalendarBookingDto

```csharp
// MeetSpace.Application/Models/CalendarBookingDto.cs
public class CalendarBookingDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }

    // Поля видны только владельцу брони (или Manager):
    public string? Title { get; init; }            // null для чужих
    public string? Description { get; init; }      // null для чужих
    public string? UserFirstName { get; init; }    // null для чужих
    public string? UserLastName { get; init; }     // null для чужих
    public bool IsOwn { get; init; }               // true, если текущий пользователь — владелец
}
```

**Примечания:**
- `IsOwn` позволяет фронтенду определить, как отображать бронь (свой/чужой цвет)
- Поля `Title`, `Description`, `UserFirstName`, `UserLastName` заполняются только для своих броней или для Manager. Для чужих — `null`.
- Это обеспечивает privacy: обычный пользователь видит только занятость слотов, но не детали чужих броней.

### 4.4. AvailabilityResult

```csharp
// MeetSpace.Application/Models/AvailabilityResult.cs
public class AvailabilityResult
{
    public bool IsAvailable { get; init; }
    public IReadOnlyCollection<ConflictingBookingInfo>? Conflicts { get; init; }

    public static AvailabilityResult Available() =>
        new() { IsAvailable = true, Conflicts = null };

    public static AvailabilityResult Conflicted(IReadOnlyCollection<ConflictingBookingInfo> conflicts) =>
        new() { IsAvailable = false, Conflicts = conflicts };
}

public class ConflictingBookingInfo
{
    public Guid BookingId { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}
```

---

## 5. Интерфейсы зависимостей (в Application)

Все интерфейсы размещаются в `MeetSpace.Application/Abstractions/`.

### 5.1. IRoomRepository

```csharp
// MeetSpace.Application/Abstractions/IRoomRepository.cs
namespace MeetSpace.Application.Abstractions;

public interface IRoomRepository
{
    /// <summary>Проверить существование комнаты по идентификатору</summary>
    Task<bool> ExistsAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Получить комнату по идентификатору (только для чтения)</summary>
    Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default);
}
```

**Обоснование:**
- `ExistsAsync` — лёгкая проверка (может реализовываться как `SELECT 1` или `AnyAsync`)
- `GetByIdAsync` — для случаев, когда нужна информация о комнате (название, вместимость)
- Не включаем `GetAll`, фильтрацию и т.д. — это не нужно сценариям DEV-005
- Возвращает Domain-сущность `Room` (Application имеет право ссылаться на Domain)

### 5.2. IBookingRepository

```csharp
// MeetSpace.Application/Abstractions/IBookingRepository.cs
namespace MeetSpace.Application.Abstractions;

public interface IBookingRepository
{
    /// <summary>Получить бронь по Id (с пользователем и комнатой)</summary>
    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken ct = default);

    /// <summary>Получить все брони пользователя</summary>
    Task<IReadOnlyCollection<Booking>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Получить брони за диапазон дат (с пользователем и комнатой)</summary>
    Task<IReadOnlyCollection<Booking>> GetByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>Найти пересекающиеся брони для комнаты (исключая указанную)</summary>
    Task<IReadOnlyCollection<Booking>> FindOverlappingAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeBookingId,
        CancellationToken ct = default);

    /// <summary>Добавить новую бронь</summary>
    Task AddAsync(Booking booking, CancellationToken ct = default);

    /// <summary>Обновить существующую бронь</summary>
    Task UpdateAsync(Booking booking, CancellationToken ct = default);

    /// <summary>Удалить бронь</summary>
    Task DeleteAsync(Booking booking, CancellationToken ct = default);
}
```

**Обоснование методов:**
- `GetByIdAsync` — загрузка для update/delete, с навигационными свойствами (User, Room) для проверки прав и построения DTO
- `GetByUserIdAsync` — список броней текущего пользователя
- `GetByDateRangeAsync` — календарь; с User и Room для построения CalendarBookingDto
- `FindOverlappingAsync` — проверка пересечений с исключением текущей брони при редактировании
- `AddAsync`, `UpdateAsync`, `DeleteAsync` — мутирующие операции

**Требование к будущей реализации (DataAccess):**
- `FindOverlappingAsync` должен выполняться в одной транзакции с последующей записью (`AddAsync`/`UpdateAsync`) для предотвращения гонки при конкурентном бронировании. Конкретный механизм (транзакция EF Core, `SELECT ... FOR UPDATE`, оптимистическая блокировка через `ConcurrencyToken`) определяется в тикете DataAccess.
- `GetByDateRangeAsync` и `GetByUserIdAsync` должны использовать `.Include()` или projection для загрузки User и Room без N+1.
- Все методы возвращают Domain-сущности `Booking`. При реализации важно, чтобы навигационные свойства `User` и `Room` были загружены там, где они нужны (GetById, GetByDateRange, GetByUserId), но необязательно в FindOverlappingAsync.

### 5.3. ICurrentUserContext

```csharp
// MeetSpace.Application/Abstractions/ICurrentUserContext.cs
namespace MeetSpace.Application.Abstractions;

public interface ICurrentUserContext
{
    /// <summary>Идентификатор текущего пользователя (null если не аутентифицирован)</summary>
    Task<Guid?> GetCurrentUserIdAsync(CancellationToken ct = default);

    /// <summary>Роль текущего пользователя (null если не аутентифицирован)</summary>
    Task<UserRole?> GetCurrentUserRoleAsync(CancellationToken ct = default);
}
```

**Обоснование:**
- Возвращает `null`, если пользователь не аутентифицирован — Application сам решает, выбрасывать ли ошибку
- Асинхронный — учитывает, что реализация может требовать запроса к БД (хотя простая реализация из HttpContext — синхронная, сигнатура позволяет оба варианта)
- Не зависит от `HttpContext`, `ClaimsPrincipal`, `IHttpContextAccessor`
- `CancellationToken` позволяет прервать операцию

### 5.4. ITimeProvider

```csharp
// MeetSpace.Application/Abstractions/ITimeProvider.cs
namespace MeetSpace.Application.Abstractions;

public interface ITimeProvider
{
    /// <summary>Текущее локальное время (без таймзоны согласно goal.md)</summary>
    DateTime GetCurrentTime();
}
```

**Обоснование:**
- `GetCurrentTime()` возвращает локальное время без таймзоны (`DateTimeKind.Unspecified`)
- Синхронный метод — получение времени не требует I/O
- В production-реализации: `DateTime.Now` (или `DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)`)
- Тестовая реализация возвращает фиксированное время
- **Важно:** дата и время бронирования — без таймзоны согласно goal.md. Сравнение с текущим временем должно происходить в том же «безтаймзонном» домене. Не смешивать UTC и локальное время.

### 5.5. Настройки (уже существуют, переиспользуются)

- `IOptions<BookingOptions>` — для построения `BookingTimeRules` (WorkingHoursStart, WorkingHoursEnd, TimeStepMinutes)
- `IOptions<ValidationOptions>` — для построения `StringLengthRules` (BookingTitleMaxLength, BookingDescriptionMaxLength)

Эти типы уже находятся в `MeetSpace.Application.Options` и переиспользуются без изменений.

---

## 6. Ошибки (пользовательские исключения)

Все исключения размещаются в `MeetSpace.Application/Exceptions/`.

### 6.1. Иерархия

```
MeetSpace.Application.Exceptions
├── ApplicationException (базовый абстрактный)
│   ├── NotFoundException
│   ├── AccessDeniedException
│   ├── ValidationException
│   └── BookingConflictException
```

### 6.2. Определения

```csharp
// MeetSpace.Application/Exceptions/ApplicationException.cs
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
    protected ApplicationException(string message, Exception inner) : base(message, inner) { }
}

// MeetSpace.Application/Exceptions/NotFoundException.cs
public class NotFoundException : ApplicationException
{
    public string EntityName { get; }
    public Guid EntityId { get; }

    public NotFoundException(string entityName, Guid entityId)
        : base($"{entityName} with id '{entityId}' not found.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}

// MeetSpace.Application/Exceptions/AccessDeniedException.cs
public class AccessDeniedException : ApplicationException
{
    public AccessDeniedException(string message = "Access denied.")
        : base(message) { }
}

// MeetSpace.Application/Exceptions/ValidationException.cs
public class ValidationException : ApplicationException
{
    public ValidationException(string message)
        : base(message) { }

    public ValidationException(string message, Exception inner)
        : base(message, inner) { }
}

// MeetSpace.Application/Exceptions/BookingConflictException.cs
public class BookingConflictException : ApplicationException
{
    public Guid RoomId { get; }
    public DateTime Date { get; }
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public BookingConflictException(Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
        : base($"Room '{roomId}' is already booked on {date:yyyy-MM-dd} from {startTime:hh\\:mm} to {endTime:hh\\:mm}.")
    {
        RoomId = roomId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
    }
}
```

**Примечание по неймингу:** Имя `ApplicationException` совпадает с `System.ApplicationException`, но размещение в `MeetSpace.Application.Exceptions` + использование namespace-квалификации разрешает коллизию. Альтернатива: `AppException`, `MeetSpaceException`. Оставляем `ApplicationException` — контекст использования внутри Application-слоя делает ясным, что это базовый класс бизнес-исключений приложения, а не системный.

**Примечание по NotFoundException:** Конструктор принимает `entityName` и `entityId` для формирования информативного сообщения. Дополнительный конструктор с кастомным сообщением может быть добавлен при необходимости.

### 6.3. Соответствие ошибок и сценариев

| Ошибка | Сценарии | Условие |
|---|---|---|
| `NotFoundException` | Update, Delete, CheckAvailability | Комната или бронь не найдены |
| `AccessDeniedException` | Update, Delete | Роль User, бронь не своя; или неаутентифицированный пользователь |
| `ValidationException` | Create, Update, CheckAvailability | Время в прошлом, невалидный интервал (оборачивает ArgumentException из Domain) |
| `BookingConflictException` | Create, Update | Найдены пересекающиеся брони для той же комнаты и даты |

---

## 7. Бизнес-правила (сводка)

### 7.1. Рабочее время и шаг

- Источник: `BookingOptions` → `BookingTimeRules`
- Проверяются в `Booking.SetTimeInterval()` (Domain), поэтому в Application не дублируются
- Application-сервис перехватывает `ArgumentException` из Domain и оборачивает в `ValidationException`

### 7.2. Интервалы [start, end)

- Соседние брони с `other.EndTime == this.StartTime` или `this.EndTime == other.StartTime` **допустимы** (не пересекаются)
- Доказательство правильности формулы: `existing.Start < requested.End && requested.Start < existing.End`
  - Если `other.End == this.Start`, то `requested.Start < existing.End` → `this.Start < other.End` → `true`, но `existing.Start < requested.End` → `other.Start < this.End` → зависит от other.Start. При равенстве границ: `other.End == this.Start`, значит `other.Start < other.End == this.Start < this.End`, поэтому `other.Start < this.End` → `true` и `other.Start < this.End` → `true` — значит формула даст `true` для равенства границ... 
  - **Проверка:** existing: [09:00, 10:00), requested: [10:00, 11:00). Тогда `existing.Start(09:00) < requested.End(11:00)` → true; `requested.Start(10:00) < existing.End(10:00)` → false. Результат: false (нет пересечения). ✅
  - **Проверка с перекрытием:** existing: [09:00, 10:30), requested: [10:00, 11:00). Тогда оба условия true → пересечение. ✅

### 7.3. Пересечение

Формула для той же комнаты и даты:
```
existing.Start < requested.End && requested.Start < existing.End
```
- RoomId должен совпадать
- Date должна совпадать
- Исключение текущей брони при редактировании (по bookingId)
- Не исключать чужие записи

### 7.4. Проверка на прошлое время

- `ITimeProvider.GetCurrentTime()` возвращает текущее локальное время (без таймзоны)
- `date.Date < currentTime.Date` → нельзя бронировать в прошлом
- `date.Date == currentTime.Date && startTime <= currentTime.TimeOfDay` — строго говоря, goal.md говорит «нельзя бронировать в прошлом». Интерпретация: нельзя начать бронь в прошлом, т.е. `date.Date < today || (date.Date == today && startTime <= now.TimeOfDay)`. Но тикет уточняет: «Нельзя создать или перенести бронь на прошедшее время. Не запрещать автоматически удаление прошлой брони». Значит проверка только при создании и редактировании.

### 7.5. Права доступа

| Операция | User | Manager |
|---|---|---|
| Создание | Может (от своего имени) | Может (от своего имени) |
| Изменение | Только своей брони | Любой брони |
| Удаление | Только своей брони | Любой брони |
| Просмотр своих | Свои | Свои |
| Просмотр календаря | Все брони (чужие с ограниченной видимостью) | Все брони (полная информация) |
| Проверка доступности | Любой комнаты | Любой комнаты |

**Примечание:** Manager при создании брони создаёт её от своего имени, а не от имени другого пользователя. Если потребуется функциональность «создать бронь от имени пользователя» — это отдельный тикет.

### 7.6. Детерминированное время

- `ITimeProvider` — единственный источник текущего времени
- В тестах подменяется на фиксированное значение
- Сравнение дат: только `.Date` (без времени и таймзоны)

---

## 8. Контракты сохранения

### 8.1. Требования к реализации DataAccess

1. **Загрузка с навигационными свойствами:** `GetByIdAsync`, `GetByUserIdAsync`, `GetByDateRangeAsync` должны загружать `User` и `Room` (например, через `.Include()` в EF Core) без N+1 запросов.
2. **Проверка пересечений:** `FindOverlappingAsync` реализует SQL/LINQ-запрос с фильтром по `RoomId`, `Date` и условием `StartTime < @end AND EndTime > @start`. Исключение по `bookingId` через `Id != @excludeBookingId`.
3. **Атомарность:** Проверка пересечений и вставка/обновление должны быть в одной транзакции. В DEV-005 это только документируется как требование, не реализуется и не тестируется.
4. **Возврат после сохранения:** `AddAsync`/`UpdateAsync` должны сохранять изменения и возвращать управление. Повторная загрузка сущности не требуется — Application-сервис использует уже модифицированный объект в памяти.
5. **Не использовать IQueryable:** Репозитории возвращают готовые коллекции/сущности, а не `IQueryable`. Application не должен знать о существовании EF Core.

### 8.2. Защита от конкурентных запросов

**Проблема:** проверка «нет пересечения» перед сохранением сама по себе не гарантирует отсутствие гонки при параллельных запросах. Два одновременных запроса могут оба увидеть «свободно» и оба записать бронь на один слот.

**Решение (документируется для DataAccess):**
- Использовать транзакцию с уровнем изоляции `Serializable` или `RepeatableRead`
- Либо использовать оптимистическую блокировку через `ConcurrencyToken` / `RowVersion`
- Либо `SELECT ... FOR UPDATE` для комнаты/брони на время транзакции
- Конкретный механизм выбрать при реализации DataAccess

**В DEV-005:** это требование **не реализуется и не тестируется**. В отчёте явно указано, что гарантия конкурентной записи — ответственность будущего DataAccess-тикета.

---

## 9. Состав файлов в Application

### 9.1. Новые файлы

| Файл | Назначение |
|---|---|
| `Abstractions/IRoomRepository.cs` | Интерфейс репозитория комнат |
| `Abstractions/IBookingRepository.cs` | Интерфейс репозитория бронирований |
| `Abstractions/ICurrentUserContext.cs` | Интерфейс контекста текущего пользователя |
| `Abstractions/ITimeProvider.cs` | Интерфейс провайдера времени |
| `Exceptions/ApplicationException.cs` | Базовое бизнес-исключение |
| `Exceptions/NotFoundException.cs` | Исключение «не найдено» |
| `Exceptions/AccessDeniedException.cs` | Исключение «доступ запрещён» |
| `Exceptions/ValidationException.cs` | Исключение «невалидные данные» |
| `Exceptions/BookingConflictException.cs` | Исключение «конфликт бронирования» |
| `Models/BookingRequestDto.cs` | Входная модель запроса на бронирование |
| `Models/BookingDto.cs` | Выходная модель бронирования |
| `Models/CalendarBookingDto.cs` | Модель бронирования для календаря |
| `Models/AvailabilityResult.cs` | Результат проверки доступности |
| `Services/BookingService.cs` | Сервис бизнес-логики бронирований |

### 9.2. Существующие файлы (без изменений)

| Файл | Причина неизменности |
|---|---|
| `Options/BookingOptions.cs` | Переиспользуется через `IOptions<T>` |
| `Options/ValidationOptions.cs` | Переиспользуется через `IOptions<T>` |
| `Rules/StringLengthRules.cs` | Переиспользуется — собирается в сервисе из ValidationOptions |

---

## 10. Изменения Domain (не требуются)

Существующие Domain-сущности и правила полностью покрывают потребности DEV-005:

- **Booking:** конструктор + сеттеры с `BookingTimeRules` и `maxLength` уже готовы
- **Room:** существует, навигационное свойство `Bookings` есть
- **User:** существует, навигационное свойство `Bookings` есть
- **BookingTimeRules:** находится в Domain (а не Application, как планировалось) — это допустимо, класс не зависит от ASP.NET
- **UserRole:** перечисление готово

**Изменения Domain не требуются.**

---

## 11. DI-регистрация (отложена)

В рамках DEV-005 не требуется регистрировать `BookingService` и другие новые типы в DI-контейнере. Это будет сделано в отдельном тикете связывания, когда появятся реализации репозиториев (DataAccess) и инфраструктурных сервисов (Infrastructure).

В тестах сервис создаётся вручную с fake/mock-зависимостями через конструктор.

---

## 12. Совместимость с предыдущими тикетами

| DEV-003 | DEV-004 | DEV-005 |
|---|---|---|
| Options-классы в Application и Infrastructure | Extension-метод регистрации в Api | Application-сервисы и абстракции |
| `BookingOptions`, `ValidationOptions` в Application | `AddMeetSpaceOptions` в Api | `BookingService` использует `IOptions<BookingOptions>` и `IOptions<ValidationOptions>` |
| `StringLengthRules` в Application | | `BookingService` собирает `StringLengthRules` из `ValidationOptions` |
| `BookingTimeRules` в Domain | | `BookingService` собирает `BookingTimeRules` из `BookingOptions` |

Все решения DEV-003 и DEV-004 остаются без изменений. DEV-005 только добавляет новые файлы в Application и тесты.

---

## 13. Отчёт архитектора

### Что изучено

1. **Кодовая база:**
   - Все 5 проектов solution: Domain, Application, Infrastructure, Api, Tests
   - Структура и зависимости проектов (`.csproj`)
   - Все 6 Domain-сущностей: `Booking`, `Room`, `User`, `Equipment`, `RoomEquipment`, `RefreshToken`
   - Перечисление `UserRole` (User=1, Manager=2)
   - Правило `BookingTimeRules` в Domain
   - Опции: `BookingOptions` (Application), `ValidationOptions` (Application), `JwtOptions`/`PasswordHashingOptions`/`S3Options` (Infrastructure)
   - Правило `StringLengthRules` в Application
   - Extension-метод `AddMeetSpaceOptions` в Api
   - `Program.cs` с единственным эндпоинтом `/health`

2. **Архитектурные документы:**
   - `docs/domain-model.md` — исходная модель предметной области
   - `docs/architecture/DEV-003-typed-options.md` — решение по Options и правилам
   - `docs/architecture/DEV-004-options-registration-extension.md` — вынесение регистрации в extension

3. **Тесты:**
   - `BookingTimeRulesConsumerTests` — тестирует `SetTimeInterval` с кастомными правилами
   - `BookingOptionsValidatorTests` — валидация BookingOptions
   - `OptionsRegistrationExtensionTests` — интеграционный тест регистрации всех Options
   - Используется xUnit, `GlobalUsings.cs` с `global using Xunit;`

4. **Состояние ветки:**
   - Текущая ветка: `feature/dev-005-booking-logic`
   - `git status`: только `tickets/DEV-005-booking-application-logic.md` (untracked) — посторонних изменений нет

### Какие решения приняты

1. **Один сервис `BookingService`** — 6 методов, покрывающих все сценарии. Разделение на несколько сервисов (`BookingCreator`, `BookingUpdater` и т.д.) было бы избыточным для текущего масштаба.

2. **Модели CalendarBookingDto** — разная видимость для своих/чужих броней через nullable-поля и флаг `IsOwn`. Принято решение: для чужих броней скрывать Title, Description, UserFirstName, UserLastName (оставлять null). Manager видит все поля.

3. **Исключения** — 4 пользовательских типа: `NotFoundException`, `AccessDeniedException`, `ValidationException`, `BookingConflictException`. Базовый класс `ApplicationException`. Размещение в `MeetSpace.Application.Exceptions`.

4. **ITimeProvider.GetCurrentTime()** — синхронный, возвращает локальное время без таймзоны (`DateTimeKind.Unspecified`). Это соответствует goal.md («дата и время хранятся без таймзоны») и позволяет детерминированное тестирование.

5. **ICurrentUserContext** — асинхронный (`Task<Guid?>` / `Task<UserRole?>`), возвращает `null` при отсутствии аутентификации. Application сам решает, что делать.

6. **IRoomRepository** — минимальный: `ExistsAsync` и `GetByIdAsync`. Только то, что нужно сценариям DEV-005.

7. **IBookingRepository** — 7 методов: CRUD + выборка по пользователю + выборка по диапазону дат + поиск пересечений. Возвращает Domain-сущности (не DTO, не IQueryable).

8. **Не вводим UoW** — нет необходимости, транзакционность будет обеспечена через механизмы EF Core в DataAccess.

9. **Не вводим MediatR** — как указано в тикете. Прямые вызовы методов сервиса.

10. **Проверка на прошлое время** — сравнение с `ITimeProvider.GetCurrentTime().Date`. Не запрещаем удаление прошлых броней.

### Нужны ли изменения Domain

**Нет.** Все существующие Domain-сущности, правила и перечисления полностью покрывают потребности DEV-005. Конструкторы и сеттеры `Booking` уже принимают `BookingTimeRules` и `maxLength`. Новые инварианты (проверка на прошлое, проверка пересечений) реализуются в Application и DataAccess.

### Открытые вопросы

1. **CalendarBookingDto для чужих броней — точный состав.** Принято решение: `Title`, `Description`, `UserFirstName`, `UserLastName` = `null` для чужих, `IsOwn = false`. Если владелец хочет полностью скрывать даже roomId/roomName для чужих — уточнить до реализации. Текущее решение: чужие брони видны как занятые слоты (roomId, roomName, date, startTime, endTime), без деталей.

2. **Имя базового исключения `ApplicationException`.** Совпадает с `System.ApplicationException`. В рамках слоя Application коллизия управляема через namespace-квалификацию. Если команда предпочитает избежать коллизии — можно переименовать в `MeetSpaceException` или `AppException`. Пока оставлено `ApplicationException`.

3. **Manager создаёт бронь от своего имени.** Принято решение: Manager не может создавать брони от имени других пользователей в рамках текущего API. Если потребуется — отдельный тикет.

4. **Конкурентная запись.** Подтверждено: гарантия конкурентной записи — ответственность DataAccess, в DEV-005 не реализуется и не тестируется.

### Статус тикета

Установлен: **В работе**

---

## 14. Проверка соблюдения границ

| Граница | Статус | Комментарий |
|---|---|---|
| Не использовать IConfiguration в контрактах | ✅ | Сервис получает `IOptions<T>`, контракты — простые интерфейсы |
| Не использовать IOptions в абстракциях | ✅ | `IOptions<T>` используется только в реализации `BookingService`, не в интерфейсах |
| Не использовать HttpContext, ClaimsPrincipal | ✅ | `ICurrentUserContext` — абстракция |
| Не вводить MediatR | ✅ | Прямой вызов `BookingService` |
| Не вводить UoW/универсальный Repository | ✅ | Специфичные интерфейсы под сценарии |
| Асинхронные контракты с CancellationToken | ✅ | Все методы асинхронные с `CancellationToken ct = default` |
| Не менять существующий код | ✅ | Только новые файлы в Application + тесты |
| Application не зависит от DataAccess/Infrastructure | ✅ | Только абстракции в Application |
| Не проектировать JWT/S3/почту | ✅ | Только `ICurrentUserContext`, `ITimeProvider` |
| Не использовать EF Core / SDK хранилищ в контрактах | ✅ | Репозитории возвращают Domain-сущности, не IQueryable |

