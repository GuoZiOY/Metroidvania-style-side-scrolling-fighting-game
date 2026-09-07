# Unity 6000.4 — Breaking Changes

> From LLM training cutoff (May 2025) to Unity 6000.4.8f1
> Last verified: 2026-07-28

## Critical Breaking Changes

### URP Compatibility Mode — Fully Removed

The `URP_COMPATIBILITY_MODE` scripting define symbol is removed. **All custom render passes must use Render Graph.** Methods dependent on Compatibility Mode are now hard-obsolete.

**Migration**: Convert all custom render passes to Render Graph API. Remove any `#if URP_COMPATIBILITY_MODE` conditional code.

### PVRTC Texture Compression — Removed

PVRTC texture compression is no longer supported. Use **ASTC** or **ETC** instead.

**Migration**: Re-import textures using ASTC (recommended) or ETC compression formats.

### InstanceID → EntityId Replacement

`InstanceID` (int) is deprecated. The new `EntityId` type is the preferred way to identify objects.

**Key implications:**
- Cannot cast `EntityId` to/from `int`
- Cannot rely on sign for asset vs. scene checks
- Sorting by `EntityId` does **not** sort by creation order
- Do not serialize with `ToString()` and `int.Parse`

**Migrated APIs** (old → new):
- `SerializedProperty.objectReferenceInstanceIDValue` → `objectReferenceEntityIdValue`
- `EditorApplication.hierarchyWindowItemOnGUI` → `hierarchyWindowItemOnGUIEntityId`
- `ObjectSelectorSearchContext.allowedInstanceIds` → `allowedEntityIds`
- `SearchUtils.GetMainAssetInstanceID` → `GetMainAssetEntityId`
- `ProjectWindowCallback.EndNameEditAction` → `NameEditAction`
- `AssetPreview.IsLoadingAssetPreview` → new EntityId overload
- `EditorUtility.PingObject` → new EntityId overload

### DLSS SDK Upgrade

DLSS SDK upgraded from v310.3.0 → v310.5.0 (adds Preset L and M).

## Additional Changes

- **External Material Location** removed — `ModelImporterMaterialLocation.External` and `SpeedTreeImporter.MaterialLocation.External` are obsolete
- **Cloud Diagnostics package** deprecated (since August 13, 2025)
- **Standalone Lobby, Matchmaker, Multiplay, and Relay SDKs** deprecated — migrated to Building Blocks approach
- `com.unity.xr.interactionsubsystems` package deprecated
- `UniversalResources.AfterPostProcessColor` deprecated
- **Editor engine assemblies** now compile C# in Release mode for release builds
- `LightShadowCasterMode.NonLightmappedOnly` → `ShadowMask`; `Everything` → `DistanceShadowMask`
- HLSL: `nonLightMappedOnly` → `useShadowMask`
- DX12 Device Filter and Vulkan Device Filter menus moved to *Rendering → Device Filters*
- macOS Native Plugin headers moved from `Unity.app/Contents/PluginAPI` to `Unity.app/Contents/Resources/PluginAPI`
