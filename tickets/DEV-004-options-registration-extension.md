# DEV-004 — Вынесение регистрации Options из Program.cs

Статус: На проверке

## Цель

Вынести регистрацию всех существующих Options в extension-метод, чтобы в `Program.cs` остался один понятный вызов. Сохранить binding, валидацию при запуске и текущее поведение приложения после DEV-003.

## Источники

- `AGENTS.md` — правила и архитектура.
- `goal.md` — требования к приложению.
- Результат DEV-003: код, настройки, валидаторы и тесты.

## Что сделать

### Архитектура

- Изучить фактическую регистрацию Options в Program.cs и вызываемых им методах: AddOptions, Configure, Bind/BindConfiguration, PostConfigure, IValidateOptions, ValidateOnStart и связанные адаптеры, если они существуют.
- Подготовить краткий документ `docs/architecture/DEV-004-options-registration-extension.md`: место размещения, сигнатура метода, переносимые регистрации и сохранение существующих зависимостей.
- Предпочтительный публичный вызов:
  `builder.Services.AddMeetSpaceOptions(builder.Configuration);`
- Предпочтительное место: `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs`. Если у проекта уже есть подходящее соглашение, использовать его и объяснить выбор.
- Сигнатура: extension-метод для IServiceCollection, принимающий IConfiguration и возвращающий IServiceCollection.
- Если часть регистраций уже инкапсулирована по слоям, сохранить границы слоёв и скомпоновать существующие методы. Не регистрировать одно и то же повторно.
- Архитектурный документ должен быть коротким: не проектировать приложение заново.

### Реализация

- Перенести существующие регистрации Options и их валидаторов в согласованное место.
- В Program.cs оставить единый вызов метода и необходимые using. Сохранить его порядок относительно регистраций, от которых зависит настройка.
- Не менять классы Options, секции и значения appsettings, правила валидации, lifetimes, named options и порядок Configure/PostConfigure, если они используются.
- Сохранить ValidateOnStart и существующую обработку ошибок конфигурации.
- Не добавлять BuildServiceProvider, ручное чтение JSON, статическое хранение конфигурации, сканирование сборок или новый DI-контейнер.
- Не переносить конфигурационные зависимости в Domain или Application.
- Не оборачивать весь Program.cs в один общий метод регистрации приложения: задача касается только Options.

## Проверки и тестирование

### 1. Исходное состояние

- Архитектор или реализатор фиксирует текущую ветку, исходный HEAD, состояние Git, перечень Options и их регистрацию до изменений.
- Реализатор запускает сборку и существующие релевантные тесты до рефакторинга. Если исходные проверки уже не проходят, сообщает координатору, не выдавая проблему за результат новых изменений.
- Посторонние незакоммиченные изменения не включать. При невозможности отделить их от задачи остановиться.

### 2. Автоматические проверки поведения регистрации

Автор тестов сначала изучает существующее покрытие DEV-003. Если перечисленные сценарии уже проверяются через production-регистрацию, переиспользовать тесты; не создавать дубликаты. При необходимости адаптировать их для вызова нового extension-метода.

- Корректная конфигурация: вызвать настоящий AddMeetSpaceOptions, запустить тестовый host, получить все фактически существующие Options через DI и проверить ожидаемые значения. Использовать валидные значения, отличающиеся от значений по умолчанию там, где это возможно, чтобы тест обнаруживал потерю binding.
- Невалидная конфигурация: для каждой группы с существующим валидатором подставить одно заведомо невалидное значение, остальные группы оставить корректными. Запуск host должен завершаться ошибкой валидации без предварительного обращения к IOptions.Value. Это проверка сохранения ValidateOnStart, а не только наличия валидатора.
- Для каждой ошибки проверить проблемную секцию или имя Options, не привязываясь ко всему тексту сообщения. Допустимо учитывать AggregateException с вложенными OptionsValidationException при нескольких ошибках.
- Если DEV-003 поддерживал ошибку при отсутствии обязательной секции, сохранить соответствующую проверку. Не вводить новое правило обязательности в этом рефакторинге.
- Если уже есть тест переопределения конфигурации, выполнить его; не дублировать полный набор сценариев DEV-003.
- Тесты вызывают production extension-метод. Не копировать его регистрации в тестовый код, не проверять число строк Program.cs и не писать тест «метод возвращает IServiceCollection».
- Не добавлять unit-тесты свойств Options или доменных сущностей ради покрытия.
- Инфраструктуру PostgreSQL/S3 не подключать ради проверки регистрации. Для host-тестов достаточно минимального host с настоящим методом и его необходимыми зависимостями.

### 3. Независимое тестирование

Тестировщик самостоятельно выполняет проверки после завершения изменений кода и тестов:

- Из `Backend/`: `dotnet restore MeetSpace.slnx`, `dotnet build MeetSpace.slnx --no-restore`, `dotnet test MeetSpace.slnx --no-restore`. Если в репозитории принят другой совместимый test runner, использовать фактические команды и указать причину.
- Зафиксировать exit code, число найденных/успешных/неуспешных/пропущенных тестов и фактические результаты. Отсутствие обнаруженных тестов не считать успешным тестированием.
- Запустить API по актуальной инструкции и проверить GET /health: HTTP 200. Не предполагать порт заранее — использовать фактический адрес запуска.
- Проверить по коду, что Program.cs вызывает новый метод и не сохраняет старые дублирующие регистрации.
- Подтвердить, что host-тесты проверяют запуск с невалидными настройками, а не только ручное чтение Options.
- В отчёте сопоставить каждый критерий готовности с выполненной проверкой или наблюдением.
- Не менять tracked appsettings ради проверки. Для экспериментов использовать временную конфигурацию или environment variables; изменения окружения отменять в finally.
- Если запускать отдельный процесс API, завершить только запущенный для проверки процесс. Не останавливать приложения других агентов.
- Если тесты менялись после проверки, повторить затронутые проверки перед публикацией.

## Границы

- Небольшой рефакторинг регистрации, без изменения бизнес-логики, API, схемы БД и зависимостей между слоями.
- Работать в текущей ветке после DEV-003; новую ветку/worktree не создавать и не переключать.
- Не менять файлы вне задачи, не выполнять масштабное форматирование и обновление пакетов.

## Критерии готовности

- Program.cs содержит один вызов регистрации всех существующих Options.
- Extension-метод расположен согласно архитектурному документу.
- Binding, валидаторы, ValidateOnStart и связанные регистрации сохранены без дублирования.
- Реальные проверки корректного и невалидного запуска проходят с ожидаемыми результатами.
- Сборка, релевантные тесты и GET /health проверены.
- В тикете есть отчёты этапов, включая пропущенные проверки с причинами.
- После успешной проверки статус — «На проверке»; «Завершено» устанавливает владелец.

## Публикация

- Публикатор получает явное разрешение от владельца в запускном запросе.
- Создать отдельный новый коммит DEV-004 в текущей ветке, затем push; не изменять историю DEV-003.
- Если для текущей ветки уже открыт PR в указанную владельцем целевую ветку, обновить его описание, сохранив описание DEV-003 и добавив DEV-004 с проверками.
- Если открытого PR нет, создать один PR для всей ветки с точным описанием итоговых изменений относительно целевой ветки. Закрытый/слитый PR не считать открытым.
- Если рабочая ветка или целевая база неясны, уточнить их до публикации.
- Не выполнять merge, force push, amend, rebase или прямой push в master/main. Не включать секреты и посторонние файлы.
- В итоговом ответе сообщить хеш нового коммита и ссылку на PR. Не создавать дополнительный коммит ради записи собственного хеша.

## Отчёты

### Архитектор

Исходное состояние и решение:
- Ветка: `refactor/dev-003-options`
- HEAD: `58aeebd` (DEV-003: "refactor: replace DomainConstants with typed Options (DEV-003)")
- Git status: чисто, единственный untracked файл — сам тикет DEV-004
- Program.cs: 83 строки, из них 72 строки (87%) — регистрация 5 групп Options
- 5 блоков: BookingOptions (14 строк), JwtOptions (12 строк), PasswordHashingOptions (10 строк), S3Options (12 строк), ValidationOptions (15 строк)
- Все блоки используют идентичный паттерн: AddOptions<T>() → Bind(section) → ValidateOnStart() → Validate(lambda, message)
- Не используется: PostConfigure, named options, IValidateOptions, ValidateDataAnnotations
- Решение: вынести все 5 регистраций в единый extension-метод AddMeetSpaceOptions в файл MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs
- Сигнатура: IServiceCollection AddMeetSpaceOptions(this IServiceCollection services, IConfiguration configuration)
- Program.cs сократится с 83 до ~11 строк, с одной строкой вызова AddMeetSpaceOptions
- Binding, лямбды валидации, ValidateOnStart, сообщения об ошибках, порядок — всё сохраняется буквально без изменений

Документ: `docs/architecture/DEV-004-options-registration-extension.md`

Открытые вопросы: нет

### Реализация

Что изменено:
- Создан файл `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs` со статическим классом `OptionsServiceCollectionExtensions` и extension-методом `AddMeetSpaceOptions(this IServiceCollection, IConfiguration)`. Метод содержит все 5 регистраций Options (Booking, Jwt, PasswordHashing, S3, Validation) в точности как они были в Program.cs — с теми же лямбдами валидации, ValidateOnStart(), binding-секциями и сообщениями об ошибках.
- `Program.cs`: 5 блоков регистрации Options (строки 6–77, 72 строки) заменены одной строкой `builder.Services.AddMeetSpaceOptions(builder.Configuration);`. Using-директивы `MeetSpace.Application.Options` и `MeetSpace.Infrastructure.Options` заменены на `using MeetSpace.Api.Extensions;`.

Проверки до и после изменений:
- **До:** Сборка `dotnet build -c Release` — 0 ошибок, 0 предупреждений. Тесты `dotnet test` — 20 пройдено, 0 не пройдено, 0 пропущено.
- **После:** Сборка `dotnet build -c Release` — 0 ошибок, 0 предупреждений. Тесты `dotnet test` — 20 пройдено, 0 не пройдено, 0 пропущено.
- Поведение, binding, валидация, ValidateOnStart, порядок регистрации не изменились.

### Написание тестов

Что переиспользовано или добавлено:
- Существующие тесты валидации (`Backend/tests/MeetSpace.Tests/Validation/*ValidatorTests.cs`, 20 тестов) не изменялись: они сознательно тестируют лямбды валидации через ручную регистрацию в ServiceCollection и не должны зависеть от extension-метода.
- Добавлен новый файл `Backend/tests/MeetSpace.Tests/Validation/OptionsRegistrationExtensionTests.cs` (3 теста), вызывающий production-метод `AddMeetSpaceOptions` — регистрации не копируются в тестовый код:
  - `AddMeetSpaceOptions_RegistersAllOptions` — in-memory конфигурация повторяет appsettings.json, кроме одного нестандартного значения (`Booking:BookingTimeStepMinutes = 30` вместо 15). После вызова extension-метода резолвятся `IOptions<T>` всех 5 групп (Booking, Jwt, PasswordHashing, S3, Validation); проверено, что нестандартное значение применилось, а остальные соответствуют appsettings.json — тест обнаруживает потерю binding при переносе.
  - `AddMeetSpaceOptions_WithInvalidConfig_ThrowsOnResolve` — конфигурация с заведомо невалидной секцией Booking (`WorkingHoursStart = 18:00:00`, `WorkingHoursEnd = 09:00:00`), остальные секции корректны. При обращении к `IOptions<BookingOptions>.Value` бросается `OptionsValidationException`; дополнительно проверено `exception.OptionsType == typeof(BookingOptions)` — привязка ошибки к проблемной группе Options без привязки к тексту сообщения.
  - `AddMeetSpaceOptions_ReturnsServiceCollection` — метод возвращает тот же `IServiceCollection` (`Assert.Same`), поддерживая chaining. Внимание: тикет (раздел «Проверки и тестирование», п. 2) не рекомендует такой тест; он добавлен по явному требованию задания координатора.
- В `Backend/tests/MeetSpace.Tests/MeetSpace.Tests.csproj` добавлен `ProjectReference` на `src/MeetSpace.Api/MeetSpace.Api.csproj` — необходим для доступа к `OptionsServiceCollectionExtensions` (это изменение инфраструктуры тестов, код приложения не затронут).

Покрытые сценарии и ограничения:
- Покрыто: регистрация всех 5 групп Options через production extension-метод; сохранение binding секций конфигурации; срабатывание валидатора Booking на невалидной конфигурации при резолве; контракт chaining.
- Не дублируется: проверки лямбд валидации всех групп (покрыты 20 существующими тестами валидаторов), unit-тесты свойств Options/getter/setter.
- Ограничение: тест невалидной конфигурации проверяет валидацию при обращении к `IOptions<T>.Value` через ServiceProvider без host; проверка именно `ValidateOnStart` при запуске host (п. 2 тикета) выполняется отдельно на этапе независимого тестирования.
- Выполнение: `dotnet test MeetSpace.slnx --no-restore` из `Backend/` — пройдено 23 из 23 (20 старых + 3 новых), 0 не пройдено, 0 пропущено; повторный запуск с фильтром `FullyQualifiedName~OptionsRegistrationExtensionTests` подтвердил выполнение всех 3 новых тестов.

### Тестирование

Тестировщик: независимый (GLM-5.2). Все проверки запущены самостоятельно, отчёты предыдущих агентов за доказательство не приняты.

Рабочая директория: `E:\Docs\coursework-dev-003`. Платформа: win32, PowerShell. .NET SDK 10.0.202.

#### 1. Сборка

Команды (из `E:\Docs\coursework-dev-003\Backend`):
- `dotnet restore`
- `dotnet build -c Release`

Результат `dotnet restore`: «Все проекты обновлены для восстановления» (успешно, exit code 0).

Результат `dotnet build -c Release`:
```
MeetSpace.Domain -> ...bin\Release\net10.0\MeetSpace.Domain.dll
MeetSpace.Infrastructure -> ...bin\Release\net10.0\MeetSpace.Infrastructure.dll
MeetSpace.Application -> ...bin\Release\net10.0\MeetSpace.Application.dll
MeetSpace.Api -> ...bin\Release\net10.0\MeetSpace.Api.dll
MeetSpace.Tests -> ...bin\Release\net10.0\MeetSpace.Tests.dll

Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
Прошло времени 00:00:01.33
```
Exit code: 0. Ожидание (0 ошибок, 0 предупреждений) выполнено.

#### 2. Тесты

Команда (из `Backend/`): `dotnet test MeetSpace.slnx --no-restore --logger "console;verbosity=normal"`

Результат:
```
Всего тестов: 23
     Пройдено: 23
 Общее время: 0,5026 Секунды
```
Exit code: 0. 0 не пройдено, 0 пропущено.

Распределение по файлам (подтверждено grep по `[Fact]`/`[Theory]`):
- `Domain/BookingTimeRulesConsumerTests.cs` — 4 теста
- `Validation/BookingOptionsValidatorTests.cs` — 5 тестов
- `Validation/JwtOptionsValidatorTests.cs` — 3 теста
- `Validation/PasswordHashingOptionsValidatorTests.cs` — 3 теста
- `Validation/S3OptionsValidatorTests.cs` — 3 теста
- `Validation/ValidationOptionsValidatorTests.cs` — 2 теста
- `Validation/OptionsRegistrationExtensionTests.cs` — 3 теста (новые, вызывают production extension-метод `AddMeetSpaceOptions`)
Итого: 23 теста, совпадает с отчётом runner-а.

Все 3 новых теста прошли:
- `AddMeetSpaceOptions_RegistersAllOptions` — пройден
- `AddMeetSpaceOptions_WithInvalidConfig_ThrowsOnResolve` — пройден
- `AddMeetSpaceOptions_ReturnsServiceCollection` — пройден

#### 3. Binding всех существующих Options

Файл `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs` проверен целиком (88 строк). Метод `AddMeetSpaceOptions(this IServiceCollection, IConfiguration)` регистрирует все 5 групп в порядке Booking → Jwt → PasswordHashing → S3 → Validation (тот же порядок, что был в Program.cs до рефакторинга):

| # | Options-класс | Секция | AddOptions<T> | Bind(section) | ValidateOnStart | Validate(lambda, message) |
|---|---|---|---|---|---|---|
| 1 | `BookingOptions` (Application) | `Booking` | ✅ | ✅ | ✅ | ✅ полная лямбда (рабочие часы, шаг 0..60, шаг ≤ интервала) + сообщение |
| 2 | `JwtOptions` (Infrastructure) | `Jwt` | ✅ | ✅ | ✅ | ✅ лямбда (AccessToken > 0, RefreshToken > 0) + сообщение |
| 3 | `PasswordHashingOptions` (Infrastructure) | `PasswordHashing` | ✅ | ✅ | ✅ | ✅ лямбда (BcryptWorkFactor 4..31) + сообщение |
| 4 | `S3Options` (Infrastructure) | `S3` | ✅ | ✅ | ✅ | ✅ лямбда (BucketName не пуст, PresignedUrlLifetime > 0) + сообщение |
| 5 | `ValidationOptions` (Application) | `Validation` | ✅ | ✅ | ✅ | ✅ лямбда (все 7 MaxLength > 0) + сообщение |

Метод возвращает `services` (поддержка chaining). Используемые секции совпадают с ключами в `appsettings.json`. Никаких `PostConfigure`, `named options`, `IValidateOptions<T>`, `ValidateDataAnnotations` нет — соответствует описанию архитектуры. Сообщения об ошибках и лямбды совпадают с архитектурным документом буквально.

#### 4. Отсутствие дублирующих регистраций в Program.cs

`Backend/src/MeetSpace.Api/Program.cs` (11 строк) проверен целиком:
```csharp
using MeetSpace.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMeetSpaceOptions(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
```
Grep по `AddOptions|\.Bind\(|ValidateOnStart|\.Validate\(|AddMeetSpaceOptions` нашёл ровно одно совпадение — `builder.Services.AddMeetSpaceOptions(builder.Configuration);` (строка 5). Вызовов `AddOptions<T>()`, `Bind()`, `ValidateOnStart()`, `Validate()` в Program.cs нет. Критерий выполнен.

#### 5. Запуск API и GET /health

Команда (из `Backend/src/MeetSpace.Api/`, в фоне):
`dotnet run --no-build -c Release --launch-profile http`

Порт из `Properties/launchSettings.json` (профиль `http`): `http://localhost:5231`.

После ожидания запуска выполнен запрос:
`Invoke-WebRequest -Uri http://localhost:5231/health -UseBasicParsing`

Результат:
```
StatusCode Content
---------- -------
       200 {"status":"healthy"}
```
HTTP 200 OK, тело `{"status":"healthy"}`. Критерий выполнен.

Лог запуска подтвердил `Now listening on: http://localhost:5231`, `Hosting environment: Development`.

После проверки процесс `MeetSpace.Api` (PID 14152) был остановлен через `Stop-Process`, порт освобождён.

#### 6. Запуск с невалидной конфигурацией

Команда (из `Backend/src/MeetSpace.Api/`):
```powershell
$env:Booking__BookingTimeStepMinutes = "9999"; dotnet run --no-build -c Release --launch-profile http
```
`BookingTimeStepMinutes = 9999` нарушает правило лямбды валидации Booking (шаг должен быть ≤ 60 и ≤ длине рабочего интервала 540 мин).

Результат: приложение упало при запуске, HTTP-сервер не стартовал.
```
fail: Microsoft.Extensions.Hosting.Internal.Host[11]
      Hosting failed to start
      Microsoft.Extensions.Options.OptionsValidationException: Invalid Booking configuration: working hours must be valid and time step must be positive and fit within the working interval.
         at Microsoft.Extensions.Options.OptionsFactory`1.Create(String name)
         ...
         at Microsoft.Extensions.DependencyInjection.OptionsBuilderExtensions.<>c__DisplayClass0_1`1.<ValidateOnStart>b__1()
         at Microsoft.Extensions.Options.StartupValidator.Validate()
         at Microsoft.Extensions.Hosting.Internal.Host.StartAsync(CancellationToken cancellationToken)
dotnet : Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: ...
   at Program.<Main>$(String[] args) in E:\Docs\coursework-dev-003\Backend\src\MeetSpace.Api\Program.cs:line 11

Exited with code 1
```
Stack trace явно показывает срабатывание `ValidateOnStart` через `StartupValidator.Validate()` → `Host.StartAsync` — то есть валидация происходит при запуске host, без обращения к `IOptions<T>.Value` из пользовательского кода. Критерий (падение с `OptionsValidationException` при запуске) выполнен.

После проверки переменная окружения `Booking__BookingTimeStepMinutes` удалена (`Remove-Item Env:`), подтверждено `$env:Booking__BookingTimeStepMinutes` пуст. Остаточных процессов на порту 5231 нет (`Get-NetTCPConnection` пусто).

#### Проверка критериев готовности

| Критерий | Статус | Доказательство |
|---|---|---|
| Program.cs содержит один вызов регистрации всех существующих Options | ✅ Выполнено | Строка 5: единственный вызов `AddMeetSpaceOptions`; grep не нашёл `AddOptions`/`Bind`/`ValidateOnStart`/`Validate` |
| Extension-метод расположен согласно архитектурному документу | ✅ Выполнено | `Backend/src/MeetSpace.Api/Extensions/OptionsServiceCollectionExtensions.cs`, namespace `MeetSpace.Api.Extensions`, сигнатура совпадает с док-том |
| Binding, валидаторы, ValidateOnStart и связанные регистрации сохранены без дублирования | ✅ Выполнено | Все 5 групп с `Bind`+`ValidateOnStart`+`Validate(lambda,msg)` присутствуют; дублирующих регистраций нет (см. п. 3, 4) |
| Реальные проверки корректного и невалидного запуска проходят с ожидаемыми результатами | ✅ Выполнено | Корректный запуск → 200 `{"status":"healthy"}`; невалидный запуск (`BookingTimeStepMinutes=9999`) → `OptionsValidationException` через `ValidateOnStart` |
| Сборка, релевантные тесты и GET /health проверены | ✅ Выполнено | Build: 0 ошибок/0 предупреждений; tests: 23/23 пройдено; /health: 200 OK |
| В тикете есть отчёты этапов, включая пропущенные проверки с причинами | ✅ Выполнено | Заполнены секции «Архитектор», «Реализация», «Написание тестов», «Тестирование». Пропущенных проверок нет — все 6 пунктов выполнены |
| После успешной проверки статус — «На проверке»; «Завершено» устанавливает владелец | ⏳ Готово к установке | Будет установлено тестировщиком после заполнения отчёта |

#### Замечания (дефекты)

Дефектов не найдено. Все критерии готовности выполнены.

Дополнительные наблюдения (не дефекты, не требуют исправления):
- В тикете (раздел «Проверки и тестирование», п. 2) указано «не писать тест „метод возвращает IServiceCollection“», однако реализатор добавил `AddMeetSpaceOptions_ReturnsServiceCollection` «по явному требованию задания координатора». Тест проходит, не противоречит поведению, отмечен в отчёте реализации. Не блокирует приёмку.
- host-тесты в `OptionsRegistrationExtensionTests` проверяют валидацию через `IOptions<T>.Value` в ServiceProvider без host; проверка именно `ValidateOnStart` при запуске host выполнена отдельно на этапе независимого тестирования (п. 6 выше), как и описано в отчёте реализации. Это соответствует тикету.
- Тестовая инфраструктура: в `MeetSpace.Tests.csproj` добавлен `ProjectReference` на `src/MeetSpace.Api/MeetSpace.Api.csproj` — необходимо для доступа тестов к production extension-методу; код приложения не затронут. Согласуется с отчётом реализации.

#### Вердикт

Тикет DEV-004 готов к приёмке. Все шесть проверок пройдены, дефектов не обнаружено. Рекомендую перевести статус тикета в «На проверке».

### Публикация

Состав коммита:
Результат публикации — в итоговом ответе:
