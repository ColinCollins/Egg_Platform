# Level073 Transition Recovery

## Background

Entering Level073 could stop in the page-turn transition after the shared HUD refresh raised a null reference. The HUD variant change alone did not resolve the issue.

## Scope And Fixed Behavior

- Affected asset: `Assets/Game/Prefabs/Level/Level_073.prefab`.
- Level073 requests `GamePlayPanel_001`, a HUD variant with no removed default GameObjects.
- The `L73` charge-bar root is attached below the nested Actor prefab root. Its Transform (`4471622129479151677`) is now registered in the Actor PrefabInstance `m_AddedGameObjects` list, using Actor source-root Transform `5665381616076721547`.
- This restores valid nested-prefab ownership and removes the known "Broken Prefab with multiple roots" condition. No shared gameplay, HUD, loading, save, event, or configuration code is changed.

## Verification

- Static check confirms Level073 requests `GamePlayPanel_001`.
- Static check confirms `L73` remains parented to the Actor stripped root and has a matching Actor PrefabInstance `m_AddedGameObjects` entry.
- Static check uses the existing Level_028 Actor-added-object serialization pattern as the reference.
- Unity PlayMode verification remains pending: reimport the prefab, enter from the level-choice panel, confirm the transition closes and no new `PlayCtrl.RefreshGamePanel` null reference appears, then verify short jump, charged jump, and one reset.

## Remaining Risk And Rollback

The level still depends on the shared HUD runtime initialization path. If this specific repair must be rolled back, restore the Actor PrefabInstance `m_AddedGameObjects` list to its prior empty form and reparent `L73` through Unity Prefab Mode before saving; do not move `L73` back to the level root.
