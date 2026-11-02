# Quickstart: GameObject Switch Toggle Validation

## Prerequisites

- Unity project opens in Unity 6000.0.59f2.
- Feature directory is `specs/003-gameobject-switch-toggle`.
- Implementation target is `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`.

## Static Validation

From `D:\GitStore\BrinPunk\BrainPunk`:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Expected outcome:

- Build completes with 0 errors.
- Existing warnings may remain, but no new `GameObjectSwitchToggle` errors are introduced.

## Unity PlayMode Validation

1. Attach `GameObjectSwitchToggle` to a test object or prefab instance with `TriggerBtn`, `OnPart`, and `OffPart`.
2. Start with Off visible and On hidden.
3. Enter PlayMode and tap/click the trigger once.
4. Confirm On is visible and Off is hidden.
5. Tap/click the trigger again.
6. Confirm Off is visible and On is hidden.
7. Tap/click ten consecutive times.
8. Confirm exactly one visual part is active after each tap.
9. Register a local callback listener.
10. Confirm the callback fires once per effective state change and receives the new boolean state.
11. Disable and re-enable the host object five times.
12. Confirm one tap still produces exactly one state change.
13. Repeat with missing trigger, missing On part, and missing Off part.
14. Confirm no runtime crashes occur.

## Regression Validation

If the helper is used in or near ShopPanel UI:

1. Open ShopPanel.
2. Confirm existing shop goods display.
3. Confirm existing purchase/ad/restore/close buttons remain usable according to current rules.
4. Toggle the helper-controlled visuals.
5. Return to shop controls and confirm existing behavior is unchanged.

## Mobile Smoke

On representative Android/iOS landscape device or simulator:

1. Open the screen using the helper.
2. Tap the trigger repeatedly.
3. Confirm touch target behavior is stable.
4. Confirm no visible overlap between On and Off parts.
5. Confirm no noticeable hitch during repeated toggles.

## Remaining Evidence To Record

- Unity version and entry scene/prefab used for validation.
- Static build result.
- PlayMode results for normal, repeated, and missing-reference cases.
- Android/iOS smoke result or explicit unverified-risk note.
