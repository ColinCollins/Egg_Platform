---
name: ui-event-listener
description: Adds and manages Bear.EventSystem event subscriptions (EventSubscriber + EventsUtils.ResetEvents) for Unity UI and gameplay scripts in this project. Use when adding new event listeners such as UpdatePropEvent, SwitchGameStateEvent, or other Game.Events types.
---

# UI Event Listener Workflow

## When To Use

- Use this skill whenever you:
  - Add a new event listener to a UI panel (`BaseUIView`) or other MonoBehaviour using `Bear.EventSystem`.
  - Hook into existing events like `UpdatePropEvent`, `SwitchGameStateEvent`, `GameResetEvent`, etc.
  - Need to ensure event subscriptions are correctly created and cleaned up to avoid leaks or duplicate handlers.

This skill follows the patterns used in scripts such as `GamePlayPanel` and `PropBlock`, and is applied to panels like `ChoiceLevelPanel`.

## Standard Pattern Overview

In this project, event listening generally follows this pattern:

- Use `EventSubscriber _subscriber;` as a private field.
- Use `EventsUtils.ResetEvents(ref _subscriber);` before (re)subscribing.
- Subscribe events via `_subscriber.Subscribe<TEvent>(HandlerMethod);`.
- Clean up in `OnClose()` (for `BaseUIView`) or `OnDestroy()` (for other MonoBehaviours) with `EventsUtils.ResetEvents(ref _subscriber);`.

Example (from `GamePlayPanel`):

- Field:
  - `private EventSubscriber _subscriber;`
- Setup (called from `OnOpen()`):
  - `EventsUtils.ResetEvents(ref _subscriber);`
  - `_subscriber.Subscribe<GameResetEvent>(OnGameReset);`
- Cleanup:
  - In `OnClose()`: `EventsUtils.ResetEvents(ref _subscriber);`

Example (from `PropBlock`):

- In `Awake()`:
  - `EventsUtils.ResetEvents(ref _subscriber);`
  - `_subscriber.Subscribe<UpdatePropEvent>(OnPropUpdate);`

## Step-By-Step: Adding A New Event Listener

When you need to add a new event listener (for example, `UpdatePropEvent` in `ChoiceLevelPanel`), follow these steps.

### 1. Add Required Usings

- Ensure the following namespaces are present at the top of the file as needed:
  - `using Bear.EventSystem;`
  - `using Game.Events;` for event types like `UpdatePropEvent`, `SwitchGameStateEvent`, etc.
  - Any event payload enums (for example, `using Config.Game;` for `GameProps`).

Only add the ones that are actually required for the file.

### 2. Add EventSubscriber Field

- If the file does not already define a subscriber, add:

```csharp
private EventSubscriber _subscriber;
```

- Place it near other private fields for consistency.

### 3. Create A Listener Setup Method

- Add a dedicated method to register event listeners. Common names:
  - `AddListener()`
  - `InitEvents()`

Pattern:

```csharp
private void AddListener()
{
    EventsUtils.ResetEvents(ref _subscriber);
    _subscriber.Subscribe<SomeEvent>(OnSomeEvent);
    // Add more subscriptions here if needed
}
```

- Use this method to group all subscriptions for that class.

### 4. Call Setup Method From Lifecycle Hook

- For `BaseUIView`-based panels (like `GamePlayPanel`, `ChoiceLevelPanel`):
  - Call the setup method from `OnOpen()` after base logic, e.g.:

```csharp
public override void OnOpen()
{
    base.OnOpen();
    // existing UI refresh logic...
    AddListener();
}
```

- For non-UI MonoBehaviours (like `PropBlock` or gameplay controllers):
  - Call the setup method from `Awake()` or `Start()` depending on existing patterns.

### 5. Implement The Event Handler

- Event handler signature should take the event type as parameter:

```csharp
private void OnUpdateProp(UpdatePropEvent evt)
{
    // Filter by payload if needed
    // e.g. if (evt.Prop != GameProps.NoAds) return;
    // Then perform the desired UI or gameplay updates
}
```

- Use event payload properties instead of direct fields when available (e.g. `evt.Prop`, `evt.OldCount`, `evt.NewCount`).

### 6. Add Cleanup In OnClose/OnDestroy

- For `BaseUIView`:

```csharp
public override void OnClose()
{
    base.OnClose();
    EventsUtils.ResetEvents(ref _subscriber);
}
```

- For other MonoBehaviours, do the same in `OnDestroy()` if not already handled elsewhere.

This ensures events are unsubscribed when the object is closed or destroyed, preventing duplicate callbacks and memory leaks.

## Example: UpdatePropEvent In ChoiceLevelPanel

When `SimpleBag` dispatches an `UpdatePropEvent`:

- The payload describes which `GameProps` changed and the old/new counts.
- `ChoiceLevelPanel` uses `RefreshBtn()` to show/hide the No-Ads button based on `PlayCtrl.Instance.Bag.GetToolCount(GameProps.NoAds)`.

To keep the panel in sync:

1. Add usings:
   - `using Game.Events;`
   - `using Config.Game;`
2. Add field:
   - `private EventSubscriber _subscriber;`
3. In `OnOpen()`, after existing refresh logic, call:
   - `AddListener();`
4. Implement:

```csharp
private void AddListener()
{
    EventsUtils.ResetEvents(ref _subscriber);
    _subscriber.Subscribe<UpdatePropEvent>(OnPropUpdate);
}

private void OnPropUpdate(UpdatePropEvent evt)
{
    if (evt.Prop != GameProps.NoAds)
        return;

    RefreshBtn();
}
```

5. Add cleanup:

```csharp
public override void OnClose()
{
    base.OnClose();
    EventsUtils.ResetEvents(ref _subscriber);
}
```

This pattern can be reused for any other event types; adjust the event type and handler body based on the specific UI or gameplay update you need.

## Checklist

Before finishing any new event listener:

- [ ] Required `using` directives are present (`Bear.EventSystem`, `Game.Events`, and payload enums).
- [ ] `EventSubscriber _subscriber` field exists.
- [ ] A single setup method (`AddListener` / `InitEvents`) calls `EventsUtils.ResetEvents(ref _subscriber)` then subscribes all events.
- [ ] Lifecycle method (`OnOpen`, `Awake`, or `Start`) calls the setup method.
- [ ] Handler methods filter on payload when necessary and only perform relevant updates.
- [ ] `OnClose` or `OnDestroy` resets events with `EventsUtils.ResetEvents(ref _subscriber)`.

