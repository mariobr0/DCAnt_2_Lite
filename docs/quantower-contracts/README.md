# Quantower Contracts Repository

This directory contains the results of manual testing of Quantower API contracts.

## Index

| Date | Scenario | RunId | Result | Evidence Quality | Covered Contracts | Reports |
|---|---|---|---|---|---|---|
| 2026-07-24 | PlaceAndObserve | `run_260724_100206_3cb1` | Passed | High | QT-PLACE-001, QT-PLACE-002, QT-ID-001, QT-ID-002 | [Report](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md) / [Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log) |
| 2026-07-24 | ObserveOnly | `run_260724_110811_4fd6` | Passed | High | (Diagnostic confirmation) | [Report](runs/2026-07-24_ObserveOnly_run_260724_110811_4fd6.md) / [Log](evidence/2026-07-24_ObserveOnly_run_260724_110811_4fd6.log) |
| 2026-07-24 | CancelAndObserve | `run_260724_111913_1ea6` | Failed | Low | QT-CANCEL-001 | [Report](runs/2026-07-24_cancel-and-observe_request-error_run_260724_111913_1ea6.md) / [Log](evidence/2026-07-24_cancel-and-observe_request-error_run_260724_111913_1ea6.log) |
| 2026-07-24 | CancelAndObserve | `run_260724_112811_3cff` | Passed | High | QT-CANCEL-001 | [Report](runs/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.md) / [Log](evidence/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.log) |
| 2026-07-24 | CancelAndObserve | `run_260724_114025_a54c` | Passed | High | (Defensive rejection) | [Report](runs/2026-07-24_cancel-and-observe_defensive_empty-marker_run_260724_114025_a54c.md) / [Log](evidence/2026-07-24_cancel-and-observe_defensive_empty-marker_run_260724_114025_a54c.log) |
| 2026-07-24 | PlaceAndObserve | `run_260724_114237_6132` | Passed | High | (Defensive rejection) | [Report](runs/2026-07-24_place-and-observe_defensive_active-order-exists_run_260724_114237_6132.md) / [Log](evidence/2026-07-24_place-and-observe_defensive_active-order-exists_run_260724_114237_6132.log) |
| 2026-07-24 | CancelAndObserve | `run_260724_114512_b21d` | Passed | High | (Defensive retry) | [Report](runs/2026-07-24_cancel-and-observe_defensive_retry_run_260724_114512_b21d.md) / [Log](evidence/2026-07-24_cancel-and-observe_defensive_retry_run_260724_114512_b21d.log) |
| 2026-07-24 | PlaceAndObserve | `run_260724_114845_c89e` | Passed | High | (Defensive rate-limit) | [Report](runs/2026-07-24_place-and-observe_defensive_rate-limit_run_260724_114845_c89e.md) / [Log](evidence/2026-07-24_place-and-observe_defensive_rate-limit_run_260724_114845_c89e.log) |
| 2026-07-24 | PlaceForFullFill |
un_260724_141120_58fa | Passed | High | QT-PLACE-001, QT-PLACE-002, QT-ID-001, QT-ID-002 | [Report](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md) / [Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log) |
| 2026-07-24 | ObserveFill | `run_260724_142612_a3c6` | Passed | High | (Cleanup / Bugfix verify) | [Report](runs/2026-07-24_ObserveFill_cleanup_run_260724_142612_a3c6.md) / [Log](evidence/2026-07-24_ObserveFill_cleanup_run_260724_142612_a3c6.log) |
| 2026-07-24 | PlaceForPartialFill | `run_260724_143338_fec5` | Passed | High | QT-FILL-001, QT-FILL-002, QT-POS-002 | [Report](runs/2026-07-24_PlaceForPartialFill_run_260724_143338_fec5.md) / [Log](evidence/2026-07-24_PlaceForPartialFill_run_260724_143338_fec5.log) |
| 2026-07-24 | ObserveFill | `run_260724_144131_d9e9` | Passed | High | (Cleanup after Partial Fill) | [Report](runs/2026-07-24_ObserveFill_partial-cleanup_run_260724_144131_d9e9.md) / [Log](evidence/2026-07-24_ObserveFill_partial-cleanup_run_260724_144131_d9e9.log) |
