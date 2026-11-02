# Implementation Plan: GameObject Switch Toggle

**Branch**: `003-gameobject-switch-toggle` | **Date**: 2026-06-12 | **Spec**: `specs/003-gameobject-switch-toggle/spec.md`

## Summary

Complete `GameObjectSwitchToggle` as a small runtime helper that toggles between two assigned GameObject visual states through an optional `CustomButton`, exposes the current On state, allows direct state application, and notifies local listeners after effective state changes. The change must stay local to the component and must not take over ShopPanel-specific purchase, ad, restore, save, config, analytics, or resource-loading behavior.

## Technical Context

**Language/Version**: C# in Unity 6000.0.59f2

**Primary Dependencies**: Unity runtime lifecycle, `GameObject.SetActive`, existing `CustomButton.OnClick` event

**Storage**: Serialized Unity component fields only; no save data, DB data, config table, RemoteConfig, or asset loading

**Testing**: Static compile via `dotnet build Assembly-CSharp.csproj --no-restore` when available; Unity PlayMode/manual prefab validation for lifecycle and visual state; Android/iOS landscape touch smoke before shipping UI that uses the helper

**Target Platform**: Android and iOS mobile landscape runtime UI; Unity Editor PlayMode for validation

**Project Type**: Unity game runtime component

**Performance Goals**: One click produces at most constant-time state updates and no runtime instantiation, resource loads, or persistent allocations from the toggle operation

**Constraints**: Keep the implementation near `Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs`; avoid unrelated ShopPanel refactors; do not add editor lifecycle logic unless separately scoped and protected by project rules

**Scale/Scope**: One helper component, one trigger button, two visual GameObjects, optional local callbacks

## Constitution Check

**I. Published Level Stability**: PASS. The planned change is a local UI helper and does not intentionally modify levels, save/unlock data, config, analytics, resource loading, or platform SDK flows. Regression validation must confirm any prefab using it still opens, toggles, closes, and reopens correctly.

**II. Unity/C# Mobile Technical Boundary**: PASS. The change remains in a Unity-recognized runtime script location and uses existing Unity and project button patterns. No editor-only lifecycle or build pipeline changes are planned.

**III. Verifiable Regression Evidence**: PASS with required follow-up. The plan includes compile validation plus Unity PlayMode/manual checks for initial state, click toggling, repeated enable/disable, missing references, and callback behavior. Device smoke remains required before shipping UI usage.

**IV. Mobile Performance Budget**: PASS. Normal toggling must use serialized references and `SetActive` only, with no instantiation, resource loading, manager initialization, or DB/config access.

**V. Local Complexity Over High-Risk Reuse**: PASS. The plan keeps a narrow generic two-state helper and explicitly avoids replacing the existing `ShopPanelSwitchGroup` without separate regression evidence.

## Project Structure

### Documentation

```text
specs/003-gameobject-switch-toggle/
|- spec.md
|- plan.md
|- research.md
|- data-model.md
|- quickstart.md
`- contracts/
   `- component-contract.md
```

### Source Targets

```text
Assets/Game/UI/ShopPanel/Scripts/GameObjectSwitchToggle.cs
DevelopMD/已有功能设计与使用记录.md
```

## Phase 0: Research

Research output is recorded in `specs/003-gameobject-switch-toggle/research.md`.

Resolved decisions:

- Use existing `CustomButton.OnClick` subscription/cleanup pattern.
- Derive initial state from assigned parts when possible and fall back to Off.
- Notify callbacks only after effective state changes.
- Keep the helper free of DB/config/save/resource/manager access.

## Phase 1: Design & Contracts

Design artifacts:

- `data-model.md` defines the trigger, visual parts, On state, listener notification, and state transitions.
- `contracts/component-contract.md` defines the component-facing runtime contract for local callers and prefab wiring.
- `quickstart.md` defines compile, Unity PlayMode/manual, and mobile smoke validation.

## Implementation Approach

1. Complete `GameObjectSwitchToggle` with serialized initial state, internal `_isOn`, `_isBound`, and optional callback/event support.
2. Bind the trigger in `OnEnable` and unbind in `OnDisable`/`OnDestroy`, following the existing `ShopPanelSwitchGroup` pattern.
3. Apply visual state through a single state-application path that uses null-safe `SetActive` checks and avoids duplicate callbacks when the state has not changed.
4. Expose local runtime methods to read current state, set a specific state, and toggle directly.
5. Keep behavior independent of ShopPanel data, purchases, ads, restore, DB, config, resource loading, and runtime managers.
6. Record implementation facts and unverified validation gaps in `DevelopMD/已有功能设计与使用记录.md`.

## Validation Plan

- Static compile: `dotnet build Assembly-CSharp.csproj --no-restore`
- Unity PlayMode/manual validation:
  - Start with both parts assigned and Off visible; tap trigger once and confirm On visible only.
  - Tap trigger ten times and confirm exactly one visual part is active after each tap.
  - Register a local callback and confirm one callback per effective state change.
  - Disable/enable or close/reopen the host object five times and confirm one toggle per tap.
  - Test missing trigger, missing On part, and missing Off part without runtime crashes.
- Android/iOS landscape smoke when attached to production UI:
  - Touch target works.
  - No visible overlap between On and Off parts.
  - No noticeable hitch during repeated tapping.

## Risk Register

- **Duplicate click subscriptions**: Mitigate with `_isBound` and cleanup in disable/destroy.
- **Ambiguous initial state**: Resolve from current assigned part active states; fallback to Off and document this behavior.
- **Callback recursion or duplicates**: Notify only after effective state changes, and use one state-application path.
- **Unintended ShopPanel behavior changes**: Do not modify `ShopPanel`, `ShopItem`, `ShopPanelSwitchGroup`, purchase/ad/restore flows, or prefab wiring unless a later task explicitly scopes it.
- **Prefab reference gaps**: Null-safe behavior prevents crashes, but PlayMode validation must confirm visual expectations for actual prefab usage.

## Complexity Tracking

No constitution violations or justified complexity exceptions are currently planned.

## Post-Design Constitution Check

PASS. The design remains local, testable, runtime-only, mobile-aware, and does not broaden into shared UI, save/config/resource, platform, or ShopPanel business systems. Remaining risk is validation coverage, not design non-compliance.
