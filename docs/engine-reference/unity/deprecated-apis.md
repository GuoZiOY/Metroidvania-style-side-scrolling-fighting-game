# Unity 6000.4 — Deprecated APIs

> "Don't use X → Use Y" reference for Unity 6000.4
> Last verified: 2026-07-28

## Editor APIs

| Deprecated | Replacement | Notes |
|-----------|-------------|-------|
| `SerializedProperty.objectReferenceInstanceIDValue` (int) | `objectReferenceEntityIdValue` (EntityId) | Do not cast EntityId to int |
| `EditorApplication.hierarchyWindowItemOnGUI` | `hierarchyWindowItemOnGUIEntityId` | EntityId-based overload |
| `ObjectSelectorSearchContext.allowedInstanceIds` | `allowedEntityIds` | EntityId-based property |
| `SearchUtils.GetMainAssetInstanceID` | `GetMainAssetEntityId` | Returns EntityId |
| `ProjectWindowCallback.EndNameEditAction` | `NameEditAction` | New class |
| `AssetPreview.IsLoadingAssetPreview(int)` | `IsLoadingAssetPreview(EntityId)` | EntityId overload |
| `EditorUtility.PingObject(int)` | `PingObject(EntityId)` | EntityId overload |

## Asset Import

| Deprecated | Replacement | Notes |
|-----------|-------------|-------|
| `ModelImporterMaterialLocation.External` | [REMOVED — no replacement] | External Material Location no longer supported |
| `SpeedTreeImporter.MaterialLocation.External` | [REMOVED — no replacement] | External Material Location no longer supported |

## Rendering

| Deprecated | Replacement | Notes |
|-----------|-------------|-------|
| `URP_COMPATIBILITY_MODE` define | Render Graph API | Symbol fully removed |
| `UniversalResources.AfterPostProcessColor` | [REMOVED — never used] | Was never functional |
| `LightShadowCasterMode.NonLightmappedOnly` | `ShadowMask` | Renamed |
| `LightShadowCasterMode.Everything` | `DistanceShadowMask` | Renamed |
| HLSL `nonLightMappedOnly` | `useShadowMask` | Renamed in shader code |

## Texture Compression

| Deprecated | Replacement | Notes |
|-----------|-------------|-------|
| PVRTC compression | ASTC or ETC | PVRTC fully removed |

## Packages

| Deprecated | Replacement | Notes |
|-----------|-------------|-------|
| Cloud Diagnostics | [REMOVED] | Since Aug 13, 2025 |
| Standalone Lobby SDK | Building Blocks | Multiplayer migration |
| Standalone Matchmaker SDK | Building Blocks | Multiplayer migration |
| Standalone Multiplay SDK | Building Blocks | Multiplayer migration |
| Standalone Relay SDK | Building Blocks | Multiplayer migration |
| `com.unity.xr.interactionsubsystems` | [REMOVED] | Package removed |
