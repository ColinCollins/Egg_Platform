# Feature Specification: Shop Panel Switch Group

**Feature Branch**: `[002-shop-panel-switch-group]`

**Created**: 2026-06-12

**Status**: Draft

**Input**: User description: "实现 ShopPAnelSwitchGroup 功能"

## Constitution Alignment *(mandatory)*

**Gameplay/Level Impact**: This feature affects the ShopPanel UI flow only. It does not intentionally change levels, scenes, save/unlock data, config tables, analytics events, purchase rewards, ad rewards, restore purchase behavior, or resource loading paths.

**Target Platforms**: Android and iOS mobile landscape play are in scope. The switch must work with touch input on the existing ShopPanel.

**Regression Requirement**: Before acceptance, verify opening ShopPanel, switching between shop and skin sections, closing ShopPanel, and existing purchase/ad/restore entry behavior remain usable. Existing IAP, ad reward, and close/resume behavior must not regress.

**Original Behavior Consistency**: Existing ShopPanel product display, purchase flow, rewarded-ad flow, restore purchase flow, close behavior, reward animation, and sold-out behavior must remain unchanged when the shop section is active.

**Risk Avoidance**: Keep the feature local to the ShopPanel switch control so tab display does not modify purchase or ad reward systems. Roll back by disabling or removing the switch component if tab switching blocks shop access or hides existing purchase UI incorrectly. Runtime visual validation in Unity remains unverified in this specification.

**Performance Requirement**: Switching sections should be immediate for users and must not allocate or instantiate visible runtime UI objects during normal tab clicks. The feature should preserve smooth 60 fps target on representative Android/iOS landscape devices.

**Stability vs Reuse Decision**: Prefer a local ShopPanel component because this is a ShopPanel-specific UI state concern and generalizing tab behavior could create unnecessary risk for existing accepted UI flows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Switch To Shop Section (Priority: P1)

As a player in the shop panel, I want to tap the shop tab and see shop goods while the skin section is hidden.

**Why this priority**: The shop tab contains the existing purchase and ad reward entry points, so it must remain available and visually clear.

**Independent Test**: Open the ShopPanel, tap the shop tab, and confirm only the shop section is visible with the shop tab selected.

**Acceptance Scenarios**:

1. **Given** the ShopPanel is open and the skin section is visible, **When** the player taps the shop tab, **Then** the shop section becomes visible and the skin section becomes hidden.
2. **Given** the shop tab is selected, **When** the player taps the shop tab again, **Then** the UI remains on the shop section without showing both sections.

---

### User Story 2 - Switch To Skin Section (Priority: P1)

As a player in the shop panel, I want to tap the skin tab and see skin content while shop goods are hidden.

**Why this priority**: The new skin area must be reachable without overlapping or corrupting the existing shop area.

**Independent Test**: Open the ShopPanel, tap the skin tab, and confirm only the skin section is visible with the skin tab selected.

**Acceptance Scenarios**:

1. **Given** the ShopPanel is open and the shop section is visible, **When** the player taps the skin tab, **Then** the skin section becomes visible and the shop section becomes hidden.
2. **Given** the skin tab is selected, **When** the player taps the skin tab again, **Then** the UI remains on the skin section without showing both sections.

---

### User Story 3 - Preserve Existing Shop Behavior (Priority: P2)

As a player using existing shop goods, I want tab switching to leave purchase, ad reward, restore, and close flows unchanged.

**Why this priority**: ShopPanel is connected to economy, ads, purchase cache, and resume behavior; a visual tab feature must not alter those flows.

**Independent Test**: Switch to the shop section and use the existing buttons as before, confirming their visible state and clickability are preserved.

**Acceptance Scenarios**:

1. **Given** the shop section is selected, **When** the player views shop goods, **Then** existing item states, sold-out states, and price/ad buttons remain consistent with current ShopPanel rules.
2. **Given** the shop section is selected, **When** the player closes ShopPanel, **Then** existing close and resume behavior is preserved.

### Edge Cases

- If tab references are missing from a prefab instance, the switch should avoid runtime crashes and keep any resolvable section working.
- If both sections are initially active, the switch should settle into one selected section and hide the other.
- If both sections are initially inactive, the switch should default to the shop section so existing purchase access remains reachable.
- Repeated fast taps should leave exactly one section visible.
- Opening, closing, and reopening ShopPanel should preserve a valid single selected section without duplicate click handlers.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST show no more than one of the shop section and skin section at a time.
- **FR-002**: System MUST display the selected tab's "on" visual and the non-selected tab's "off" visual.
- **FR-003**: Users MUST be able to switch from shop to skin by tapping the skin tab.
- **FR-004**: Users MUST be able to switch from skin to shop by tapping the shop tab.
- **FR-005**: System MUST default to a valid selected section when ShopPanel opens.
- **FR-006**: System MUST avoid duplicate tab click handling after repeated enable/disable or open/close cycles.
- **FR-007**: System MUST avoid changing purchase, ad reward, restore purchase, reward animation, sold-out, save, config, analytics, and resource loading behavior.
- **FR-REGRESSION**: The feature MUST preserve existing ShopPanel shop behavior unless an intentional change is documented in this specification.
- **FR-CONSISTENCY**: The feature MUST preserve the original player-visible behavior for existing shop goods when the shop section is active.
- **FR-MOBILE**: The feature MUST remain usable on Android and iOS landscape mobile devices with touch input.

### Key Entities *(include if feature involves data)*

- **Shop Section**: The UI section containing existing shop goods and related purchase/ad entries.
- **Skin Section**: The UI section containing skin-related content.
- **Tab Visual State**: The selected and unselected visual state for shop and skin tabs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested tab clicks, exactly one of shop or skin section is visible after the click completes.
- **SC-002**: Users can switch from shop to skin and back to shop within 2 taps without reopening ShopPanel.
- **SC-003**: Existing shop goods remain visible and interactable according to current sold-out and busy-state rules when the shop section is selected.
- **SC-004**: Opening and closing ShopPanel 5 consecutive times leaves one valid selected section visible each time, with no duplicate tab responses.

## Assumptions

- The requested `ShopPAnelSwitchGroup` refers to the existing `ShopPanelSwitchGroup` script in the ShopPanel module.
- The default safe section is the shop section because it contains the existing purchase and ad reward flow.
- Skin data loading, skin purchase/equip behavior, and skin item population are outside this switch-group scope unless specified separately.
- The existing ShopPanel prefab names for switch roots are `ShopToggle_On`, `ShopToggle_Off`, `SkinToggle_On`, `SkinToggle_Off`, `ShopPart`, and `SkinPart`.
