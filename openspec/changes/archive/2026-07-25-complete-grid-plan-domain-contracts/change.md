# Complete GridPlan Domain Contracts

## Scope

- Добавить `InstrumentRules.MinQuantity` (строго `Quantity`).
- Проверять Quantity после округления по QuantityStep.
- Проверять Notional после округления Price и Quantity.
- Отклонять весь GridPlan при нарушении minimum любого уровня.
- Добавить вычисляемые агрегаты:
  - `TotalQuantity` (тип `Quantity`);
  - `TotalNotional` (тип `Money`);
  - `ExpectedVwap` (тип `Price`).
- Рассчитывать агрегаты только из неизменяемых Levels.
- Явно отделить плановый VWAP от фактической средней позиции.
- Добавить unit-тесты инвариантов.
- Синхронизировать `ROADMAP.md` после успешной проверки.

## Вне Scope

- SQLite
- Outbox
- TP
- SL
- фактический VWAP позиции
- Quantower integration
- миграции
- TradeCycle lifecycle
- Cumulative-профиль отдельных уровней явно не входит в текущий change.
- Сложная иерархия исключений (новые типы Exception).

## Семантика ExpectedVwap

`GridPlan.ExpectedVwap` является плановым VWAP полного GridPlan:
- рассчитывается из плановых округленных Price и Quantity;
- не учитывает фактические partial fills;
- не учитывает slippage;
- не учитывает комиссии;
- не заменяет `Position.OpenPrice`;
- не используется как доказательство фактической средней цены позиции.

Фактическая средняя цена определяется только из подтвержденного position snapshot или будущего подтвержденного execution ledger.
Формула VWAP рассчитывается как точное decimal-отношение `TotalNotional / TotalQuantity` и не округляется повторно к `TickSize` внутри GridPlan.

## Порядок расчета уровня

1. Рассчитать исходную Price.
2. Округлить Price по TickSize.
3. Рассчитать исходную Quantity.
4. Округлить Quantity по QuantityStep.
5. Проверить Quantity > 0.
6. Проверить Quantity >= MinQuantity.
7. Рассчитать Notional из округленных Price и Quantity.
8. Проверить Notional >= MinNotional.
9. Только после успешных проверок создать GridLevel.

## Ошибка minimum constraints

При нарушении `MinQuantity` или `MinNotional` использовать уже существующий тип ошибки расчета (например, `InvalidOperationException`).
В сообщении обязательно указать:
- индекс уровня;
- нарушенное ограничение;
- фактические значения, относящиеся к этому ограничению (например, округленную Quantity и MinQuantity при нарушении по объему).

## Инварианты GridPlan

- `Levels` не может быть `null`.
- `Levels` должен содержать минимум один уровень Entry.
- Первый уровень имеет `Index = 0`.
- Levels копируются при создании GridPlan (внешний код не может изменить коллекцию).
- Публичный конструктор не принимает готовые значения агрегатов.
- `TotalQuantity` всегда строго больше нуля. Поэтому деление при расчете `ExpectedVwap` всегда допустимо.

## Testing Strategy (Обязательные тесты)

### MinQuantity Validation
- Создание отрицательного `Quantity` отклоняется доменным типом.
- `InstrumentRules` дополнительно отклоняет `MinQuantity == 0`.
- Валидный положительный MinQuantity сохраняется без изменения.

### Minimum after rounding
- Raw Quantity проходит `MinQuantity`. После округления вниз Quantity становится меньше `MinQuantity`. GridCalculator отклоняет весь план.
- Raw Price и Quantity дают допустимый Notional. После округления итоговый Notional становится меньше `MinNotional`. GridCalculator отклоняет весь план.

### Плановые агрегаты
Для каждого плана проверить:
- `TotalQuantity = сумма level.Quantity`
- `TotalNotional = сумма level.Price × level.Quantity`
- `ExpectedVwap = TotalNotional / TotalQuantity`
(Точный дробный VWAP с неудобными числами, где среднее значение не является целым).

### Неизменяемость
- Конструктор `GridPlan` копирует входную коллекцию; добавление/удаление в исходном списке не меняет созданный план.

### Тест Entry-only
- `GridPlan` содержит ровно один уровень с Index=0
- Проверить: `TotalQuantity = Entry.Quantity`, `TotalNotional = Entry.Price × Entry.Quantity`, `ExpectedVwap = Entry.Price`.

## Definition of Done

- [x] 1. `InstrumentRules` содержит `MinQuantity`.
- [x] 2. Quantity проверяется после округления.
- [x] 3. Notional проверяется после округления Price и Quantity.
- [x] 4. Нарушение minimum дает явную ошибку.
- [x] 5. `GridPlan` предоставляет `TotalQuantity`.
- [x] 6. `GridPlan` предоставляет `TotalNotional`.
- [x] 7. `GridPlan` предоставляет плановый `ExpectedVwap`.
- [x] 8. Плановый VWAP явно отделен от фактической средней цены позиции.
- [x] 9. Агрегаты не могут рассинхронизироваться с уровнями.
- [x] 10. Добавлены тесты для Entry-only и нескольких DCA-уровней.
- [x] 11. Добавлены граничные тесты `MinQuantity` и `MinNotional`.
- [x] 12. Этапы 1–6 корректно отмечены в `ROADMAP.md`.
- [x] 13. Release build и все тесты проходят.
- [x] 14. `MinQuantity` использует доменный тип `Quantity`.
- [x] 15. `MinQuantity` обязана быть больше нуля.
- [x] 16. Недопустимый уровень не пропускается и не корректируется автоматически. Весь расчет завершается явной ошибкой.
- [x] 17. `ExpectedVwap` рассчитывается из округленных Price и Quantity, но сам не округляется повторно к TickSize.
- [x] 18. Агрегаты вычисляются только из immutable Levels и не передаются независимо через публичный конструктор.
- [x] 19. Cumulative-профиль отдельных уровней явно не входит в текущий change.
- [x] 20. Существующие call sites `InstrumentRules` обновлены без изменения прежней семантики остальных параметров.
- [x] 21. `git diff --check` проходит.
- [x] 22. Strict review подтверждает отсутствие изменений вне scope.
- [x] 23. `GridPlan.Levels` всегда содержит минимум Entry-уровень.
- [x] 24. Конструктор `GridPlan` копирует входную коллекцию; изменение исходного списка не изменяет созданный план.
- [x] 25. Доменные типы агрегатов зафиксированы: Quantity, Money и Price.
- [x] 26. Проверки `MinQuantity` и `MinNotional` выполняются только над окончательно округленными значениями.
- [x] 27. Ошибка minimum содержит индекс уровня и фактические значения, но новый сложный framework исключений не создается.
