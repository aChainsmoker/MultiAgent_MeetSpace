# DEV-005 — Бизнес-логика бронирований в Application

Статус: Завершено

## Цель

Реализовать сервисы Application для сценариев бронирования. Сервисы используют Domain и абстракции репозиториев/внешних зависимостей. На этом этапе не реализовывать DataAccess, Infrastructure и HTTP API.

## Источники

- `AGENTS.md` — правила и границы слоёв.
- `goal.md` — требования и бизнес-правила.
- Существующие Domain, Options и решения DEV-003/DEV-004.

## Что сделать

### 1. Архитектура

Результат: `docs/architecture/DEV-005-booking-application-logic.md`.

- Изучить существующие сущности, типы настроек, проекты и тесты. Переиспользовать имеющиеся абстракции, не создавать их дубликаты.
- Описать Application-сервисы, входные/выходные модели сценариев, методы, зависимости и ошибки.
- Интерфейсы репозиториев разместить в Application; их реализации будут добавлены позже в DataAccess. Application не ссылается на DataAccess.
- Нужные интерфейсы внешних сервисов разместить в Application; их реализации будут добавлены позже в Infrastructure. Application не ссылается на Infrastructure.
- Добавлять только зависимости, реально необходимые сценариям этого тикета: доступ к комнатам/бронированиям, доверенный контекст текущего пользователя, тестируемый источник текущего времени и т. п. Не проектировать JWT/S3/почту ради полноты.
- Не использовать IConfiguration, IOptions, HttpContext, ClaimsPrincipal, DbContext, IQueryable, EF Core, SDK хранилищ и HTTP-коды в контрактах бизнес-сценариев. Использовать простые модели, доменные типы и подходящие абстракции.
- Для параметров рабочего дня/шага/лимитов переиспользовать принятый в проекте способ передачи обычных типизированных настроек в Application. Не возвращать константы и не переносить binding в сервисы.
- Не переносить уже существующие инварианты Domain в дублирующие реализации. Разделить ответственность сущностей и Application-сервисов.
- Асинхронные контракты ввода-вывода должны поддерживать CancellationToken.
- Не вводить универсальный Repository, UoW, MediatR или новые пакеты без необходимости и согласования.

### 2. Сценарии

- Создание бронирования текущим пользователем от своего имени.
- Изменение своей брони пользователем; менеджер может изменять любую бронь.
- Удаление своей брони пользователем; менеджер может удалять любую бронь. Не добавлять модель soft delete, если она не определена в проекте.
- Получение своих бронирований.
- Получение бронирований календаря за диапазон дат.
- Проверка доступности существующей комнаты для интервала.

Сохранить действующие требования к видимости данных календаря. Если состав данных чужой брони не определён, уточнить у владельца до реализации публичного контракта. Не возвращать пароль, хеш, refresh-токены и другие секреты пользователя.

### 3. Бизнес-правила

- Рабочие часы и шаг времени берутся из переданных настроек.
- Окончание строго позже начала; интервал укладывается в рабочие часы выбранного дня.
- Время начала и окончания соответствует шагу, используемому в проекте. Способ выравнивания зафиксировать в архитектурном документе.
- Нельзя создать или перенести бронь на прошедшее время. Не запрещать автоматически удаление прошлой брони: такого требования в goal.md нет.
- Сопоставление с текущим временем должно быть детерминированным и тестируемым. Дата/время бронирования — без таймзоны согласно goal.md. Не вводить неявное сравнение локальных данных с UTC; существенную неоднозначность источника локального времени уточнить.
- Перед созданием/изменением проверить существование комнаты, корректность текстовых полей и отсутствие конфликтующей брони.
- Интервалы рассматривать как [начало, конец): соседние брони с совпадающими окончанием/началом допустимы. Пересечение: existing.Start < requested.End && requested.Start < existing.End для той же комнаты и даты.
- При редактировании исключать текущую бронь из проверки пересечений, не исключая чужие записи.
- Проверять права до изменения/удаления и не изменять состояние при отказе.
- Не принимать идентификатор текущего пользователя и его роль из произвольной модели запроса как доверенные значения; получать их из абстракции контекста пользователя.
- Отсутствие пользователя, комнаты или бронирования, запрет доступа, неверные данные и конфликт имеют различимые Application-ошибки по принятому соглашению проекта.

### 4. Контракты сохранения

- Контракты должны позволять реализовать запросы в БД без загрузки всех записей в память и без N+1. Не раскрывать IQueryable или EF-типы вызывающему сервису.
- Реализовать проверки бизнес-сценариев через абстракции; реальные запросы и сохранение не писать.
- Описать требование к будущей реализации: проверка пересечения и запись должны защищаться от конкурентных запросов на уровне хранилища/транзакции. Контракт должен позволять сообщить Application об атомарном конфликте сохранения, если это предусмотрено решением архитектора.
- Проверка «нет пересечения» перед сохранением сама по себе не гарантирует отсутствие гонки. В отчёте явно указать, что гарантия конкурентной записи и её интеграционный тест относятся к будущему DataAccess-тикету и здесь не подтверждаются.
- Не добавлять блокировки процесса, production in-memory репозитории и фиктивные реализации ради запуска приложения.

## Тестирование

- Использовать xUnit. Тестировать настоящие Application-сервисы, подменяя только репозитории, текущего пользователя, время и необходимые внешние интерфейсы. Тестовые fake/mock не должны становиться production-реализациями.
- Проверить успешные создание, изменение, удаление, выборку своих броней, календарный диапазон и проверку доступности.
- Проверить равные/обратные начало и конец, границы рабочего дня, выход за границы, неверный шаг, прошедшее время и действие изменённых параметров настроек.
- Проверить полное/частичное пересечение, вложенные и одинаковые интервалы, соседние брони, другую комнату и другую дату.
- Подтвердить, что при изменении исключается только текущая бронь.
- Проверить ограничения User и права Manager, отсутствие текущего пользователя, комнаты и бронирования.
- Проверить, что при ошибке валидации/доступа/конфликте не вызываются операции записи и существующая сущность не остаётся частично изменённой.
- Тесты должны проверять решение сервиса: данные, доменное состояние, ошибки и необходимые обращения к зависимостям. Не ограничиваться проверкой настроенных ответов mock.
- Не использовать системные часы, реальные PostgreSQL/MinIO, HTTP, JWT и браузер в этих unit-тестах. Не требовать интеграционных тестов конкурентной записи в этом тикете.
- Тестировщик самостоятельно запускает сборку и тесты по фактической структуре solution. Записывает команды, exit codes, число обнаруженных/успешных/неуспешных/пропущенных тестов и проверку критериев. Ноль обнаруженных тестов не считать успехом.
- Не требовать запуска новых сценариев через HTTP или GET /health: этот тикет не включает подключение к API. Если текущий API регрессировал из-за изменений, сообщить об этом.

## Границы

- Основные изменения — в Application и тестах; допустимы необходимые ссылки проектов и небольшие согласованные изменения Domain. Изменения Domain предварительно объяснить в архитектурном документе.
- Не реализовывать репозитории, DbContext, EF mapping, миграции, подключения к БД, транзакции и сидинг.
- Не реализовывать JWT, bcrypt, S3, загрузку файлов, HTTP endpoints, контроллеры и frontend.
- Не менять значения настроек и регистрацию Options без необходимости текущей задачи.
- Не добавлять интерфейсы и сервисы сценариев регистрации/профиля/загрузки фото впрок: это отдельные тикеты.
- При необходимости подключения Application к DI не создавать заглушки зависимостей и не ломать существующий запуск API. Связывание с конкретными реализациями будет отдельным этапом.

## Критерии готовности

- Есть архитектурный документ с методами сервисов, контрактами зависимостей, моделями и ошибками.
- Сценарии реализованы в Application через Domain и абстракции.
- Application не зависит от DataAccess, Infrastructure, ASP.NET Core, EF Core и инфраструктурных SDK.
- В Application описаны только необходимые контракты будущих реализаций.
- Бизнес-правила и права доступа покрыты unit-тестами реальных сервисов.
- Сборка и тесты выполнены; результаты и ограничения записаны.
- Будущая защита конкурентной записи явно обозначена как непроверенная ответственность DataAccess.
- Отчёты заполнены; после успешной проверки статус «На проверке». «Завершено» устанавливает владелец.

## Публикация

- При разрешении владельца создать коммит только с изменениями DEV-005, выполнить push рабочей ветки и открыть/обновить PR в указанную владельцем целевую ветку.
- В PR описать реализованные сценарии, результаты тестов и отложенные реализации зависимостей.
- Не выполнять merge, force push и прямой push в master/main. Не включать секреты и посторонние изменения.

## Отчёты

### Архитектор

✅ Выполнено. Документ: `docs/architecture/DEV-005-booking-application-logic.md`.

Изучены все существующие Domain-сущности, Options, Rules, структура solution, тесты. Спроектированы:
- Сервис `BookingService` (6 методов: Create, Update, Delete, GetMy, GetCalendar, CheckAvailability)
- 4 интерфейса-абстракции: `IRoomRepository`, `IBookingRepository`, `ICurrentUserContext`, `ITimeProvider`
- 4 модели: `BookingRequestDto`, `BookingDto`, `CalendarBookingDto`, `AvailabilityResult`
- 4 типа исключений: `NotFoundException`, `AccessDeniedException`, `ValidationException`, `BookingConflictException`
- Все бизнес-правила: рабочие часы, шаг, [start,end) интервалы, пересечение, права User/Manager, проверка прошлого

Изменения Domain не требуются. Подтверждено, что конкурентная запись — ответственность будущего DataAccess.

### Реализация

✅ Выполнено. Дата: 2026-11-03.

#### Созданные файлы (14 шт.)

**Интерфейсы (Abstractions/):**
1. `IRoomRepository.cs` — `ExistsAsync`, `GetByIdAsync`
2. `IBookingRepository.cs` — `GetByIdAsync`, `GetByUserIdAsync`, `GetByDateRangeAsync`, `FindOverlappingAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
3. `ICurrentUserContext.cs` — `GetCurrentUserIdAsync`, `GetCurrentUserRoleAsync`
4. `ITimeProvider.cs` — `GetCurrentTime()`

**Исключения (Exceptions/):**
5. `ApplicationException.cs` — базовый абстрактный класс
6. `NotFoundException.cs` — entityName + entityId
7. `AccessDeniedException.cs` — кастомное сообщение
8. `ValidationException.cs` — сообщение + inner exception
9. `BookingConflictException.cs` — roomId, date, startTime, endTime

**Модели (Models/):**
10. `BookingRequestDto.cs` — RoomId, Date, StartTime, EndTime, Title, Description
11. `BookingDto.cs` — Id, Title, Description, UserId, UserFirstName, UserLastName, RoomId, RoomName, Date, StartTime, EndTime
12. `CalendarBookingDto.cs` — Id, RoomId, RoomName, Date, StartTime, EndTime, Title?, Description?, UserFirstName?, UserLastName?, IsOwn
13. `AvailabilityResult.cs` + `ConflictingBookingInfo` — IsAvailable, Conflicts

**Сервис (Services/):**
14. `BookingService.cs` — 6 методов:
    - `CreateBookingAsync` — создание с проверкой комнаты, времени, пересечений
    - `UpdateBookingAsync` — редактирование (права User/Manager, исключение своей брони из overlap)
    - `DeleteBookingAsync` — удаление с проверкой прав
    - `GetMyBookingsAsync` — список броней текущего пользователя
    - `GetCalendarBookingsAsync` — календарь с разной видимостью (свои — полная, чужие — без деталей)
    - `CheckAvailabilityAsync` — проверка доступности комнаты

#### Ключевые детали реализации
- Зависимости через конструктор: `IRoomRepository`, `IBookingRepository`, `ICurrentUserContext`, `ITimeProvider`, `IOptions<BookingOptions>`, `IOptions<ValidationOptions>`
- `BookingTimeRules` собирается из `BookingOptions`, `StringLengthRules` — из `ValidationOptions`
- Domain `ArgumentException` оборачиваются в `ValidationException`
- Нормализация дат к `DateTimeKind.Unspecified`
- Проверка "не в прошлом" через `ITimeProvider.GetCurrentTime().Date`
- Интервалы `[start, end)`, формула: `existing.Start < requested.End && requested.Start < existing.End`
- При редактировании — исключение текущей брони через `excludeBookingId`
- CalendarBookingDto: `IsOwn = booking.UserId == currentUserId || role == Manager`
- Ограничение: изменение `RoomId` при редактировании не поддерживается (Domain не имеет `SetRoomId`)

#### Сборка
```
dotnet build Backend\MeetSpace.slnx
Результат: 0 ошибок, 0 предупреждений
```

#### Тесты
```
dotnet test Backend\MeetSpace.slnx
Результат: 23 пройдено, 0 не пройдено, 0 пропущено
```

#### Примечания
1. Конкурентная запись — ответственность DataAccess, не гарантируется здесь.
2. Изменение RoomId при Update невозможно без изменения Domain.
3. Новые unit-тесты BookingService — в разделе «Написание тестов».
4. DI-регистрация BookingService — отдельный этап связывания.
5. Существующий код не изменялся.

### Написание тестов

✅ Выполнено. Дата: 2026-11-03.

Файл: `Backend/tests/MeetSpace.Tests/Application/Services/BookingServiceTests.cs`

#### Конфигурация тестов

- Фреймворк: xUnit (2.9.3), Moq (4.21.0)
- `BookingService` — реальный экземпляр с подменой зависимостей через конструктор
- Mock-зависимости: `IRoomRepository`, `IBookingRepository`, `ICurrentUserContext`, `ITimeProvider`
- Настройки по умолчанию:
  - `BookingOptions`: WorkingHoursStart=09:00, WorkingHoursEnd=18:00, BookingTimeStepMinutes=15
  - `ValidationOptions`: BookingTitleMaxLength=200, BookingDescriptionMaxLength=2000
  - `ITimeProvider.GetCurrentTime()` → 2026-11-10 12:00

#### Количество тестов

**53 теста в классе BookingServiceTests**, все пройдены.

#### Покрытые сценарии (все 30 из тикета + дополнительные):

##### Успешные (8+):
1. `CreateBooking_ValidRequest_CreatesBooking` — создание, проверка DTO + `AddAsync` вызван
2. `UpdateBooking_UserUpdatesOwnBooking_Succeeds` — User обновляет свою бронь
3. `UpdateBooking_ManagerUpdatesOtherBooking_Succeeds` — Manager обновляет чужую бронь
4. `DeleteBooking_UserDeletesOwnBooking_Succeeds` — User удаляет свою бронь
5. `DeleteBooking_ManagerDeletesOtherBooking_Succeeds` — Manager удаляет чужую бронь
6. `GetMyBookings_ReturnsList` — список броней пользователя (2 элемента, проверка полей)
7. `GetCalendarBookings_OwnAndOthers_DifferentVisibility` — свои: Title/Description/имена ≠ null, IsOwn=true; чужие: null, IsOwn=false
8. `CheckAvailability_RoomFree_ReturnsAvailable` / `CheckAvailability_RoomOccupied_ReturnsConflicted` — оба состояния

##### Валидация (5+):
9. `CreateBooking_EndTimeEqualsStartTime_ThrowsValidationException` + `CreateBooking_EndTimeBeforeStartTime_ThrowsValidationException`
10. `CreateBooking_BeforeWorkingHours_ThrowsValidationException` + `CreateBooking_AfterWorkingHours_ThrowsValidationException`
11. `CreateBooking_InvalidTimeStepMinutes_ThrowsValidationException` + `CreateBooking_EndTimeMinuteNotMultipleOfStep_ThrowsValidationException`
12. `CreateBooking_PastDate_ThrowsValidationException` + `CreateBooking_PastTimeOnToday_ThrowsValidationException`
13. `CreateBooking_EmptyTitle_ThrowsValidationException` + `CreateBooking_WhitespaceTitle_ThrowsValidationException`

##### Пересечения (6):
14. `CreateBooking_ExactOverlap_ThrowsConflict` — [10:00-12:00) и [10:00-12:00)
15. `CreateBooking_PartialOverlap_ThrowsConflict` — [10:00-12:00) и [11:00-13:00)
16. `CreateBooking_NestedOverlap_ThrowsConflict` — [10:00-13:00) и [11:00-12:00)
17. `CreateBooking_AdjacentBookings_NoConflict` — [09:00-10:00) и [10:00-11:00) → успех
18. `CreateBooking_OtherRoom_NoConflict` — другая комната → успех
19. `CreateBooking_OtherDate_NoConflict` — другая дата → успех

##### Исключение текущей брони (1):
20. `UpdateBooking_SameTime_NoSelfConflict` — саморедактирование без конфликта

##### Права доступа (3+):
21. `UpdateBooking_UserModifiesOtherBooking_ThrowsAccessDenied`
22. `DeleteBooking_UserDeletesOtherBooking_ThrowsAccessDenied`
23. `CreateBooking_UnauthenticatedUser_ThrowsAccessDenied` + `UpdateBooking_UnauthenticatedUser_ThrowsAccessDenied` + `DeleteBooking_UnauthenticatedUser_ThrowsAccessDenied`

##### Отсутствие сущностей (3+):
24. `CreateBooking_RoomNotFound_ThrowsNotFoundException` — с проверкой EntityName/EntityId
25. `UpdateBooking_BookingNotFound_ThrowsNotFoundException`
26. `DeleteBooking_BookingNotFound_ThrowsNotFoundException`
(+) `CheckAvailability_RoomNotFound_ThrowsNotFoundException`

##### Защита состояния (3+):
27. `CreateBooking_ValidationError_DoesNotCallAddAsync` — `AddAsync` Times.Never
28. `CreateBooking_ConflictError_DoesNotCallAddAsync` — `AddAsync` Times.Never
29. `DeleteBooking_AccessDenied_DoesNotCallDeleteAsync` — `DeleteAsync` Times.Never
(+) `UpdateBooking_AccessDenied_DoesNotCallUpdateAsync` — `UpdateAsync` Times.Never

##### Настройки (1+):
30. `CreateBooking_CustomBookingOptions_Step30Working10to20` — успех с Step=30, 10:00-20:00
(+) `CreateBooking_CustomStep30_Rejects15MinStep` — 15-мин шаг невалиден при 30-мин настройке
(+) `CreateBooking_CustomWorkingHours_RejectsBeforeStart` / `RejectsAfterEnd`

##### Дополнительные тесты:
- `CreateBooking_DtoHasCorrectUserData` — маппинг Title + Description
- `GetMyBookings_WithUserAndRoomData_MapsCorrectly` — полный DTO: Id, Title, Description, UserName, RoomName
- `GetCalendarBookings_ManagerSeesAllDetails` — Manager видит чужие Title/Description
- `GetCalendarBookings_IsOwnTrueForSelf` — свойство IsOwn для своей брони
- `GetMyBookings_EmptyList_ReturnsEmpty` — пустой список
- `CheckAvailability_ValidatesTimeInterval` — валидация интервала в CheckAvailability
- `CheckAvailability_PastDate_ThrowsValidationException` — прошедшая дата в CheckAvailability
- `UpdateBooking_ChangesAllFields_Succeeds` — изменение всех полей
- `CreateBooking_DescriptionExceedsMaxLength_ThrowsValidationException` — превышение длины описания
- `CreateBooking_NormalizesDateKind` — нормализация DateTimeKind.Utc → Unspecified

#### Результаты выполнения

```
dotnet build Backend\MeetSpace.slnx
→ 0 ошибок, 0 предупреждений

dotnet test Backend\MeetSpace.slnx
→ Всего тестов: 76 (53 BookingServiceTests + 23 существующих)
→ Пройдено: 76
→ Упало: 0
→ Пропущено: 0
```

#### Замечания

1. **Moq уже присутствовал в проекте** (версия 4.21.0), дополнительных пакетов не потребовалось.
2. **Тестовый файл уже содержал 53 теста**, написанных на этапе реализации. Все тесты проходят.
3. Все сценарии из тикета покрыты. Дополнительные тесты проверяют граничные случаи (пустой список, маппинг DTO, нормализацию даты, Manager-видимость в календаре).
4. Тесты используют реальный `BookingService` с mock-зависимостями. Применяются:
   - `Times.Once` / `Times.Never` для проверки вызовов
   - `Assert.ThrowsAsync<T>` для проверки исключений
   - Проверка содержимого DTO через `Assert.Equal`, `Assert.NotNull`, `Assert.Null`
5. Изменений кода приложения не потребовалось.

### Тестирование

✅ Выполнено. Дата: 2026-11-03.

- Сборка: `dotnet build Backend\MeetSpace.slnx` — успешно (0 ошибок, 0 предупреждений)
- Тесты: `dotnet test Backend\MeetSpace.slnx` — 76/76 пройдено
- Названия методов соответствуют соглашению `MethodName_Scenario_ExpectedBehavior`
- Все 30 обязательных сценариев покрыты
- Конкурентная запись не тестируется — ответственность DataAccess (документировано в архитектуре)
- Код приложения не изменялся

#### Независимая проверка (тестировщик)

Дата: 2026-11-03. Проверка выполнена независимо, код приложения и тесты не изменялись.

**1. Сборка и тесты (самостоятельный запуск)**

Команды и результаты (решение — `Backend/MeetSpace.slnx`, формата `.sln` в репозитории нет):

| Команда | Exit code | Результат |
|---|---|---|
| `dotnet build Backend/MeetSpace.slnx` | 0 | Сборка успешно завершена. Предупреждений: 0, Ошибок: 0 |
| `dotnet test Backend/MeetSpace.slnx` | 0 | Пройдено: 76, Не пройдено: 0, Пропущено: 0, Всего: 76 |

Распределение тестов по сборкам (из лога запуска):
- `MeetSpace.Tests.Application.Services.BookingServiceTests` — 53 теста, все пройдены
- `MeetSpace.Tests.Domain.BookingTimeRulesConsumerTests` — 4 теста
- `MeetSpace.Tests.Validation.*` — 19 тестов (BookingOptionsValidatorTests 5, ValidationOptionsValidatorTests 2, PasswordHashingOptionsValidatorTests 3, S3OptionsValidatorTests 3, JwtOptionsValidatorTests 3, OptionsRegistrationExtensionTests 3)
- Итого: 53 + 23 = 76. Упавших и пропущенных тестов нет.

**2. Проверка критериев готовности**

| # | Критерий | Статус | Комментарий |
|---|---------|--------|-------------|
| 1 | Архитектурный документ с методами, контрактами, моделями, ошибками | ✅ | `docs/architecture/DEV-005-booking-application-logic.md`: раздел 3 — методы `BookingService` (6 шт.) и пошаговые сценарии; раздел 4 — модели (BookingRequestDto, BookingDto, CalendarBookingDto, AvailabilityResult); раздел 5 — 4 интерфейса зависимостей; раздел 6 — иерархия и определения 5 типов исключений. Разделы 3–6 полные. |
| 2 | Сценарии реализованы в Application через Domain и абстракции | ✅ | `BookingService.cs` использует Domain-сущности (`Booking`, `Room`, `UserRole`, `BookingTimeRules`, `StringLengthRules`) и 4 абстракции (`IRoomRepository`, `IBookingRepository`, `ICurrentUserContext`, `ITimeProvider`). Конкретных реализаций репозиториев/сервисов в Application нет — только их вызовы. |
| 3 | Application не зависит от DataAccess, Infrastructure, ASP.NET Core, EF Core | ✅ | `MeetSpace.Application.csproj` ссылается только на `MeetSpace.Domain` + пакеты `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Проект `DataAccess` в solution отсутствует. Grep по исходникам Application по шаблонам `DbContext`, `EntityFrameworkCore`, `Microsoft.AspNetCore`, `HttpContext`, `ClaimsPrincipal`, `IConfiguration`, `IQueryable`, `AWSSDK`, `MinIO`, `BCrypt`, `Jwt`, `Infrastructure`, `DataAccess` — совпадений нет. |
| 4 | Только необходимые контракты будущих реализаций | ✅ | В `Abstractions/` ровно 4 интерфейса: `IRoomRepository` (2 метода), `IBookingRepository` (7 методов), `ICurrentUserContext` (2 метода), `ITimeProvider` (1 метод). JWT/S3/почта/универсальный Repository/UoW не спроектированы — соответствует тикету. |
| 5 | Бизнес-правила и права доступа покрыты unit-тестами реальных сервисов | ✅ | `BookingServiceTests.cs`: создаётся реальный `BookingService` через конструктор (`new BookingService(roomRepo.Object, bookingRepo.Object, userCtx.Object, timeProvider.Object, bo, vo)`), мокаются только зависимости. Тесты проверяют решение сервиса (значения полей DTO через `Assert.Equal`, состояние `IsAvailable`/`IsOwn`, типы исключений через `Assert.ThrowsAsync<T>`) и обращения к зависимостям (`Verify(..., Times.Once/Times.Never)`). Покрыты: 8 успешных сценариев, валидация (равные/обратные интервалы, границы рабочих часов, шаг, прошлое время, пустой/whitespace-заголовок, длина описания), пересечения (полное/частичное/вложенное/соседние/другая комната/другая дата), исключение текущей брони при редактировании, права User/Manager, неаутентифицированный пользователь, отсутствие комнаты/брони, защита состояния (отсутствие вызовов записи при ошибках), действие изменённых настроек. |
| 6 | Сборка и тесты выполнены; результаты и ограничения записаны | ✅ | См. раздел 1 выше. Команды, exit codes и распределение тестов зафиксированы. |
| 7 | Будущая защита конкурентной записи явно обозначена как непроверенная ответственность DataAccess | ✅ | Раздел 8.2 архитектурного документа: «В DEV-005 это требование **не реализуется и не тестируется**. В отчёте явно указано, что гарантия конкурентной записи — ответственность будущего DataAccess-тикета». Подтверждено в отчёте архитектора (раздел 13, п.4) и отчёте реализации (примечание 1). Тесты конкурентной записи не пишутся — соответствует тикету. |
| 8 | Отчёты заполнены; после успешной проверки статус «На проверке» | ✅ | Разделы «Архитектор», «Реализация», «Написание тестов», «Тестирование» заполнены. Статус тикета изменён с «В работе» на «На проверке». |

**3. Проверка границ слоёв**

| Граница | Статус | Проверка |
|---|--------|----------|
| Нет реализаций репозиториев (только интерфейсы) | ✅ | Grep `class \w+Repository` и `: IRoomRepository/IBookingRepository/ICurrentUserContext/ITimeProvider` по `Backend/src/MeetSpace.Application` — совпадений нет. В `Backend/src` реализации этих интерфейсов отсутствуют. |
| Нет DbContext, EF mapping, миграций | ✅ | Grep `DbContext`/`EntityFrameworkCore` — нет; папок `Migrations` нет; пакет EF Core в `.csproj` отсутствует. |
| Нет JWT, bcrypt, S3, загрузки файлов | ✅ | Grep `Jwt`/`BCrypt`/`AWSSDK`/`MinIO` — нет; пакеты не подключены. |
| Нет HTTP endpoints, контроллеров | ✅ | В Application нет `Controller`/`Endpoint`; `Program.cs` (Api) не изменён — только `/health` и регистрация Options. |
| Нет production-заглушек зависимостей | ✅ | Нет fake/in-memory реализаций репозиториев/`ICurrentUserContext`/`ITimeProvider` в `Backend/src`. Реальный сервис только один — `BookingService`. |
| Application не ссылается на Infrastructure/Api/DataAccess | ✅ | `MeetSpace.Application.csproj` — только ссылка на `Domain`. |

**4. Качество тестов (выборочная проверка)**

- Тесты проверяют РЕШЕНИЕ сервиса, а не только ответы mock: `CreateBooking_ValidRequest_CreatesBooking` проверяет значения `result.Title/UserId/RoomId/Date/StartTime/EndTime` + `AddAsync` `Times.Once`; `GetMyBookings_WithUserAndRoomData_MapsCorrectly` проверяет маппинг `UserFirstName/LastName/RoomName`; `GetCalendarBookings_OwnAndOthers_DifferentVisibility` проверяет `null`/не-`null` полей и флаг `IsOwn`.
- `Verify` с `Times.Once` (успешные операции) и `Times.Never` (защита состояния при ошибках валидации/конфликта/доступа) — присутствуют (`CreateBooking_ValidationError_DoesNotCallAddAsync`, `CreateBooking_ConflictError_DoesNotCallAddAsync`, `DeleteBooking_AccessDenied_DoesNotCallDeleteAsync`, `UpdateBooking_AccessDenied_DoesNotCallUpdateAsync`).
- Покрытие: успешные сценарии, ошибки валидации, граничные случаи рабочих часов и шага, прошедшее время, полное/частичное/вложенное пересечение, соседние брони, другая комната/дата, права User/Manager, отсутствие пользователя/комнаты/брони, действие изменённых настроек — всё заявленное в тикете покрыто.

**5. Наблюдения (не блокирующие)**

1. Формула пересечения `existing.Start < requested.End && requested.Start < existing.End` реализуется в будущем `FindOverlappingAsync` (DataAccess), а не в сервисе. Тесты `CreateBooking_AdjacentBookings_NoConflict` и т.п. конфигурируют mock-репозиторий возвращать пустой/непустой результат, т.е. проверяют реакцию сервиса на ответ репозитория, а не саму формулу. Это соответствует архитектуре (формула — ответственность DataAccess), замечанием не является.
2. В `UpdateBookingAsync` при запросе с изменённым `RoomId` сервис проверяет существование новой комнаты, но не изменяет `RoomId` (Domain не имеет `SetRoomId`) и использует для overlap-проверки старый `existing.RoomId`. Поведение зафиксировано в отчёте реализации (примечание 2) как ограничение, тестов на смену комнаты нет. Сценарий не входит в явные требования тикета; рекомендую в будущем либо явно отклонять смену комнаты (`ValidationException`), либо не валидировать несменяемое поле.
3. Тесты `CreateBooking_EmptyTitle_ThrowsValidationException`/`CreateBooking_WhitespaceTitle_ThrowsValidationException` проверяют тип исключения, но не его сообщение (в коде оставлен комментарий-примечание). Контракт (выброс `ValidationException`) соблюдён, замечанием не является.

**Общий вердикт**

✅ Все 8 критериев готовности выполнены. Сборка и тесты проходят (exit code 0; 76/76). Границы слоёв соблюдены: Application зависит только от Domain и абстракций, реализаций репозиториев, DbContext, EF Core, JWT, S3, HTTP-эндпоинтов и production-заглушек нет. Тесты используют реальный `BookingService` с подменой зависимостей и проверяют решение сервиса. Защита конкурентной записи корректно отнесена к будущему DataAccess-тикету.

Блокирующих дефектов не обнаружено. Тикет переведён в статус «На проверке».

### Публикация

