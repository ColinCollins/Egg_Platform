# Feature Specification: GameObject Switch Toggle

**Feature Branch**: `[003-gameobject-switch-toggle]`

**Created**: 2026-06-12

**Status**: Draft

**Input**: User description: "完善一下 GameObjectSwitchToggle"

## Constitution Alignment *(mandatory)*

**Gameplay/Level Impact**: This feature affects a local UI/scene helper component that switches between two GameObject visual states. It does not intentionally change levels, save/unlock data, config tables, analytics events, purchase rewards, ad rewards, restore purchase behavior, resource loading paths, or platform SDK flows.

**Target Platforms**: Android and iOS mobile landscape play are in scope when this component is used on runtime UI. The toggle must work with existing touch/click input behavior.

**Regression Requirement**: Before acceptance, verify the existing screen or prefab using this component still opens, toggles, closes, and reopens without duplicate click responses or visible overlap between the two controlled parts.

**Original Behavior Consistency**: Existing player-visible behavior outside the controlled On/Off visual state must remain unchanged. Existing shop purchase, rewarded-ad, restore, sold-out, save, config, analytics, and resource-loading behavior must not be modified by this helper.

**Risk Avoidance**: Keep the behavior scoped to the assigned trigger button and two assigned visual parts. Roll back by removing or disabling the component if the helper hides the wrong part, blocks access to existing UI, or causes duplicate click handling. Runtime visual validation in Unity remains required after implementation.

**Performance Requirement**: Each toggle should complete immediately for the player and must not instantiate or load assets during normal clicks. The feature should preserve the 60 fps mobile target on representative Android/iOS landscape devices.

**Stability vs Reuse Decision**: Prefer a small reusable helper only for direct two-state GameObject switching. Do not replace established module-specific switch controllers unless regression evidence covers the affected prefab or screen.

## Local Evidence

- `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs` currently declares `TriggerBtn`, `OnPart`, and `OffPart`, and its summary states the intended behavior: switch On/Off state, show one part at a time, and provide an `IsOnCallback`.
- `Assets/Game/Scripts/Common/Buttons/CustomButton.cs` exposes an `OnClick` event and blocks clicks when the button is inactive, non-interactable, delayed, or click-blocked.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelSwitchGroup.cs` already handles a ShopPanel-specific two-tab group. This specification is for the generic two-state helper and must not broaden scope into ShopPanel purchase or skin-content logic.
- `DevelopMD/ShopPanel_ShopItem_Design.md` documents that ShopPanel owns data assembly, purchases, ads, restore purchase, button locking, and reward animation; this helper must not take over those responsibilities.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Toggle To On State (Priority: P1)

As a player interacting with a two-state UI control, I want tapping the trigger to show the On visual state and hide the Off visual state so the current selection is clear.

**Why this priority**: The helper's primary value is keeping two visual states mutually exclusive and understandable.

**Independent Test**: Attach the component to a runtime object with a trigger and two visual parts, tap the trigger from the Off state, and confirm only the On part is visible afterward.

**Acceptance Scenarios**:

1. **Given** the toggle is currently Off, **When** the player taps the trigger, **Then** the On part becomes visible and the Off part becomes hidden.
2. **Given** the toggle is currently On, **When** the player taps the trigger again, **Then** the component changes to Off unless configured or driven externally to remain On.

---

### User Story 2 - Toggle To Off State (Priority: P1)

As a player interacting with the same control, I want tapping again to show the Off visual state and hide the On visual state so the state can be reversed.

**Why this priority**: A toggle is incomplete if it can only enter one state and cannot return.

**Independent Test**: Start from the On state, tap the trigger, and confirm only the Off part is visible afterward.

**Acceptance Scenarios**:

1. **Given** the toggle is currently On, **When** the player taps the trigger, **Then** the Off part becomes visible and the On part becomes hidden.
2. **Given** the toggle receives rapid repeated taps, **When** input processing settles, **Then** exactly one of the On or Off parts is visible.

---

### User Story 3 - Notify Other Local Logic Of State Changes (Priority: P2)

As a feature owner wiring a prefab, I want the component to report whether it is On so nearby local logic can respond without reading child active states directly.

**Why this priority**: The current component comment explicitly requests an `IsOnCallback`, and callback notification prevents other code from duplicating visual-state checks.

**Independent Test**: Register a local callback, tap the trigger from Off to On and back to Off, and confirm the callback receives `true` then `false` once per effective state change.

**Acceptance Scenarios**:

1. **Given** a listener is registered, **When** the toggle changes from Off to On, **Then** the listener is notified with `true`.
2. **Given** a listener is registered, **When** the toggle changes from On to Off, **Then** the listener is notified with `false`.
3. **Given** the requested state is already active, **When** the state is applied again without an actual change, **Then** duplicate callback notification is avoided.

### Edge Cases

- If both visual parts are initially active, the component must settle into one valid state during initialization or enable.
- If both visual parts are initially inactive, the component must still choose a valid default state.
- If one visual part reference is missing, the component must avoid runtime crashes and still update the assigned part.
- If the trigger button reference is missing, the component must avoid runtime crashes and leave externally driven state changes possible.
- Repeated enable/disable cycles must not register duplicate click handlers.
- Disable or destroy must clean up click handling so reopened screens do not toggle multiple times per tap.
- Runtime behavior must not depend on editor-only initialization, runtime singleton bootstraps, save data, DB data paths, remote config, or resource loading.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The component MUST maintain a boolean On/Off state.
- **FR-002**: The component MUST show no more than one of the configured On part and Off part as active at a time.
- **FR-003**: When the state is On, the configured On part MUST be visible and the configured Off part MUST be hidden when those references exist.
- **FR-004**: When the state is Off, the configured Off part MUST be visible and the configured On part MUST be hidden when those references exist.
- **FR-005**: Tapping the configured trigger MUST toggle between On and Off states when the trigger is available and interactable.
- **FR-006**: The component MUST expose a way for local callers to set the state directly without simulating a user click.
- **FR-007**: The component MUST expose a way for local callers to read whether the current state is On.
- **FR-008**: The component MUST notify local listeners with the current On value after an effective state change.
- **FR-009**: The component MUST avoid duplicate trigger subscriptions across repeated enable/disable, open/close, or destroy cycles.
- **FR-010**: The component MUST avoid runtime crashes when optional references are missing.
- **FR-011**: The component MUST not instantiate objects, load resources, access save data, access DB/config data, or initialize runtime managers as part of normal toggling.
- **FR-REGRESSION**: The feature MUST preserve existing module-specific behavior outside the two assigned visual parts unless an intentional change is documented in this specification.
- **FR-CONSISTENCY**: Existing ShopPanel purchase, rewarded-ad, restore purchase, reward animation, sold-out, save, config, analytics, and resource-loading behavior MUST remain unchanged when this helper is used near ShopPanel UI.
- **FR-MOBILE**: The feature MUST remain usable on Android and iOS landscape mobile devices with touch input.

### Key Entities *(include if feature involves data)*

- **Trigger**: The user-interactable control that requests a state toggle.
- **On Part**: The GameObject shown when the toggle state is On.
- **Off Part**: The GameObject shown when the toggle state is Off.
- **On State**: The boolean state value reported to local callers and listeners.
- **State Change Callback**: The notification sent after the On state effectively changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested trigger taps, exactly one configured visual state is active after the tap when both parts are assigned.
- **SC-002**: Ten consecutive trigger taps alternate the reported On state exactly ten times without duplicate callback invocations.
- **SC-003**: Five consecutive enable/disable or open/close cycles leave the trigger producing one state change per tap.
- **SC-004**: Missing trigger, missing On part, or missing Off part scenarios do not crash during initialization, enable, disable, or direct state application.
- **SC-005**: Normal toggle interaction completes without visible delay on representative Android/iOS landscape devices.

## Assumptions

- `GameObjectSwitchToggle` is intended as a generic two-state helper, not a replacement for the existing `ShopPanelSwitchGroup`.
- The default initial state should be derived from existing active visual parts when possible; if no clear state exists, defaulting to Off is acceptable because it is the conservative inactive state for a standalone toggle.
- The callback is intended for local runtime logic and does not imply analytics, save, config, purchase, or resource-loading integration.
- Editor-time preview and custom inspector behavior are outside this specification unless requested separately.

## Missing Evidence

- No local prefab reference to `GameObjectSwitchToggle` was verified during specification generation.
- No Unity PlayMode evidence exists yet for the intended initial default state, callback wiring style, or final visual behavior.
- `.specify/templates/spec-template.md` was not present in this workspace, so the specification follows the existing local `specs/002-shop-panel-switch-group/spec.md` structure.
