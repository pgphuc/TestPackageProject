---
description: Development workflow - adding features, save extension, configuration (jsdk_2)
globs: ["**/*.cs"]
alwaysApply: false
---

# Development Workflow

> Boot game mới trên SDK này: xem hợp đồng abstract cần implement ở `Docs/SourceOfTruth/System/JSDK_Overview.txt` §11.

## Table of Contents
- [Adding New Features](#adding-new-features)
- [Save System Extension](#save-system-extension)
- [Configuration Addition](#configuration-addition)

## Adding New Features

1. Tạo abstract base class (A-prefix) đúng namespace (`JoyCraftSDK.*`).
2. Implement concrete class kế thừa abstract + 1 base class (JBase/JMonoBehaviour/JScriptableObject).
3. Register vào DI scope đúng tầng (Project / Scene) — `Assets/!_/SDK Managers/Scopes Scripts/ProjectScope.cs` hoặc `SceneScope.cs`.
4. Add vào `ProjectManagers` (`Manager Scripts/ProjectManagers.cs`) nếu cần global access qua `ProjectManagers.Instance`.
5. Định nghĩa event trong `AEventManager.*` nếu cần cross-system communication.

⚠️ Sửa DI wiring / register: xem `unity-safety.md` — thiếu register → null runtime (compile-green).

## Save System Extension

1. Định nghĩa data structure mới. Mỗi feature 1 folder riêng, saved data ở cùng folder của feature; theo pattern Main+Sub Data + Functions (xem `architecture.md` §Save).
2. Test data versioning compatibility (incremental versioning — sai compat làm hỏng save cũ của người chơi).

## Configuration Addition

1. Tạo Config class tương ứng.
2. Register vào Factory cho DI (`Assets/!_/SDK Managers/Scripts/Factory/Afactory.cs`).
3. Add initialization vào controller/manager liên quan (`SDK Managers/Scripts/Init/ProjectInitManager.cs`).
