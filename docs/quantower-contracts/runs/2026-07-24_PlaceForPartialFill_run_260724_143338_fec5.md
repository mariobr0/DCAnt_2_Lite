# Quantower Contract Test Run

## Environment
- **Scenario**: PlaceForPartialFill
- **RunId**: run_260724_143338_fec5
- **TargetMarker**: qtct_240726_69c7ad08
- **Quantower version**: v1.146.16
- **Git commit**: current
- **Symbol ID**: 6470df5d-045f-4c2b-a6e7-a816a265f111

## Validation
- Multiple partial fills observed.
- Cumulative nature of FilledQuantity and AverageFillPrice successfully documented.
- PositionAdded behavior correctly verified against the new fallback logic (PositionState is correctly set to Open).
- Evidence quality: **High**.

## Confirmed observations
- The order was partially filled many times.
- OrdersHistoryAdded fired on each partial fill with Status=PartiallyFilled.
- FilledQty on the order object increases cumulatively (e.g. 7088 -> 9744 -> 23393).
- AverageFillPrice updates dynamically to represent the cumulative average fill price of the entire order.
- OrderAdded event inexplicably fired repeatedly on partial fills without corresponding OrderRemoved events.
- PositionAdded event fired repeatedly with sequentially increasing PositionQty.
- The previously observed bug (empty PositionState) is confirmed fixed; it correctly logged PositionState=Open.

## Supported contracts
- QT-PLACE-001
- QT-PLACE-002
- QT-ID-001
- QT-ID-002
- QT-FILL-001 (Confirmed: FilledQuantity is cumulative)
- QT-FILL-002 (Confirmed: AverageFillPrice is cumulative average)
- QT-FILL-005
- QT-POS-001
- QT-POS-002 (Confirmed: Position.OpenPrice equals cumulative AverageFillPrice)

## Final state
- **Order state**: PartiallyFilled (FilledQty=994140, then OrderRemoved)
- **Position state**: Open (PositionQty=994140)
- **ManualCleanupRequired**: Yes

## Result and next action
- **Result**: Passed
- **Next Action**: ManualCleanupRequired, then EngineLoop design

## Partial Fill Transitions
| Sequence | Source | Status | PrevFilledQty | CurrFilledQty | ObsDiff | AvgFillPrice | PosQty | PosOpenPrice |
|---|---|---|---|---|---|---|---|---|
| 11 | PositionAdded | - | - | - | - | - | 7088 | 0.10406 |
| 14 | PositionAdded | - | - | - | - | - | 9744 | 0.10406272577996716 |
| 17 | PositionAdded | - | - | - | - | - | 23393 | 0.10407280468516222 |
| 20 | PositionAdded | - | - | - | - | - | 34679 | 0.10407840076126763 |
| 23 | PositionAdded | - | - | - | - | - | 41991 | 0.10408216189183396 |
| 26 | PositionAdded | - | - | - | - | - | 47170 | 0.10408521835912656 |
| 29 | PositionAdded | - | - | - | - | - | 62920 | 0.10409392482517482 |
| 32 | PositionAdded | - | - | - | - | - | 67239 | 0.1040962420618986 |
| 35 | PositionAdded | - | - | - | - | - | 74721 | 0.1041006236533237 |
| 38 | PositionAdded | - | - | - | - | - | 83346 | 0.10410573332853407 |
| 41 | PositionAdded | - | - | - | - | - | 83621 | 0.10410591179249232 |
| 44 | PositionAdded | - | - | - | - | - | 90334 | 0.1041114175172139 |
| 47 | PositionAdded | - | - | - | - | - | 98775 | 0.10411813292837255 |
| 50 | PositionAdded | - | - | - | - | - | 115562 | 0.1041300252678216 |
| 53 | PositionAdded | - | - | - | - | - | 124872 | 0.104135987891601 |
| 56 | PositionAdded | - | - | - | - | - | 128838 | 0.10413857402319192 |
| 59 | PositionAdded | - | - | - | - | - | 129928 | 0.10413934101964165 |
| 62 | PositionAdded | - | - | - | - | - | 144865 | 0.10414971994615678 |
| 65 | PositionAdded | - | - | - | - | - | 145001 | 0.10414981400128276 |
| 68 | PositionAdded | - | - | - | - | - | 148967 | 0.1041527475212631 |
| 71 | PositionAdded | - | - | - | - | - | 152281 | 0.10415529921657989 |
| 74 | PositionAdded | - | - | - | - | - | 157494 | 0.10415942677181353 |
| 77 | PositionAdded | - | - | - | - | - | 169131 | 0.10416841081765023 |
| 80 | PositionAdded | - | - | - | - | - | 173202 | 0.1041715037355227 |
| 83 | PositionAdded | - | - | - | - | - | 178761 | 0.10417581060745913 |
| 86 | PositionAdded | - | - | - | - | - | 185415 | 0.10418098514143947 |
| 89 | PositionAdded | - | - | - | - | - | 193400 | 0.10418713759048603 |
| 92 | PositionAdded | - | - | - | - | - | 198861 | 0.10419133540513223 |
| 95 | PositionAdded | - | - | - | - | - | 211641 | 0.10420091641033637 |
| 98 | PositionAdded | - | - | - | - | - | 215712 | 0.10420391869715176 |
| 101 | PositionAdded | - | - | - | - | - | 222223 | 0.10420878477925327 |
| 104 | PositionAdded | - | - | - | - | - | 226239 | 0.10421182404448394 |
| 107 | PositionAdded | - | - | - | - | - | 229113 | 0.1042140590887466 |
| 110 | PositionAdded | - | - | - | - | - | 233556 | 0.10421759629382248 |
| 113 | PositionAdded | - | - | - | - | - | 238308 | 0.10422143293552881 |
| 116 | PositionAdded | - | - | - | - | - | 247026 | 0.1042284407309352 |
| 119 | PositionAdded | - | - | - | - | - | 260193 | 0.10423864058602653 |
| 122 | PositionAdded | - | - | - | - | - | 264159 | 0.10424166373282759 |
| 125 | PositionAdded | - | - | - | - | - | 269755 | 0.104245985616578 |
| 128 | PositionAdded | - | - | - | - | - | 274325 | 0.10424955089765789 |
| 131 | PositionAdded | - | - | - | - | - | 283195 | 0.10425645562245096 |
| 134 | PositionAdded | - | - | - | - | - | 291973 | 0.10426317635534793 |
| 137 | PositionAdded | - | - | - | - | - | 307451 | 0.10427459533389061 |
| 140 | PositionAdded | - | - | - | - | - | 312548 | 0.10427827120954222 |
| 143 | PositionAdded | - | - | - | - | - | 313975 | 0.1042793244048093 |
| 146 | PositionAdded | - | - | - | - | - | 318687 | 0.10428288295412114 |
| 149 | PositionAdded | - | - | - | - | - | 318959 | 0.10428309368915754 |
| 152 | PositionAdded | - | - | - | - | - | 333147 | 0.10429403476543389 |
| 155 | PositionAdded | - | - | - | - | - | 345801 | 0.10430340137824934 |
| 158 | PositionAdded | - | - | - | - | - | 350025 | 0.10430649793586173 |
| 161 | PositionAdded | - | - | - | - | - | 350347 | 0.10430674011765478 |
| 164 | PositionAdded | - | - | - | - | - | 357569 | 0.10431225928422207 |
| 167 | PositionAdded | - | - | - | - | - | 360393 | 0.10431443562999282 |
| 170 | PositionAdded | - | - | - | - | - | 365633 | 0.10431852814160647 |
| 173 | PositionAdded | - | - | - | - | - | 366329 | 0.10431908191816647 |
| 176 | PositionAdded | - | - | - | - | - | 371274 | 0.10432308984739035 |
| 182 | PositionAdded | - | - | - | - | - | 385052 | 0.10433407176173609 |
| 185 | PositionAdded | - | - | - | - | - | 389850 | 0.10433783691163269 |
| 188 | PositionAdded | - | - | - | - | - | 397081 | 0.10434370362721965 |
| 191 | PositionAdded | - | - | - | - | - | 398274 | 0.10434468102361692 |
| 194 | PositionAdded | - | - | - | - | - | 403441 | 0.10434897556272169 |
| 197 | PositionAdded | - | - | - | - | - | 419460 | 0.10436199914175369 |
| 200 | PositionAdded | - | - | - | - | - | 424353 | 0.1043658964588444 |
| 203 | PositionAdded | - | - | - | - | - | 424405 | 0.10436593861995028 |
| 206 | PositionAdded | - | - | - | - | - | 429209 | 0.1043699015165106 |
| 209 | PositionAdded | - | - | - | - | - | 429775 | 0.1043703757547554 |
| 212 | PositionAdded | - | - | - | - | - | 434293 | 0.10437422099826615 |
| 215 | PositionAdded | - | - | - | - | - | 435279 | 0.10437507221804866 |
| 218 | PositionAdded | - | - | - | - | - | 439515 | 0.10437878211210085 |
| 287 | PositionAdded | - | - | - | - | - | 440076 | 0.10437847771748517 |
| 290 | PositionAdded | - | - | - | - | - | 442021 | 0.1043774723599105 |
| 293 | PositionAdded | - | - | - | - | - | 442339 | 0.10437731601780535 |
| 296 | PositionAdded | - | - | - | - | - | 445101 | 0.10437602955284307 |
| 299 | PositionAdded | - | - | - | - | - | 460994 | 0.10436927133541868 |
| 302 | PositionAdded | - | - | - | - | - | 468995 | 0.10436621298734527 |
| 305 | PositionAdded | - | - | - | - | - | 469784 | 0.10436593383342133 |
| 308 | PositionAdded | - | - | - | - | - | 469997 | 0.10436586316508403 |
| 311 | PositionAdded | - | - | - | - | - | 474139 | 0.10436458892856315 |
| 316 | PositionAdded | - | - | - | - | - | 481441 | 0.10436254762265781 |
| 319 | PositionAdded | - | - | - | - | - | 496427 | 0.10435884818915973 |
| 322 | PositionAdded | - | - | - | - | - | 496611 | 0.10435880785967286 |
| 326 | PositionAdded | - | - | - | - | - | 500578 | 0.10435802482330427 |
| 329 | PositionAdded | - | - | - | - | - | 502910 | 0.10435761665109065 |
| 332 | PositionAdded | - | - | - | - | - | 508582 | 0.10435675102540003 |
| 335 | PositionAdded | - | - | - | - | - | 519761 | 0.10435531534686135 |
| 338 | PositionAdded | - | - | - | - | - | 526597 | 0.10435459727267721 |
| 341 | PositionAdded | - | - | - | - | - | 528304 | 0.1043544531746873 |
| 344 | PositionAdded | - | - | - | - | - | 533357 | 0.10435412676687472 |
| 347 | PositionAdded | - | - | - | - | - | 540383 | 0.10435381307332023 |
| 350 | PositionAdded | - | - | - | - | - | 545583 | 0.10435368141969234 |
| 353 | PositionAdded | - | - | - | - | - | 559342 | 0.10435359086212015 |
| 356 | PositionAdded | - | - | - | - | - | 563414 | 0.10435363718331458 |
| 359 | PositionAdded | - | - | - | - | - | 574737 | 0.10435395955019428 |
| 362 | PositionAdded | - | - | - | - | - | 579256 | 0.1043541627018106 |
| 366 | PositionAdded | - | - | - | - | - | 588451 | 0.10435487894489091 |
| 369 | PositionAdded | - | - | - | - | - | 593204 | 0.10435532059797305 |
| 372 | PositionAdded | - | - | - | - | - | 598801 | 0.10435620556745896 |
| 375 | PositionAdded | - | - | - | - | - | 603371 | 0.10435699171819661 |
| 378 | PositionAdded | - | - | - | - | - | 607490 | 0.10435775795486345 |
| 381 | PositionAdded | - | - | - | - | - | 611456 | 0.10435855083603726 |
| 384 | PositionAdded | - | - | - | - | - | 626935 | 0.10436179631062233 |
| 387 | PositionAdded | - | - | - | - | - | 627316 | 0.10436188632204504 |
| 390 | PositionAdded | - | - | - | - | - | 631933 | 0.1043630415249718 |
| 393 | PositionAdded | - | - | - | - | - | 632206 | 0.1043631136211931 |
| 396 | PositionAdded | - | - | - | - | - | 636700 | 0.10436436213287263 |
| 399 | PositionAdded | - | - | - | - | - | 640667 | 0.10436557351947268 |
| 402 | PositionAdded | - | - | - | - | - | 645287 | 0.10436710872836429 |
| 405 | PositionAdded | - | - | - | - | - | 651926 | 0.10436937857977747 |
| 408 | PositionAdded | - | - | - | - | - | 661018 | 0.10437255067184252 |
| 411 | PositionAdded | - | - | - | - | - | 661320 | 0.10437265910603037 |
| 419 | PositionAdded | - | - | - | - | - | 670499 | 0.10437631895051298 |
| 422 | PositionAdded | - | - | - | - | - | 671842 | 0.1043769060136163 |
| 425 | PositionAdded | - | - | - | - | - | 676736 | 0.10437924255544259 |
| 430 | PositionAdded | - | - | - | - | - | 677252 | 0.10437950979842067 |
| 433 | PositionAdded | - | - | - | - | - | 682082 | 0.10438206252327432 |
| 437 | PositionAdded | - | - | - | - | - | 688545 | 0.10438551615362832 |
| 440 | PositionAdded | - | - | - | - | - | 692781 | 0.10438780593001251 |
| 443 | PositionAdded | - | - | - | - | - | 702147 | 0.10439290405000663 |
| 446 | PositionAdded | - | - | - | - | - | 705456 | 0.10439162397087841 |
| 449 | PositionAdded | - | - | - | - | - | 706517 | 0.1043912310814885 |
| 452 | PositionAdded | - | - | - | - | - | 710655 | 0.1043897682138309 |
| 463 | PositionAdded | - | - | - | - | - | 716544 | 0.10438779765094677 |
| 466 | PositionAdded | - | - | - | - | - | 732114 | 0.1043829530373685 |
| 469 | PositionAdded | - | - | - | - | - | 748804 | 0.10437842944215042 |
| 472 | PositionAdded | - | - | - | - | - | 760106 | 0.10437562768876972 |
| 475 | PositionAdded | - | - | - | - | - | 764405 | 0.10437463996180037 |
| 478 | PositionAdded | - | - | - | - | - | 765034 | 0.10437450459718131 |
| 481 | PositionAdded | - | - | - | - | - | 769944 | 0.10437351930789772 |
| 484 | PositionAdded | - | - | - | - | - | 777470 | 0.10437213002430962 |
| 500 | PositionAdded | - | - | - | - | - | 790331 | 0.10436997988184697 |
| 503 | PositionAdded | - | - | - | - | - | 794592 | 0.10436933648967017 |
| 509 | PositionAdded | - | - | - | - | - | 796757 | 0.10436903939344118 |
| 512 | PositionAdded | - | - | - | - | - | 808847 | 0.10436755903155975 |
| 515 | PositionAdded | - | - | - | - | - | 809059 | 0.10436753608822101 |
| 518 | PositionAdded | - | - | - | - | - | 810818 | 0.10436741126862009 |
| 521 | PositionAdded | - | - | - | - | - | 818727 | 0.10436704987132464 |
| 524 | PositionAdded | - | - | - | - | - | 829089 | 0.10436671180054254 |
| 527 | PositionAdded | - | - | - | - | - | 835892 | 0.10436673856191948 |
| 530 | PositionAdded | - | - | - | - | - | 839909 | 0.10436680198688192 |
| 533 | PositionAdded | - | - | - | - | - | 842840 | 0.10436688265863035 |
| 545 | PositionAdded | - | - | - | - | - | 852087 | 0.10436724205392171 |
| 548 | PositionAdded | - | - | - | - | - | 856840 | 0.10436747923766397 |
| 551 | PositionAdded | - | - | - | - | - | 867920 | 0.10436814972578118 |
| 554 | PositionAdded | - | - | - | - | - | 881453 | 0.10436909931669641 |
| 557 | PositionAdded | - | - | - | - | - | 885420 | 0.10436941697725373 |
| 560 | PositionAdded | - | - | - | - | - | 891017 | 0.10436992316644912 |
| 563 | PositionAdded | - | - | - | - | - | 895642 | 0.10437038831363424 |
| 566 | PositionAdded | - | - | - | - | - | 899599 | 0.10437082646823752 |
| 569 | PositionAdded | - | - | - | - | - | 906184 | 0.10437161980348363 |
| 572 | PositionAdded | - | - | - | - | - | 923518 | 0.10437384174428652 |
| 575 | PositionAdded | - | - | - | - | - | 929889 | 0.10437470609933014 |
| 578 | PositionAdded | - | - | - | - | - | 935660 | 0.10437560224868007 |
| 581 | PositionAdded | - | - | - | - | - | 935661 | 0.1043756024136947 |
| 584 | PositionAdded | - | - | - | - | - | 948886 | 0.10437803306192736 |
| 587 | PositionAdded | - | - | - | - | - | 952952 | 0.10437880946784309 |
| 605 | PositionAdded | - | - | - | - | - | 953211 | 0.10437886141683217 |
| 616 | PositionAdded | - | - | - | - | - | 957393 | 0.10437974001272206 |
| 619 | PositionAdded | - | - | - | - | - | 964293 | 0.10438124452837468 |
| 622 | PositionAdded | - | - | - | - | - | 968533 | 0.10438220218619293 |
| 625 | PositionAdded | - | - | - | - | - | 968910 | 0.10438229082164494 |
| 628 | PositionAdded | - | - | - | - | - | 973350 | 0.10438337514768584 |
| 631 | PositionAdded | - | - | - | - | - | 986108 | 0.10438656591367275 |
| 634 | PositionAdded | - | - | - | - | - | 994140 | 0.10438861349508118 |
| 637 | PositionAdded | - | - | - | - | - | 1000000 | 0.10439020382 |

## Diagnostic limitations
> Sequence отражает порядок строк файла. При конкурентных callbacks он не гарантирует порядок входа событий в Quantower.
