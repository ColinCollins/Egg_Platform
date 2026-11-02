# Component Contract: GameObjectSwitchToggle

## Purpose

`GameObjectSwitchToggle` provides a local two-state GameObject visibility switch. It is intended for prefab wiring and nearby runtime logic that needs to read or react to the On state.

## Prefab Wiring Contract

- `TriggerBtn` may be assigned to allow user click/touch toggling.
- `OnPart` may be assigned as the visual root for the On state.
- `OffPart` may be assigned as the visual root for the Off state.
- Missing references must be tolerated without runtime crashes.

## Runtime State Contract

- The component has a current boolean On state.
- A trigger click toggles the current state when the trigger exists and is interactable.
- Direct callers can set the state to On or Off without simulating a click.
- Direct callers can read whether the state is currently On.
- Visual state is applied consistently:
  - On state: On part active, Off part inactive.
  - Off state: Off part active, On part inactive.

## Notification Contract

- Local listeners can be notified after the On state effectively changes.
- The notification payload is the new On value.
- The notification is not emitted for no-op state applications.

## Non-Goals

- No analytics event dispatch.
- No save/config/DB reads or writes.
- No resource loading or runtime object creation.
- No replacement of `ShopPanelSwitchGroup` or ShopPanel purchase/ad/restore logic.
