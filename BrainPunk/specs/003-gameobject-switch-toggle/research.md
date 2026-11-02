# Research: GameObject Switch Toggle

## Decision: Use `CustomButton.OnClick` with explicit lifecycle binding

**Rationale**: `CustomButton` exposes an `OnClick` event and already blocks inactive, non-interactable, delayed, or click-blocked input. `ShopPanelSwitchGroup` uses the same event with an `_isBound` guard and cleanup in `OnDisable`/`OnDestroy`, which matches the repeated open/close risk in the spec.

**Alternatives considered**:

- Unity UI `Button` dependency: rejected because the local script already references `CustomButton`.
- Inspector-only UnityEvent binding: rejected for this helper's core toggle path because lifecycle cleanup and duplicate subscription prevention are explicit requirements.

## Decision: Derive initial state from assigned visual parts, then fall back to Off

**Rationale**: The spec requires settling invalid initial active states into one valid state. Deriving from existing active objects preserves prefab-authored intent when possible. Off fallback is the conservative state for a standalone toggle when both parts are inactive or unavailable.

**Alternatives considered**:

- Always start On: rejected because it may reveal UI that a prefab intentionally kept inactive.
- Require a caller to initialize state: rejected because the component should be safe as a prefab-wired helper.

## Decision: Notify only after effective state changes

**Rationale**: The spec requires avoiding duplicate callback notification when the requested state is already active. This keeps listener logic deterministic and prevents repeated local side effects.

**Alternatives considered**:

- Notify on every apply call: rejected because it violates the callback acceptance scenario.
- Notify before visual state changes: rejected because listeners should observe a state that already matches visuals.

## Decision: Keep the helper independent of data and resource systems

**Rationale**: Project rules forbid unplanned save/config/resource/platform side effects, and the feature only needs local serialized references. No DB, save, RemoteConfig, manager singleton, resource load, or instantiation is needed for the requested behavior.

**Alternatives considered**:

- Auto-discover parts by global names or load missing objects: rejected because it broadens runtime risk and conflicts with the local helper scope.
- Replace `ShopPanelSwitchGroup`: rejected because that component is ShopPanel-specific and already isolates purchase/ad/restore flows from tab visuals.
