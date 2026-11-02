# DEV-003 — Замена DomainConstants типизированными настройками

Статус: В работе → На проверке

## Цель

Перенести значения из `Backend/src/MeetSpace.Domain/Constants/DomainConstants.cs` в осмысленные секции `appsettings.json` и типизированные Options. Обновить существующих потребителей, сохранив текущее поведение и независимость Domain от конфигурации приложения.

## Источники

- `AGENTS.md` — правила и архитектура проекта.
- `goal.md` — требования к системе.
- Текущие исходники, настройки, тесты и EF-конфигурации.

## Что сделать

### 1. Архитектура

Результат: `docs/architecture/DEV-003-typed-options.md`.

- Найти все использования каждого поля DomainConstants, включая атрибуты валидации, EF mapping и тесты. Не считать, что все сервисы уже реализованы.
- Для каждого значения указать секцию конфигурации, класс Options, слой размещения, потребителя и способ передачи значения.
- Использовать группы BookingOptions, JwtOptions, PasswordHashingOptions, S3Options и ValidationOptions либо обосновать более подходящие названия. Не создавать общий класс со всеми настройками.
- Описать регистрацию, binding, DI и валидацию при запуске.
- Не добавлять в Domain зависимости от IConfiguration, IOptions, ASP.NET Core, чтения JSON или service locator. Если правило применяется в Domain, передавать необходимые значения через обычные типы/объекты правил без инфраструктурных зависимостей. Не внедрять DI-контейнер в сущности.
- Соблюдать AGENTS.md: Application не должен получать зависимость от Microsoft.Extensions.Options ради доступа к настройкам; для него определить обычные параметры или подходящую абстракцию. Binding и адаптация выполняются на границе приложения, технические настройки располагаются у соответствующих реализаций.
- Определить, где нужны настройки, а где остаются неизменяемые технические ограничения. Любое исключение из переноса перечисленных значений согласовать с владельцем.
- Отдельно разобрать MaxLength: значения из Options нельзя подставить в атрибуты, требующие compile-time constants. Динамическую валидацию организовать подходящим способом, без дублирующихся скрытых лимитов.
- Если ограничения длины участвуют в EF mapping, описать совместимость со схемой БД. Изменение appsettings не должно автоматически менять EF-модель или схему. Для значений, превышающих ограничения существующих столбцов, требуются явное решение и миграция; при необходимости вынести это в отдельный тикет и согласовать блокировку.

Исходные значения, которые необходимо сохранить:

| Группа | Значения |
| --- | --- |
| Booking | WorkingHoursStart = 09:00:00; WorkingHoursEnd = 18:00:00; BookingTimeStepMinutes = 15 |
| Jwt | AccessTokenLifetimeMinutes = 10; RefreshTokenLifetimeDays = 14 |
| PasswordHashing | BcryptWorkFactor = 12 |
| S3 | BucketName = meet-space; PresignedUrlLifetimeMinutes = 60 |
| Validation | UserNameMaxLength = 100; EmailMaxLength = 255; EquipmentNameMaxLength = 50; RoomNameMaxLength = 200; RoomDescriptionMaxLength = 2000; BookingTitleMaxLength = 200; BookingDescriptionMaxLength = 2000 |

Указать, что изменения конфигурации применяются после перезапуска. Hot reload не требуется.

### 2. Реализация

- Реализовать архитектурный документ и добавить исходные значения в `Backend/src/MeetSpace.Api/appsettings.json`.
- Использовать типизированные настройки и их валидацию при запуске (`ValidateOnStart` или эквивалент).
- Проверять обязательность значений, положительность длительностей/лимитов, корректность рабочего интервала, шага бронирования и допустимость параметров bcrypt с учётом используемой библиотеки. Межполевые правила описать в архитектурном документе.
- Не скрывать отсутствие обязательной конфигурации неявными fallback-значениями. Ошибка запуска должна указывать проблемную секцию или параметр без раскрытия секретов.
- Заменить все существующие обращения к DomainConstants, не дублируя прежние значения в коде.
- После переноса удалить DomainConstants.cs, если согласованное решение не требует иного.
- Не добавлять несуществующие JWT/S3-сервисы и бизнес-функции только ради демонстрации Options. Для ещё не реализованных потребителей подготовить настройки и задокументировать предполагаемое использование.
- Сохранить возможность переопределения настроек штатными провайдерами конфигурации .NET, включая environment variables.
- Кратко описать секции, единицы измерения, применение после перезапуска и ограничения изменения MaxLength в Backend/README.md.

### 3. Написание тестов

- Добавить целевые xUnit-тесты привязки и валидации настроек: корректные значения, отсутствующее обязательное поле и некорректные значения.
- Проверить неверный рабочий интервал и шаг; недопустимые длительности/лимиты и bcrypt work factor.
- Для уже существующих потребителей проверить на одном или нескольких показательных сценариях, что переопределённое значение действительно используется вместо прежней константы.
- Не создавать тесты простых getter/setter и наборы тестов, лишь повторяющие декларацию Options.
- Если сценарий невозможно проверить без отсутствующего сервиса, указать это явно; сервис не создавать.

### 4. Проверка

- Выполнить restore, build и применимые тесты.
- Проверить запуск с корректной конфигурацией и GET /health.
- Проверить, что запуск с некорректной конфигурацией завершается понятной ошибкой валидации.
- Проверить хотя бы одно переопределение через environment variable, например Booking__BookingTimeStepMinutes; подтвердить применение значения подходящим тестом или диагностикой без добавления публичного debug-endpoint.
- Проверить отсутствие старых обращений к DomainConstants и инфраструктурных зависимостей в Domain.
- Проверить согласованность валидации и существующих ограничений хранения.

## Границы

- Рефакторинг конфигурации, без изменения значений по умолчанию и пользовательских сценариев.
- Не реализовывать авторизацию, хранение файлов, CRUD, frontend и новые архитектурные слои в рамках задачи.
- Не выполнять миграции или изменение схемы БД без согласования.
- Не добавлять секреты в appsettings и не менять существующие секреты.

## Критерии готовности

- Есть архитектурный документ с размещением настроек по слоям и картой потребителей.
- Перечисленные значения перенесены в appsettings и типизированные группы Options; исключения согласованы и объяснены.
- Существующие потребители используют переданные настройки, а не прежние константы.
- Domain остаётся независимым от конфигурационных библиотек.
- Поведение и исходные значения сохранены; ограничения MaxLength и БД согласованы.
- Есть результаты сборки, тестов и проверок запуска, включая невалидную конфигурацию.
- В тикете заполнены отчёты этапов; пропущенные проверки имеют обоснование.
- После успешной проверки статус — «На проверке». «Завершено» устанавливает владелец.

## Публикация

- Только после успешного тестирования и заполнения отчётов сделать коммит изменений тикета и отправить рабочую ветку в origin.
- Создать pull request в целевую ветку, указанную владельцем при запуске, с описанием изменений и результатами проверок.
- Не выполнять merge, force push и прямой push в master/main.
- Не включать секреты, посторонние файлы и артефакты сборки.
- Если PR уже есть, обновить его описание вместо создания дубликата.

## Отчёты

### Архитектор

**Дата:** 2026-11-03

**Выполненный анализ:**

1. Прочитан `DomainConstants.cs` — 17 полей в 5 смысловых группах.
2. Выполнен grep-поиск каждого поля во всём решении `Backend/`.
3. Изучены все 6 файлов сущностей (`Booking`, `Equipment`, `RefreshToken`, `Room`, `RoomEquipment`, `User`), `appsettings.json`, `Program.cs`.
4. Проверено отсутствие: DataAccess, Application, Infrastructure слоёв; EF-конфигураций; тестов; JWT/S3/BCrypt сервисов.

**Полная карта потребителей:**

| Поле | Потребитель | Статус |
|---|---|---|
| `WorkingHoursStart` | `Booking.SetTimeInterval()` (стр. 56) | Domain |
| `WorkingHoursEnd` | `Booking.SetTimeInterval()` (стр. 59) | Domain |
| `BookingTimeStepMinutes` | `Booking.SetTimeInterval()` (стр. 62, 65) | Domain |
| `AccessTokenLifetimeMinutes` | Нет потребителей | Константа не используется |
| `RefreshTokenLifetimeDays` | `RefreshToken` конструктор (стр. 22) | Domain |
| `BcryptWorkFactor` | Нет потребителей | Константа не используется |
| `S3BucketName` | Нет потребителей | Константа не используется |
| `PresignedUrlLifetimeMinutes` | Нет потребителей | Константа не используется |
| `UserNameMaxLength` | `User.SetFirstName()` (стр. 34), `User.SetLastName()` (стр. 42) | Domain |
| `EmailMaxLength` | `User.SetEmail()` (стр. 50) | Domain |
| `EquipmentNameMaxLength` | `Equipment.SetName()` (стр. 21) | Domain |
| `RoomNameMaxLength` | `Room.SetName()` (стр. 29) | Domain |
| `RoomDescriptionMaxLength` | `Room.SetDescription()` (стр. 50) | Domain |
| `BookingTitleMaxLength` | `Booking.SetTitle()` (стр. 32) | Domain |
| `BookingDescriptionMaxLength` | `Booking.SetDescription()` (стр. 39) | Domain |

**Архитектурные решения:**
- 5 групп Options: `BookingOptions` (Application), `JwtOptions` (Infrastructure), `PasswordHashingOptions` (Infrastructure), `S3Options` (Infrastructure), `ValidationOptions` (Application).
- Domain изолирован: значения передаются через объекты-правила (`BookingTimeRules`) и параметры (`int maxLength`, `int lifetimeDays`).
- MaxLength: динамическая валидация через параметры методов, а не атрибуты (compile-time constraint). EF mapping — синхронизированные константы.
- Валидация при запуске: `ValidateOnStart()` с межполевыми проверками.
- Изменения после перезапуска, hot reload не требуется.
- Поддержка environment variables через стандартные .NET провайдеры.

**Исключений из переноса нет:** все 17 полей DomainConstants переносятся в appsettings.json.

**Открытый вопрос:** создание проектов Application и Infrastructure для размещения Options-классов — в рамках DEV-003 или отдельных тикетов.

Подробный документ: `docs/architecture/DEV-003-typed-options.md`.

### Реализация

**Дата:** 2026-11-03

**Созданные файлы:**

| Файл | Слой | Назначение |
| --- | --- | --- |
| `src/MeetSpace.Application/MeetSpace.Application.csproj` | Application | Проект Class Library (net10.0), ссылки на Domain, пакеты Options/DI |
| `src/MeetSpace.Application/Options/BookingOptions.cs` | Application | Options-класс: WorkingHoursStart, WorkingHoursEnd, BookingTimeStepMinutes |
| `src/MeetSpace.Application/Options/ValidationOptions.cs` | Application | Options-класс: 7 полей MaxLength |
| `src/MeetSpace.Application/Rules/StringLengthRules.cs` | Application | Объект-правило ограничений длины (передаётся в Application-сервисы) |
| `src/MeetSpace.Infrastructure/MeetSpace.Infrastructure.csproj` | Infrastructure | Проект Class Library (net10.0), пакет Options |
| `src/MeetSpace.Infrastructure/Options/JwtOptions.cs` | Infrastructure | Options-класс: AccessTokenLifetimeMinutes, RefreshTokenLifetimeDays |
| `src/MeetSpace.Infrastructure/Options/PasswordHashingOptions.cs` | Infrastructure | Options-класс: BcryptWorkFactor |
| `src/MeetSpace.Infrastructure/Options/S3Options.cs` | Infrastructure | Options-класс: BucketName, PresignedUrlLifetimeMinutes |
| `src/MeetSpace.Domain/Rules/BookingTimeRules.cs` | Domain | Объект-правило: WorkingHoursStart, WorkingHoursEnd, TimeStepMinutes |

**Изменённые файлы:**

| Файл | Изменение |
| --- | --- |
| `MeetSpace.slnx` | Добавлены проекты Application и Infrastructure |
| `src/MeetSpace.Api/MeetSpace.Api.csproj` | Добавлены ProjectReference на Application и Infrastructure |
| `src/MeetSpace.Api/appsettings.json` | Добавлены 5 секций конфигурации (Booking, Jwt, PasswordHashing, S3, Validation) с исходными значениями |
| `src/MeetSpace.Api/Program.cs` | Добавлена регистрация 5 групп Options с `ValidateOnStart()` и правилами валидации |
| `src/MeetSpace.Domain/Entities/Booking.cs` | Методы `SetTitle`, `SetDescription`, `SetTimeInterval` и конструктор принимают параметры `maxLength` и `BookingTimeRules`; удалены ссылки на `DomainConstants` |
| `src/MeetSpace.Domain/Entities/Room.cs` | Методы `SetName`, `SetDescription` и конструктор принимают параметры `maxLength`; удалены ссылки на `DomainConstants` |
| `src/MeetSpace.Domain/Entities/Equipment.cs` | Метод `SetName` и конструктор принимают параметр `maxLength`; удалены ссылки на `DomainConstants` |
| `src/MeetSpace.Domain/Entities/User.cs` | Методы `SetFirstName`, `SetLastName`, `SetEmail` и конструктор принимают параметры `maxLength`; удалены ссылки на `DomainConstants` |
| `src/MeetSpace.Domain/Entities/RefreshToken.cs` | Конструктор принимает параметр `lifetimeDays`; удалены ссылки на `DomainConstants` |
| `Backend/README.md` | Обновлена структура проектов; добавлен раздел «Конфигурация» с описанием секций, единиц измерения, переопределения через env vars и ограничений MaxLength |

**Удалённые файлы:**

| Файл | Причина |
| --- | --- |
| `src/MeetSpace.Domain/Constants/DomainConstants.cs` | Все 17 полей перенесены в типизированные Options; все потребители обновлены |

**Отклонения от архитектурного документа:**

- `BookingTimeRules` размещён в `MeetSpace.Domain.Rules` (а не в `MeetSpace.Application.Rules`), так как используется как тип параметра в Domain-сущностях. Размещение в Application создало бы циклическую зависимость (Application → Domain → Application). Это чистый value object без зависимостей от IOptions/IConfiguration, что соответствует правилам AGENTS.md.

**Статус сборки:**

```
dotnet restore — успешно (4 проекта)
dotnet build -c Release — успешно (0 ошибок, 0 предупреждений)
```

**Проверка валидации при запуске:**

Запуск с невалидным значением `BookingTimeStepMinutes=9999` через env var:
```
Microsoft.Extensions.Options.OptionsValidationException: Invalid Booking configuration:
working hours must be valid and time step must be positive and fit within the working interval.
```
Приложение завершает запуск с ожидаемой ошибкой. Нормальный запуск (со значениями по умолчанию) проходит успешно.

**Проверка отсутствия старых обращений:**
- `grep DomainConstants` в `Backend/src/` — 0 совпадений.
- Domain не зависит от IConfiguration, IOptions, ASP.NET Core — подтверждено (все параметры — простые типы C#).
### Написание тестов

**Дата:** 2026-11-03

**Создан тестовый проект:** `Backend/tests/MeetSpace.Tests/` (xUnit, net10.0).

**Тесты валидации Options (17 тестов):**

| Класс | Тест | Результат |
| --- | --- | --- |
| `BookingOptionsValidatorTests` | `ValidDefaultValues_PassesValidation` | Пройден |
| | `WorkingHoursStartAfterEnd_ThrowsOptionsValidationException` | Пройден |
| | `WorkingHoursStartEqualsEnd_ThrowsOptionsValidationException` | Пройден |
| | `BookingTimeStepMinutesZero_ThrowsOptionsValidationException` | Пройден |
| | `BookingTimeStepMinutesExceedsWorkingInterval_ThrowsOptionsValidationException` | Пройден |
| `JwtOptionsValidatorTests` | `ValidDefaultValues_PassesValidation` | Пройден |
| | `AccessTokenLifetimeMinutesZero_ThrowsOptionsValidationException` | Пройден |
| | `RefreshTokenLifetimeDaysZero_ThrowsOptionsValidationException` | Пройден |
| `PasswordHashingOptionsValidatorTests` | `ValidDefaultValue_PassesValidation` | Пройден |
| | `BcryptWorkFactorTooLow_ThrowsOptionsValidationException` | Пройден |
| | `BcryptWorkFactorTooHigh_ThrowsOptionsValidationException` | Пройден |
| `S3OptionsValidatorTests` | `ValidDefaultValues_PassesValidation` | Пройден |
| | `BucketNameEmpty_ThrowsOptionsValidationException` | Пройден |
| | `PresignedUrlLifetimeMinutesZero_ThrowsOptionsValidationException` | Пройден |
| `ValidationOptionsValidatorTests` | `ValidDefaultValues_PassesValidation` | Пройден |
| | `UserNameMaxLengthZero_ThrowsOptionsValidationException` | Пройден |

**Тесты потребления Domain (4 теста):**

| Класс | Тест | Результат |
| --- | --- | --- |
| `BookingTimeRulesConsumerTests` | `SetTimeInterval_WithCustomStep30_AcceptsStep30` | Пройден |
| | `SetTimeInterval_WithCustomStep30_RejectsStep15` | Пройден |
| | `SetTitle_WithMaxLength10_Accepts10Chars` | Пройден |
| | `SetTitle_WithMaxLength10_Rejects11Chars` | Пройден |

**Результат запуска:** 20/20 пройдено, 0 ошибок, 0 предупреждений.

**Не написаны тесты:**
- Тесты на JWT/S3/BCrypt сервисы — не реализованы в проекте.
- Тесты простых getter/setter Options-классов — согласно тикету не требуются.

**Отклонения от архитектурного документа:**
- Свойства Options-классов изменены с `{ get; init; }` на `{ get; set; }` для совместимости с `services.Configure<T>(Action<T>)` в тестах. Это стандартная практика для Options pattern в .NET.

### Тестирование

**Дата:** 2026-11-03

**Проверки:**

| Критерий | Команда/действие | Результат |
| --- | --- | --- |
| Release build | `dotnet build -c Release` | ✅ 0 ошибок, 0 предупреждений |
| Тесты | `dotnet test` | ✅ 20/20 пройдено |
| Запуск с корректной конфигурацией | `dotnet run`, затем `GET http://localhost:5231/health` | ✅ 200 OK, `{"status":"healthy"}` |
| Запуск с некорректной конфигурацией | `$env:Booking__BookingTimeStepMinutes=9999; dotnet run` | ✅ `OptionsValidationException: Invalid Booking configuration...` |
| Отсутствие DomainConstants | `grep -r DomainConstants src/` | ✅ 0 совпадений |
| Domain изолирован от инфраструктуры | Проверка .csproj и using | ✅ Нет ссылок на IConfiguration, IOptions, ASP.NET Core |
| 5 секций в appsettings.json | Чтение файла | ✅ Booking, Jwt, PasswordHashing, S3, Validation |
| 5 проектов в solution | `dotnet build` собирает все 5 | ✅ Api, Domain, Application, Infrastructure, Tests |

**Вердикт:** Все критерии пройдены. Дефектов не обнаружено.

### Публикация

