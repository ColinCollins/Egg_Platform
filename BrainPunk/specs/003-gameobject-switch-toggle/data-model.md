# Data Model: GameObject Switch Toggle

## Entity: Trigger

**Description**: The optional user-interactable control that requests a toggle.

**Fields**:

- `TriggerBtn`: assigned button reference; optional for externally driven state changes.

**Validation Rules**:

- Missing trigger must not crash initialization, enable, disable, or direct state application.
- If present, one click must request one toggle while the button's own interactability allows it.

## Entity: Visual Part

**Description**: One of the two GameObject roots controlled by the toggle.

**Fields**:

- `OnPart`: visible when the toggle state is On.
- `OffPart`: visible when the toggle state is Off.

**Validation Rules**:

- Both assigned: exactly one part is active after state application.
- One missing: the assigned part still receives the correct visibility update.
- Both initially active or both initially inactive: initialization resolves to one valid state.

## Entity: Toggle State

**Description**: The current boolean state reported to callers and reflected by visual parts.

**Fields**:

- `IsOn`: true when On state is active; false when Off state is active.

**State Transitions**:

```text
Off --Trigger/Toggle--> On
On  --Trigger/Toggle--> Off
Any --SetState(true)--> On
Any --SetState(false)-> Off
```

**Validation Rules**:

- Reapplying the current state does not emit a duplicate state-change notification.
- State application must be null-safe.

## Entity: State Change Callback

**Description**: A local listener notification sent after an effective On/Off state change.

**Fields**:

- `isOn`: boolean payload representing the new state.

**Validation Rules**:

- Emit once per effective state change.
- Do not emit when a direct set requests the already active state.
- Do not imply analytics, save, config, purchase, or resource-loading integration.
