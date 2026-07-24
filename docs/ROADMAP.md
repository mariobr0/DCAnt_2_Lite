# Roadmap DCAnt 2.0

## 1. Принцип разработки

DCAnt 2.0 разрабатывается небольшими проверяемыми этапами.

Каждый этап должен доказывать одну конкретную способность бота. Новый уровень сложности добавляется только после того, как предыдущий уровень подтвержден тестами и наблюдением на демо-счете.

Основная последовательность:

```text
Чистая математика
→ последовательный EngineLoop
→ подтвержденные контракты Quantower
→ сохранение состояния
→ настоящий Outbox
→ один Entry
→ один Exit
→ защитные ордера
→ один DCA-уровень
→ полная DCA-сетка
→ дополнительные функции
```

Запрещено использовать временные предположения о Quantower как основу production-логики.

Если контракт внешней системы не подтвержден, соответствующая функция считается заблокированной, а не завершенной.

***

## 2. Текущий статус

* [x] Этап 1. Каркас проекта
* [x] Этап 2. Базовые типы и округление
* [x] Этап 3. DCA-математика
* [x] Этап 4. Базовая торговая модель
* [ ] Этап 5. Надежный EngineLoop
* [ ] Этап 6. Контракты одного ордера Quantower
* [ ] Этап 7. Хранилище торгового состояния
* [ ] Этап 8. Persistent Outbox
* [ ] Этап 9. Один Entry с partial fills
* [ ] Этап 10. Один Exit с partial fills
* [ ] Этап 11. Take Profit
* [ ] Этап 12. Stop Loss
* [ ] Этап 13. Безопасная замена защитного ордера
* [ ] Этап 14. Restart и reconnect
* [ ] Этап 15. Один DCA-уровень
* [ ] Этап 16. Многоуровневая DCA-сетка
* [ ] Этап 17. Автоматический новый цикл
* [ ] Этап 18. Дополнительные торговые функции

Текущий активный change:

`Не выбран`

Рабочая ветка восстановления:

`rebuild-live-core`

Исходная точка:

`93b21db`

***

# 3. Завершенный фундамент

## Этап 1. Каркас проекта

Созданы:

* solution;
* production-проекты;
* тестовые проекты;
* направления зависимостей;
* общие настройки сборки;
* базовые правила оформления.

Этап считается завершенным.

## Этап 2. Базовые типы и округление

Созданы или подлежат переносу после проверки:

* `StrategyInstanceId`;
* `TradeCycleId`;
* `InternalOrderId`;
* `EffectId`;
* `ClientRequestId`;
* `BrokerOrderId`;
* `ExecutionId`;
* `Price`;
* `Quantity`;
* `Money`;
* `InstrumentRules`.

Перед использованием ID-типы должны запрещать пустые значения.

Округление должно использовать реальные:

* `TickSize`;
* `QuantityStep`;
* `MinQuantity`;
* `MinNotional`.

Этап считается завершенным только для чистой доменной модели. Соответствие полям Quantower проверяется отдельно.

## Этап 3. DCA-математика

Единственным источником расчета сетки должен быть `GridCalculator`.

`TradeCycle`, Stop Loss, UI и live-стратегия не должны повторять формулы сетки самостоятельно.

`GridPlan` должен заранее определять:

* цены;
* количества;
* planned notional;
* общий капитал;
* цену последнего уровня;
* ожидаемый VWAP после каждого уровня.

## Этап 4. Базовая торговая модель

Базовые модели могут быть сохранены, но live lifecycle будет реализован заново после подтверждения контрактов Quantower.

***

# 4. Новый путь реализации live-контура

## Этап 5. Надежный EngineLoop

### Цель

Создать последовательный обработчик сообщений, не связанный с Quantower, SQLite и торговыми ордерами.

### Реализовать

* `EngineMessage`;
* один `Channel`;
* одного consumer;
* последовательную обработку;
* состояния `Created`, `Running`, `Stopping`, `Stopped`, `Faulted`;
* controlled shutdown;
* обработку panic;
* запрет приема сообщений после остановки;
* контролируемую очередь.

### Не входит

* Quantower Adapter;
* SQLite;
* Outbox;
* Entry;
* TP;
* SL;
* DCA-сетка;
* автоматический restart торгового цикла.

### Проверить

* сообщения обрабатываются строго последовательно;
* одинаковая последовательность дает одинаковый результат;
* сообщение не теряется молча;
* panic переводит EngineLoop в `Faulted`;
* после panic очередь не растет бесконечно;
* shutdown не обрывает уже принятое критическое сообщение без явного результата.

### Change

`implement-engine-loop-foundation`

***

## Этап 6. Контракты одного ордера Quantower

### Цель

Установить фактическое поведение Quantower и текущего брокерского коннектора до написания production adapter.

### Проверить

#### Создание ордера

* что означает результат `PlaceOrder`;
* когда появляется broker order;
* сохраняется ли `Comment`;
* доступен ли настоящий `ClientRequestId`;
* когда появляется `BrokerOrderId`;
* может ли Execution прийти раньше статуса `Opened`.

#### Partial fills

* является ли `FilledQuantity` накопительным значением;
* что содержит `AverageFillPrice`;
* существует ли отдельный поток сделок;
* доступен ли стабильный `ExecutionId`;
* в каком порядке приходят partial и final events;
* могут ли события повторяться;
* может ли `Filled` прийти без предыдущего `PartiallyFilled`.

#### Cancel

* что означает успешный результат `CancelOrder`;
* какое событие подтверждает реальную отмену;
* когда ордер исчезает из active orders;
* может ли fill прийти после запроса Cancel;
* может ли fill прийти после события Cancelled;
* что происходит при повторном Cancel.

#### Позиция

* как получить фактический quantity;
* как получить фактическую среднюю цену;
* когда position snapshot обновляется относительно Execution;
* как представляется отсутствие позиции;
* как ведет себя позиция после partial exit.

#### Restart и reconnect

* какие активные ордера восстанавливаются;
* сохраняются ли Comments и IDs;
* какая история Executions доступна;
* насколько глубока история;
* повторяются ли старые callbacks.

### Результат

Создать краткий документ результатов contract tests.

Каждый контракт получает состояние:

* `Confirmed`;
* `Rejected`;
* `ConnectorSpecific`;
* `Inconclusive`;
* `NotSupported`.

### Ограничение

Запрещено создавать production `ExecutionMessage` из `OrderHistoryAdded`, пока не доказано, что событие содержит отдельный финансовый факт.

### Change

`verify-quantower-single-order-contracts`

***

## Этап 7. Хранилище торгового состояния

### Цель

Научиться сохранять и загружать локальное состояние без отправки торговых команд.

### Сохранять

* Strategy Instance;
* TradeCycle;
* статус TradeCycle;
* сторону сделки;
* локальный position snapshot;
* ManagedOrders;
* `InternalOrderId`;
* `BrokerOrderId`;
* `ClientRequestId`;
* роль ордера;
* статус ордера;
* cumulative filled quantity;
* SchemaMigrations.

### Не входит

* Outbox dispatch;
* автоматические retry;
* восстановление позиции только из локальной базы;
* генерация синтетических Executions;
* новая сделка после restart.

### Проверить

* все значения сохраняются атомарно;
* `ExitOnly` после restart остается `ExitOnly`;
* `DcaPaused` после restart остается `DcaPaused`;
* ID не теряются;
* decimal сохраняется независимо от системной локали;
* разные экземпляры стратегии, счета и инструменты не смешиваются;
* foreign keys реально включены;
* старая миграция не редактируется после применения.

### Change

`implement-trading-state-store`

***

## Этап 8. Persistent Outbox

### Цель

Обеспечить правило:

```text
Persist
→ Commit
→ Dispatch
→ Record result
```

### Реализовать

Каждый внешний effect должен иметь:

* `EffectId`;
* `TradeCycleId`;
* тип;
* payload;
* статус;
* время создания;
* время отправки;
* число попыток;
* последний результат или ошибку.

Минимальные состояния:

* `Pending`;
* `Dispatching`;
* `Sent`;
* `Acknowledged`;
* `Failed`;
* `Unknown`;
* `Superseded`.

### Проверить

* API не вызывается до commit SQLite;
* сбой после commit, но до dispatch, оставляет effect в `Pending`;
* сбой после dispatch, но до ответа, оставляет effect в `Unknown`;
* `Unknown` effect не повторяется автоматически;
* restart находит незавершенные effects;
* recovery запускает reconciliation до решения о повторе;
* один `EffectId` не dispatch дважды незаметно.

### Не входит

* TP;
* SL;
* grid;
* автоматический новый торговый цикл.

### Change

`implement-persistent-outbox`

***

# 5. Минимальный live vertical slice

## Этап 9. Один Entry с partial fills

### Цель

Доказать надежное открытие одной позиции одним ордером.

### Сценарий

```text
Ручной Start
→ один Entry order
→ один или несколько partial fills
→ Filled или Cancelled
→ broker position snapshot
→ остановка теста
```

### Ограничения

* без TP;
* без SL;
* без DCA-сетки;
* без автоматического нового цикла;
* без `ModifyOrder`;
* без повторной отправки Entry после timeout.

### Упрощенная обработка partial fills

Если отдельные Executions еще не подтверждены:

```text
PartiallyFilled или Filled
→ отметить состояние Dirty
→ дождаться короткой паузы
→ получить broker position snapshot
→ заменить локальные quantity и VWAP
```

Не прибавлять `FilledQuantity` вслепую.

### Проверить

* Entry создается один раз;
* partial fill не считается полным исполнением;
* повторный callback не увеличивает позицию повторно;
* локальная позиция после reconciliation совпадает с брокером;
* timeout не создает второй Entry;
* restart не учитывает старый fill повторно;
* завершение теста не открывает новую позицию.

### Change

`implement-single-entry-lifecycle`

***

## Этап 10. Один Exit с partial fills

### Цель

Доказать надежное уменьшение и полное закрытие позиции одним exit-order.

### Сценарий

```text
Подтвержденная позиция
→ один Exit order
→ partial fill
→ следующий partial fill
→ Filled
→ broker position snapshot
→ подтвержденный zero
→ Completed
→ остановка
```

### Проверить

* partial fill уменьшает фактическую позицию;
* TradeCycle не завершается при первом partial fill;
* `Filled` сам по себе не является достаточным доказательством полного выхода;
* цикл завершается только после подтвержденного broker position zero;
* позднее событие не теряется;
* duplicate event не меняет позицию повторно;
* новый цикл автоматически не запускается.

### Change

`implement-single-exit-lifecycle`

***

# 6. Защитные ордера

## Этап 11. Один Take Profit

### Цель

Добавить один TP к подтвержденной позиции.

### Проверить

* TP создается на фактический quantity;
* TP имеет доказанный ownership;
* partial TP не завершает цикл;
* остаток TP сопоставляется с остатком позиции;
* полный TP запускает reconciliation;
* цикл завершается только после broker position zero;
* старый TP не остается активным после завершения;
* новый цикл не начинается автоматически.

### Не входит

* Stop Loss;
* одновременные TP и SL;
* замена TP;
* DCA.

### Change

`implement-single-take-profit`

***

## Этап 12. Один Stop Loss

### Цель

Добавить один SL отдельно от TP.

### Проверить

* тип Stop order поддерживается connector;
* TriggerPrice округляется корректно;
* reduce-only действительно работает;
* partial SL не завершает цикл;
* полный SL подтверждается broker position snapshot;
* старый SL не остается после полного выхода.

### Не входит

* одновременный TP;
* замена SL;
* OCO;
* Last Level Stop.

### Change

`implement-single-stop-loss`

***

## Этап 13. Безопасная замена защитного ордера

### Цель

Реализовать подтвержденный процесс `Cancel → Confirm → Place`.

### Машина состояний

```text
Active
→ CancelRequested
→ CancelConfirmed
→ ReplacementPending
→ ReplacementAccepted
→ Active
```

Дополнительные состояния:

```text
PartiallyFilled
Unknown
Failed
```

### Правила

* старый ордер не удаляется из registry при запросе Cancel;
* поздний fill старого ордера всегда учитывается;
* новый ордер не создается до подтверждения отмены;
* после подтверждения выполняется повторная проверка broker position;
* replacement quantity рассчитывается от фактической позиции;
* timeout Cancel приводит к reconciliation;
* timeout Place приводит к reconciliation;
* одновременное существование двух полных защитных ордеров не допускается без подтвержденного OCO-контракта.

### Change

`implement-safe-protection-replacement`

***

## Этап 14. Restart и reconnect

### Цель

Восстановить одиночную защищенную сделку до добавления DCA.

### Порядок запуска

```text
Открыть SQLite
→ применить migrations
→ загрузить локальное состояние
→ заблокировать новую торговлю
→ получить broker position
→ получить owned active orders
→ получить доступные Executions или fill progress
→ обработать pending/unknown effects
→ reconciliation
→ разрешить продолжение сопровождения
```

### Проверить restart в точках

* до dispatch Entry;
* после dispatch, но до ответа;
* после partial Entry;
* после полного Entry;
* при активном TP;
* во время Cancel;
* после partial Exit;
* после broker position zero, но до локального Completed.

### Результат reconciliation

* `Consistent`;
* `Recoverable`;
* `Ambiguous`;
* `Fatal`.

При `Ambiguous` увеличение позиции запрещено.

### Change

`implement-protected-trade-recovery`

***

# 7. Возвращение DCA-сетки

## Этап 15. Один DCA-уровень

### Цель

Добавить ровно один grid-order к уже надежной защищенной сделке.

### Сценарий

```text
Entry
→ подтвержденная позиция
→ TP/SL
→ один Grid order
→ partial fill
→ position reconciliation
→ безопасное обновление защиты
→ полный выход
```

### Проверить

* Grid order создается один раз;
* partial fill не завершает grid-level;
* место active window не освобождается преждевременно;
* позиция берется из broker snapshot или подтвержденных Executions;
* TP/SL обновляются по безопасной процедуре;
* timeout Grid order не вызывает слепой retry;
* restart не создает Grid order повторно.

### Change

`implement-one-grid-level`

***

## Этап 16. Многоуровневая сетка

### Цель

Расширить проверенный один уровень до полного `GridPlan`.

### Добавлять постепенно

1. Два DCA-уровня.
2. Active grid window.
3. Несколько partial fills разных уровней.
4. Step scaling.
5. Volume scaling.
6. Capital limit.
7. DcaPaused.
8. ExitOnly.

### Правила

* live-код использует только `GridCalculator`;
* Last Level Stop использует цену последнего уровня `GridPlan`;
* весь planned capital проверяется до Entry;
* один уровень имеет один логический order intent;
* Filled, Cancelled и Rejected имеют разные переходы;
* ошибка одного grid-order не отключает сопровождение позиции.

### Change

Разделить минимум на:

* `implement-multi-level-grid`;
* `implement-active-grid-window`;
* `implement-dca-pause`;
* `implement-exit-only`.

***

## Этап 17. Автоматический новый цикл

### Цель

Вернуть непрерывную работу только после доказанного завершения старого цикла.

### Новый цикл разрешен, только если

* broker position равна нулю;
* локальная позиция равна нулю;
* нет активных owned Entry/Grid orders;
* нет активных owned TP/SL;
* нет `Pending` или `Unknown` effects;
* reconciliation завершен как `Consistent`;
* предыдущий TradeCycle сохранен как `Completed`.

### Проверить

* late event после Completed;
* старый TP или SL после завершения;
* delayed cancel confirmation;
* restart между Completed и новым Start;
* duplicate timer;
* ручной ордер на том же символе;
* два экземпляра стратегии.

### Change

`implement-automatic-cycle-restart`

***

# 8. Дополнительные функции

## Этап 18. Дополнительные торговые функции

Добавлять только по одной после стабильной многоуровневой сетки.

Предлагаемый порядок:

1. Last Level Stop.
2. Static Take Profit policies.
3. Dynamic NATR Take Profit.
4. Initial Entry Drop Stop.
5. Max PnL Stop.
6. Time Stagnation.
7. Rescue Mode.
8. Smart Entry EMA.
9. Smart Entry MFI.
10. Global Trend Filter.
11. Auto Reinvest.
12. Short.

Каждая функция реализуется отдельным `change.md`.

Новая функция не должна модифицировать базовый order lifecycle без отдельного обоснования.

***

# 9. Стратегия тестирования

## Unit tests

Проверяют:

* value objects;
* округление;
* `GridCalculator`;
* VWAP;
* calculation policies;
* валидацию настроек;
* чистые переходы состояния.

## Transition tests

Используют модель:

```text
State + Message
→ New State + Persisted Effects
```

Проверяют не только итоговое состояние, но и запрещенные эффекты.

Пример:

```text
TP PartiallyFilled
→ TradeCycle не Completed
→ StartNewCycle effect отсутствует
```

## Integration tests

Проверяют:

* SQLite migrations;
* сохранение и загрузку;
* invariant decimal;
* foreign keys;
* атомарность state + effect;
* lifecycle Outbox;
* restart recovery.

## Quantower contract tests

Проверяют только фактическое поведение Quantower и connector.

Результат contract test нельзя заменять предположением или unit-тестом с mock.

## Live scenario tests

Проводятся на демо-счете.

Каждый тест:

* выполняет один заранее определенный сценарий;
* имеет конкретный критерий завершения;
* после завершения останавливается;
* не запускает автоматически новый торговый цикл;
* сохраняет подробный лог;
* проверяет фактическую broker position и active orders.

## Обязательные сценарии

* один полный Entry;
* partial Entry;
* duplicate callback;
* rejection;
* timeout;
* Cancel confirmation;
* cancel/fill race;
* partial Exit;
* late event;
* restart;
* reconnect;
* неизвестный effect;
* broker position, не совпадающая с локальной.

Markdown-описание не заменяет автоматический тест или результат contract test.

***

# 10. Минимальный Definition of Done

Изменение считается завершенным, если:

1. Реализация соответствует `Требуемому поведению` из `change.md`.
2. Scope не расширен.
3. Release-сборка проходит.
4. Применимые тесты проходят.
5. Неподтвержденный контракт Quantower не представлен как установленный факт.
6. Внешний effect сохраняется до dispatch, если изменение затрагивает торговую команду.
7. Для timeout определено состояние `Unknown` или иной явный безопасный результат.
8. Торговая команда не повторяется вслепую.
9. Ордер не удаляется из registry до подтвержденного терминального состояния.
10. Partial fill не считается полным исполнением.
11. Duplicate event не изменяет финансовое состояние повторно.
12. TradeCycle не завершается без подтверждения фактической нулевой позиции.
13. Новый TradeCycle не начинается при старой позиции, активном ордере или неизвестном effect.
14. Для сохраняемого состояния проверен restart.
15. Важные исключения не подавляются.
16. Логи содержат ID цикла, ордера, effect и broker order, если применимо.
17. Чекбоксы отражают фактическое состояние.
18. Не добавлена лишняя архитектурная сложность.

Не каждое изменение требует всех пунктов. Применимость определяется scope текущего `change.md`.

***

# 11. Стоп-условия разработки

Реализацию следующего этапа нельзя начинать, если:

* текущий этап не имеет проверяемого критерия готовности;
* важный контракт Quantower остается `Inconclusive`;
* существуют падающие тесты текущего поведения;
* local position расходится с broker position;
* после теста остаются неизвестные owned orders;
* restart повторно учитывает старое исполнение;
* timeout приводит к автоматическому повтору торговой команды;
* старый защитный ордер забывается до подтверждения отмены;
* тест требует ручного объяснения, почему неправильный результат «временно допустим».

В такой ситуации нужно исправлять текущий этап, а не добавлять следующую функцию.

***

# 12. Что отложено

До завершения базового MVP откладываются:

* Short;
* одновременные Long и Short;
* Hedge Mode;
* Netting Mode;
* dynamic ranges;
* portfolio supervisor;
* multi-account;
* multi-symbol runtime;
* funding allocation;
* multi-currency conversion;
* машинное обучение;
* walk-forward framework;
* Monte Carlo framework;
* автоматическое A/B-тестирование;
* сложные dashboards;
* автоматические UI-тесты;
* Web-симулятор сетки;
* замена безопасного `Cancel → Confirm → Place` на `ModifyOrder`;
* универсальная торговая платформа;
* выделение общего framework для нескольких ботов.

`ModifyOrder` может быть рассмотрен только после contract test, подтверждающего:

* поведение broker order ID;
* сохранение ownership;
* отмену старой версии ордера;
* отсутствие дубликатов;
* взаимодействие с reduce-only;
* поведение partial fills;
* корректность после reconnect.

Отложенная функция не должна влиять на архитектуру текущего этапа.

***

## Что принципиально изменилось

Старый Roadmap объединял:

```text
EngineLoop + SQLite + Outbox
```

в один этап, а затем сразу переходил к:

```text
Entry + TP + SL + recovery
```

Новый Roadmap разделяет доказательства:

```text
EngineLoop
отдельно

Quantower contracts
отдельно

Persistence
отдельно

Outbox
отдельно

Entry
отдельно

Exit
отдельно

TP
отдельно

SL
отдельно

Replacement
отдельно

Recovery
отдельно

DCA
только после этого
```

Это увеличивает количество этапов, но каждый этап становится значительно меньше. В результате расход токенов и объем `change.md` не должны увеличиться. Наоборот, агенту больше не придется одновременно проектировать пять взаимозависимых механизмов.

## Что делать сейчас

Первый новый change после создания ветки от `93b21db`:

```text
implement-engine-loop-foundation
```

Но перед ним я бы выборочно перенес нужные project rules, skills и проверенные Value Objects. После EngineLoop следующим change должен стать:

```text
verify-quantower-single-order-contracts
```

До завершения этого contract change не следует переносить старый `QuantowerAdapter`, `SqliteTradingStateStore`, `DcaGridLiveTestStrategy` или `TradeCycle.Handle(OrderExecuted)`.

Этот Roadmap отражает главную коррекцию курса:

> DCA-сетка больше не является первым доказательством готовности. Первым доказательством является способность надежно провести один ордер через partial fills, cancel, restart и reconciliation без потери фактического состояния.
