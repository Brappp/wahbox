# WahBox Architecture Redesign

## Overview
WahBox will be redesigned as a modular utility suite that combines multiple smaller utilities into one comprehensive plugin.

## Module Categories

### 1. **Tracking Modules** (Current)
- Currency tracking (tomestones, scrips, etc.)
- Daily/Weekly task tracking
- Special content tracking

### 2. **Utility Modules** (New)
- **Radar Module** - Display nearby entities with customizable filters
- **Speedometer Module** - Fun speed displays with multiple styles
- **Future modules** - Easy to add new utilities

## New Architecture

### Core Changes

1. **Module System Enhancement**
   - Add new `ModuleCategory` enum: `Tracking`, `Utility`, `Display`, `Tools`
   - Each module can have its own window/overlay
   - Modules can be completely independent

2. **Main Window Redesign**
   - Tab-based interface with categories
   - Quick access toolbar for frequently used features
   - Module grid view with enable/disable toggles
   - Search and filter capabilities

3. **Settings Window Redesign**
   - Tree view for module settings
   - Global settings section
   - Per-module configuration pages
   - Import/Export settings

## Proposed UI Layout

### Main Window
```
[WahBox - All-in-One Utility Suite]
╔════════════════════════════════════════════╗
║ [🏠 Home] [📊 Tracking] [🛠️ Utilities]     ║
║ [⚙️ Settings]                   [🔍 Search] ║
╠════════════════════════════════════════════╣
║                                            ║
║  Quick Access Toolbar:                     ║
║  [💰 Currencies] [📅 Dailies] [🎯 Radar]   ║
║  [🏃 Speed] [+ Add]                        ║
║                                            ║
╠════════════════════════════════════════════╣
║                                            ║
║  Active Modules:                           ║
║  ┌─────────────┐ ┌─────────────┐          ║
║  │ 💰 Currency │ │ 🎯 Radar    │          ║
║  │ 1850/2000   │ │ 5 players   │          ║
║  │ [View] [⚙️] │ │ [View] [⚙️] │          ║
║  └─────────────┘ └─────────────┘          ║
║                                            ║
║  ┌─────────────┐ ┌─────────────┐          ║
║  │ 📅 Dailies  │ │ 🏃 Speed    │          ║
║  │ 3/5 done    │ │ 15.2 y/s    │          ║
║  │ [View] [⚙️] │ │ [View] [⚙️] │          ║
║  └─────────────┘ └─────────────┘          ║
║                                            ║
╚════════════════════════════════════════════╝
```

### Module Management
```
[Module Manager]
╔════════════════════════════════════════════╗
║ Available Modules                          ║
╠════════════════════════════════════════════╣
║ 📊 Tracking Modules                        ║
║ ├─ ✅ Currency Tracker                     ║
║ ├─ ✅ Daily Tasks                          ║
║ ├─ ✅ Weekly Tasks                         ║
║ └─ ⬜ Custom Trackers                      ║
║                                            ║
║ 🛠️ Utility Modules                         ║
║ ├─ ✅ Player Radar                         ║
║ ├─ ✅ Speedometer                          ║
║ ├─ ⬜ Teleport Helper                      ║
║ └─ ⬜ Screenshot Tool                      ║
║                                            ║
║ 🎨 Display Modules                         ║
║ ├─ ⬜ Custom Overlays                      ║
║ └─ ⬜ HUD Extensions                       ║
╚════════════════════════════════════════════╝
```

## Implementation Plan

### Phase 1: Core Refactoring
1. Create new module categories
2. Implement module enable/disable system
3. Create module registry with metadata

### Phase 2: UI Redesign
1. New main window with module grid
2. Tab-based navigation
3. Quick access toolbar

### Phase 3: Module Integration
1. Integrate Radar module from Wahdar
2. Integrate Speedometer module from Zoomies
3. Update existing modules to new system

### Phase 4: Polish
1. Unified theme/styling
2. Module communication system
3. Performance optimizations

## Benefits
- **Modularity**: Easy to add/remove features
- **Flexibility**: Users can enable only what they need
- **Scalability**: Can grow with new utilities
- **Performance**: Disabled modules don't consume resources
- **User Experience**: One plugin instead of many
