---
description: Core architecture - DI, Event, Save, UI, file organization (jsdk_2 SDK framework)
globs: ["**/*.cs"]
alwaysApply: false
---

# Core Architecture

> jsdk_2 là **SDK framework reusable**, KHÔNG phải game. Path/cấu trúc dưới đây đã sync với code thật (xem `Docs/SourceOfTruth/System/JSDK_Overview.txt`).

## Table of Contents
- [Dependency Injection va Managers](#dependency-injection-va-managers)
- [Event System](#event-system)
- [Save System Architecture](#save-system-architecture)
- [UI Framework](#ui-framework)
- [File Organization](#file-organization)
- [Scene Structure](#scene-structure)

## Dependency Injection va Managers

- **VContainer**: DI container chính (`Assets/!_/SDK Managers/Scopes Scripts/ProjectScope.cs`).
- **Pattern HYBRID**: VContainer inject vào `ProjectManagers` (constructor `[Inject]` ~35 manager); base class pull qua `ProjectManagers.Instance.<XxxManager>` (service-locator façade).
- **3 tầng scope**: `RootLifetimeScope` (chưa dùng) → `ProjectScope` (DontDestroyOnLoad, register chính) → `SceneScope` (per-scene, register `AGameSceneManager`).
- **Manager Hierarchy**: abstract A-prefix + concrete implementation.
- File: `SDK Managers/Scopes Scripts/{ProjectScope,SceneScope}.cs`, `Manager Scripts/ProjectManagers.cs`.

## Event System

- **GenericEventBus** (third-party). Event là **CLASS implement `IEvent`** (không phải struct), ~80 event.
- **AEventManager**: abstract partial chia 3 file domain (System / Gameplay / Monetization). `EventManager` concrete đẩy Unity lifecycle lên bus.
- **Subscribe/Unsubscribe**: override `OnEnable()`/`OnDisable()` của `JMonoBehaviour`.
- File: `SDK Managers/Scripts/Event/AEventManager.{System,Gameplay,Monetization}.cs`.

## Save System Architecture

- **Easy Save 3 (ES3)**: toàn bộ `SavedGameData` lưu dưới 1 key (`JConstants.MainDataKey`).
- **Pattern Main+Sub Data + Functions**: mỗi data tách 2 file partial — `SavedXxxData.cs` (fields + versioning) và `SavedXxxDataFunctions.cs` (getter/setter + logic).
- **Data Versioning**: incremental tự động (`DataVersionExtensions`), chạy ở PreProcessBeforeSave / PreProcessAfterLoad. Auto-save khi OnApplicationPause/Quit.
- **Location**: `Assets/!_/SDK Managers/Save System/Core Save System/`.

## UI Framework

- **Panel/Layer/Flow**: `AUIPanel/UIPanel`; 4 layer Base/Flow/Tutorial/Top (+ Loading). `GuiManager` (single source qua `_activeRegistry`), `UIFlowManager` (command pattern, có back-nav).
- **Widgets** tái sử dụng: Button/Toggle/Switch/Slider/Tab/ProgressBar (pattern 3 lớp Events → logic → SO data).
- **CellView/Scroll**: EnhancedScroller v2 (recycling thật), base `JCellView` + `AScroll/JScroll`.
- File: `Assets/!_/UI/Gui/`, `Assets/!_/UI/...`.

## File Organization

```
Assets/!_/                       # SDK core (~444 .cs)
├── Core Entities/               # JBase, JMonoBehaviour, JScriptableObject, JTest
├── SDK Managers/
│   ├── Scopes Scripts/          # ProjectScope, SceneScope, Scene Scopes
│   ├── Scripts/                 # Init, Load Scene, Event, Backend, Audio, Haptic,
│   │                            #   Input, Gameplay, PoolManager, Addressable, Factory
│   ├── Manager Scripts/         # ResourceManager, ProjectManagers, IAP/Ads/SaveData/Text
│   ├── Save System/             # Core Save System (ES3, data + functions, versioning)
│   ├── Core IAP/                # IAPManager, config, products
│   └── Core State Machines/     # JStateMachine (UnityHFSM), TwoSM/ThreeSM
├── UI/                          # Gui, UI Scripts/Game UI, Core UI, Screens
├── Lives/                       # HeartRegenService, HeartView
├── Analytics/                   # AnalyticsManager, GameAnalytics, Firebase
├── Tutorial/  Features/  World/  Addressables/  C_InputSystem/  C_Localization/
├── Utils/                       # U_Constants (JConstants, GameConstants), Extensions, JDebug
└── Z_Tests/  Admin Test/  Z_Exports/

Assets/!_Game_SDK/               # Game-layer generic
├── 6. Feature/  7. Booster/  9. FTUE/  Level/

Assets/!_Game/                   # Game-specific (chỗ trống, ~10 file)
└── Color, Level (Database), Shared Scripts, Game configs, World, Sfx, VFX
```

## Scene Structure
- **Scenes**: `Splash`, `Gameplay1`, `Gameplay2`, `Gameplay3`.
