# DEV-002 — Проектирование и реализация сущностей

Статус: На проверке

## Цель

Спроектировать и реализовать сущности, перечисленные
в goal.md, с согласованными полями, связями и ограничениями.

## Источники

- AGENTS.md — правила работы.
- goal.md — требования и перечень сущностей.
- Существующая структура .NET-проекта.

## Этап 1. Проектирование

Результат: docs/domain-model.md.

Для каждой сущности описать:
- назначение;
- поля и их типы на C#;
- обязательность и допустимые значения;
- идентификатор;
- связи и их кратность;
- ограничения и правила изменения данных.

Отдельно определить:
- представление ролей пользователя;
- связь комнаты с оборудованием;
- представление даты, времени и часового пояса бронирования;
- правило корректности интервала бронирования;
- где должна проверяться занятость комнаты;
- хранение хеша пароля;
- хранение refresh-токена и правила его жизненного цикла.

Не добавлять поля без объяснения их назначения.
Существенные неоднозначности уточнить у владельца.
Не вводить новые архитектурные слои без необходимости.

## Этап 2. Реализация

- Реализовать сущности согласно docs/domain-model.md.
- Следовать структуре и соглашениям существующего проекта.
- Добавить проверки для реализованных нетривиальных правил.
- Проверить сборку и выполнить применимые тесты.
- Зафиксировать отклонения от документа, если они согласованы.

## Границы

В задачу не входят:
- API и пользовательский интерфейс;
- полноценная авторизация и выдача токенов;
- CRUD-сервисы;
- подключение БД и миграции.

Если для реализации требуется решение вне этих границ,
сначала согласовать его.

## Критерии готовности

- Все сущности из goal.md описаны и реализованы.
- Поля, типы и связи соответствуют документу.
- Решения по ограничениям и секретам явно описаны.
- dotnet build завершается успешно.
- Применимые тесты выполнены, результаты записаны.
- В тикете есть отчёты архитектора и разработчика.
- Статус установлен «На проверке».

## Отчёт архитектора

**Дата:** 2026-10-31

**Что спроектировано:**

1. **User** — 7 полей: Id (Guid), FirstName, LastName (string [1..100]), Email (string, unique, lowercase), PasswordHash (BCrypt), ProfilePhotoKey (string?, S3-ключ), Role (enum UserRole). Связи: Booking (1:N), RefreshToken (1:N). Роль по умолчанию — User.

2. **Room** — 6 полей: Id (Guid), Name (string [1..200], unique), Capacity (int ≥1), Floor (int ≥1), Description (string? [0..2000]), PhotoKey (string?, S3-ключ). Связи: Equipment (M:N через RoomEquipment), Booking (1:N).

3. **Equipment** — 2 поля: Id (Guid), Name (string [1..50], unique). Связь M:N с Room через RoomEquipment. Сид: 6 типов.

4. **RoomEquipment** — составной ключ (RoomId, EquipmentId). Явная промежуточная сущность (не только EF-навигация), чтобы модель была прозрачной и допускала расширение.

5. **Booking** — 8 полей: Id (Guid), Title (string [1..200]), Description (string? [0..2000]), UserId, RoomId, Date (DateTime, Kind=Unspecified), StartTime (TimeSpan), EndTime (TimeSpan). Дата и время разделены: Date хранит день, TimeSpan — интервал. Минутная часть кратна 15. Рабочие часы 09:00–18:00. Проверка занятости — в Application/DataAccess (пересечение интервалов по RoomId + Date).

6. **RefreshToken** — 5 полей: Id (Guid), Token (crypto-random, unique), UserId, ExpiresAt (Utc, +14 дней), CreatedAt (Utc). Жизненный цикл: ротация при обновлении, удаление при logout, очистка просроченных.

**Ключевые решения:**
- Роли — enum UserRole (User=1, Manager=2), не таблица (фиксированный набор).
- Дата/время бронирования — Date (DateTime) + TimeSpan (StartTime, EndTime). Разделение упрощает проверки пересечений и отражает бизнес-модель.
- RoomEquipment — явная join-сущность с составным PK.
- Все константы вынесены в `DomainConstants`.
- Валидация бизнес-правил на уровне Domain (инварианты) и Application (правила, требующие внешнего контекста, например, «не в прошлом»).

**Неоднозначности:**

Блокирующих неоднозначностей нет. Все решения по списку из тикета приняты:
- Роли → enum.
- Room ↔ Equipment → явная M:N через RoomEquipment.
- Дата/время бронирования → Date + TimeSpan.
- Корректность интервала → EndTime > StartTime, шаг 15 минут, 09:00–18:00.
- Проверка занятости → Application + DataAccess.
- Хеш пароля → BCrypt, work factor 12.
- Refresh-токен → ротация, TTL 14 дней, удаление при logout.

Документ: `docs/domain-model.md`.

## Отчёт разработчика

**Дата:** 2026-10-31

**Что реализовано:**

1. **Проект `MeetSpace.Domain`** — создан проект Class Library (net10.0) в `Backend/src/MeetSpace.Domain/`, добавлен в `MeetSpace.slnx`.

2. **Enum `UserRole`** — `MeetSpace.Domain.Enums.UserRole` (User = 1, Manager = 2).

3. **Константы `DomainConstants`** — `MeetSpace.Domain.Constants.DomainConstants`:
   - WorkingHoursStart/End (09:00/18:00), BookingTimeStepMinutes (15)
   - AccessTokenLifetimeMinutes (10), RefreshTokenLifetimeDays (14)
   - BcryptWorkFactor (12), S3BucketName, PresignedUrlLifetimeMinutes (60)
   - Длины строк: UserNameMaxLength (100), EmailMaxLength (255), EquipmentNameMaxLength (50), RoomNameMaxLength (200), RoomDescriptionMaxLength (2000), BookingTitleMaxLength (200), BookingDescriptionMaxLength (2000)

4. **Сущность `User`** — `MeetSpace.Domain.Entities.User`:
   - Поля: Id (Guid, PK), FirstName, LastName, Email, PasswordHash, ProfilePhotoKey (string?), Role (UserRole)
   - Связи: Bookings (1:N), RefreshTokens (1:N)
   - Валидация: непустые строки, макс. длины, Email → ToLowerInvariant

5. **Сущность `Equipment`** — `MeetSpace.Domain.Entities.Equipment`:
   - Поля: Id (Guid, PK), Name
   - Связь: RoomEquipments (M:N через RoomEquipment)

6. **Сущность `RoomEquipment`** — `MeetSpace.Domain.Entities.RoomEquipment`:
   - Составной ключ: RoomId + EquipmentId
   - Связи: Room (N:1), Equipment (N:1)

7. **Сущность `Room`** — `MeetSpace.Domain.Entities.Room`:
   - Поля: Id (Guid, PK), Name, Capacity (int ≥1), Floor (int ≥1), Description (string?), PhotoKey (string?)
   - Связи: RoomEquipments (M:N), Bookings (1:N)

8. **Сущность `Booking`** — `MeetSpace.Domain.Entities.Booking`:
   - Поля: Id (Guid, PK), Title, Description (string?), UserId (FK), RoomId (FK), Date (DateTime, Kind=Unspecified, только дата), StartTime (TimeSpan), EndTime (TimeSpan)
   - Инварианты: EndTime > StartTime, рабочие часы 09:00–18:00, минутная часть кратна 15
   - Связи: User (N:1), Room (N:1)

9. **Сущность `RefreshToken`** — `MeetSpace.Domain.Entities.RefreshToken`:
   - Поля: Id (Guid, PK), Token, UserId (FK), ExpiresAt (Utc), CreatedAt (Utc)
   - ExpiresAt автоматически = CreatedAt + 14 дней
   - Связь: User (N:1)

**Результаты проверок:**
- `dotnet build MeetSpace.slnx` — успешно, 0 ошибок, 0 предупреждений.
- Тестовые проекты отсутствуют в решении — тесты не применимы на данном этапе.

**Отклонения от документа:** нет. Все поля, типы, связи и правила реализованы в точном соответствии с `docs/domain-model.md`.

**Замечания:**
- Проверка «не в прошлом» и проверка занятости комнаты оставлены на Application/DataAccess слой (как и указано в документе), поскольку требуют внешнего контекста.
- Валидация BCrypt и криптостойкость токена являются ответственностью Infrastructure/Application слоёв — здесь только хранение.