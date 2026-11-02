# Tasks: GameObject Switch Toggle

**Input**: Design artifacts from `specs/003-gameobject-switch-toggle/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/component-contract.md`, `quickstart.md`

## Phase 1: Setup

- [X] T001 Review feature scope and validation requirements in `specs/003-gameobject-switch-toggle/spec.md`, `specs/003-gameobject-switch-toggle/plan.md`, and `specs/003-gameobject-switch-toggle/quickstart.md`
- [X] T002 Review local button and switch patterns in `Assets/Game/Scripts/Common/Buttons/CustomButton.cs`, `Assets/Game/UI/ShopPanel/Scripts/ShopPanelSwitchGroup.cs`, and `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`

## Phase 2: Foundational

- [X] T003 Define the runtime state API and listener surface in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs` while preserving existing serialized field names `TriggerBtn`, `OnPart`, and `OffPart`
- [X] T004 Implement null-safe visual application helpers in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs` so assigned parts only call `SetActive` when their active state must change
- [X] T005 Implement initial state resolution in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs` using assigned part active states first and Off fallback when no clear state exists

## Phase 3: User Story 1 - Toggle To On State (P1)

**Goal**: A player tap from Off state shows On visual state and hides Off visual state.

**Independent Test**: Start with Off visible and On hidden, tap the trigger once, and confirm only On is visible.

- [X] T006 [US1] Implement `CustomButton.OnClick` binding in `OnEnable` with duplicate-subscription protection in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [X] T007 [US1] Implement trigger handling that changes Off state to On state in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [X] T008 [US1] Implement `OnDisable` and `OnDestroy` trigger unbinding in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [ ] T009 [US1] Verify the Off-to-On flow manually in Unity PlayMode using the checklist in `specs/003-gameobject-switch-toggle/quickstart.md`

## Phase 4: User Story 2 - Toggle To Off State (P1)

**Goal**: A second player tap from On state shows Off visual state and hides On visual state.

**Independent Test**: Start with On visible, tap the trigger once, and confirm only Off is visible; repeat rapid taps and confirm one visible state.

- [X] T010 [US2] Implement full toggle behavior from On to Off and Off to On in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [X] T011 [US2] Add direct state application and direct toggle methods for local callers in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [ ] T012 [US2] Verify ten repeated trigger taps leave exactly one visual part active after each tap using `specs/003-gameobject-switch-toggle/quickstart.md`
- [ ] T013 [US2] Verify five disable/enable or close/reopen cycles produce one state change per tap using `specs/003-gameobject-switch-toggle/quickstart.md`
- [ ] T014 [US2] Verify missing trigger, missing On part, and missing Off part cases do not crash using `specs/003-gameobject-switch-toggle/quickstart.md`

## Phase 5: User Story 3 - Notify Other Local Logic Of State Changes (P2)

**Goal**: Local logic can read the current On state and receive one notification after each effective state change.

**Independent Test**: Register a local callback, toggle Off to On and On to Off, and confirm callback receives `true` then `false` once per effective change.

- [X] T015 [US3] Implement a read-only current On-state accessor in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [X] T016 [US3] Implement `IsOnCallback` notification after effective state changes in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [X] T017 [US3] Guard callback dispatch against no-op state applications in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [ ] T018 [US3] Verify callback dispatch once per effective state change using `specs/003-gameobject-switch-toggle/quickstart.md`

## Phase 6: Polish & Cross-Cutting

- [X] T019 Run static compile validation with `dotnet build Assembly-CSharp.csproj --no-restore` from `D:\GitStore\BrinPunk\BrainPunk`
- [X] T020 Verify no DB, save, RemoteConfig, runtime manager, resource loading, or instantiation access was introduced in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`
- [ ] T021 If the helper is attached near ShopPanel UI, verify existing shop goods, purchase, ad, restore, close, and sold-out behavior remain unchanged using `specs/003-gameobject-switch-toggle/quickstart.md`
- [X] T022 Record implementation facts, compile result, PlayMode result, mobile smoke result, and any unverified gaps in `DevelopMD/已有功能设计与使用记录.md`
- [ ] T023 If `GameObjectSwitchToggle` is used in a production UI, perform Android/iOS landscape touch smoke and record the result in `DevelopMD/已有功能设计与使用记录.md`

## Dependencies

### Phase Dependencies

- Phase 1 must complete before Phase 2.
- Phase 2 must complete before any user story implementation.
- US1 is the MVP and should complete before US2 because it establishes trigger binding and one-way state application.
- US2 depends on US1 trigger binding and state application.
- US3 depends on US1/US2 state application so callback notifications can be tied to effective state changes.
- Polish tasks run after the relevant user stories are implemented.

### User Story Dependencies

- **US1**: No dependency after foundational tasks.
- **US2**: Depends on US1.
- **US3**: Depends on US1 and US2.

## Parallel Execution Examples

- After T003 is complete, T004 and T005 can be implemented by separate agents only if they coordinate changes in `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`.
- T012, T013, and T014 can be validated independently after T010 and T011 are complete.
- T021 and T023 can be performed independently after implementation and compile validation if the helper is wired into production UI.

## Implementation Strategy

### MVP First

Complete Phase 1, Phase 2, and US1. This produces the smallest useful increment: the helper can enter On state through the assigned trigger and keep visuals mutually exclusive.

### Incremental Delivery

1. Deliver US1 and verify Off-to-On behavior.
2. Add US2 to complete reversible toggle behavior and missing-reference/lifecycle validation.
3. Add US3 to expose state reading and local callback notification.
4. Finish compile, regression, documentation, and mobile smoke evidence.

## Task Summary

- Total tasks: 23
- Setup: 2
- Foundational: 3
- US1: 4
- US2: 5
- US3: 4
- Polish/Cross-cutting: 5
- Parallel opportunities: validation tasks T012-T014, production UI checks T021/T023, and tightly coordinated helper subtasks T004/T005
