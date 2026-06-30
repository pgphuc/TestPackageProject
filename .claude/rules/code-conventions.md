---
description: Code conventions cho Unity C# - curly brace, logging, naming, enum, ScriptableObject
globs: ["**/*.cs"]
---

# Code Conventions

## Table of Contents
- [Code Style](#code-style)
- [Logging](#logging)
- [GameObject SetActive](#gameobject-setactive)
- [Null Checking](#null-checking)
- [Naming Patterns](#naming-patterns)
- [Enum Conventions](#enum-conventions)
- [ScriptableObject Conventions](#scriptableobject-conventions)

## Code Style

- **Curly brace same line** - dấu `{` PHẢI cùng dòng với statement
- Ví dụ đúng:
  ```csharp
  public class MyClass {
      public void MyMethod() {
          if (condition) {
              // code
          }
      }
  }
  ```
- Ví dụ sai:
  ```csharp
  public class MyClass
  {
      public void MyMethod()
      {
          // code
      }
  }
  ```
## ALL CLASSES:
- phải inherit 1 trong các classes sau: JBase, JMonoBehaviour, JScriptableObject 
- JBase là base class cho all normal c# class
- JMonoBehaviour là base class cho tất cả class là MonoBehaviour
- JScriptableObject là base class cho tất cả class là ScriptableObject 
##

## Logging

- **KHÔNG dùng** `UnityEngine.Debug.Log/LogWarning/LogError`
- **Dùng** `Log()`, `LogWarning()`, `LogError()` có sẵn trong các base class: `JBase`, `JMonoBehaviour`, ... (cùng cách dùng)
- Last resort thì dùng JDebug.StaticLog/StaticLogWarning/StaticLogError -> không bao giờ dùng Debug.Log của Unity
## GameObject SetActive

- **KHÔNG dùng** `gameObject.SetActive(bool)` trực tiếp
- **Dùng** `SetActive(bool)` có sẵn trong `JMonoBehaviour` (đã được wrap sẵn)

## Null Checking

- **Chỉ check null khi BẮT BUỘC** (ví dụ: input từ user, data từ external source, optional dependencies)
- **Dùng `Assert.IsNotNull()`** cho các trường hợp validation không cần thiết runtime nhưng cần đảm bảo correctness
- `using UnityEngine.Assertions;` → `Assert.IsNotNull(obj, "message");`


## Naming Patterns

- **Abstract Classes**: Prefix with `A` (e.g., `AGameplayManager`)
- **Interfaces**: Standard `I` prefix
- **Managers**: Suffix with `Manager`
- **Controllers**: Suffix với `Controller` + version number nếu có (e.g., `PassengerController2`)
- **Events**: Descriptive struct names (e.g., `OnCellChange`)
- **Variable Names**: Tên variable PHẢI giống tên class, chỉ khác viết thường chữ đầu
  - Ví dụ đúng: `ACtxEffectCreation` → `ctxEffectCreation`
  - Ví dụ sai: `ACtxEffectCreation` → `context` hoặc `ctx`
  - KHÔNG được đặt tên tắt hoặc tên khác với class name

## Enum Conventions

- **PHẢI có `[Serializable]`** attribute trên mọi enum
- **PHẢI gán explicit int value** cho mỗi giá trị enum
- Ví dụ đúng:
  ```csharp
  [Serializable]
  public enum PassengerColor {
      None = 0,
      Red = 1,
      Blue = 2,
      Green = 3
  }
  ```
- Ví dụ sai:
  ```csharp
  public enum PassengerColor {
      None,
      Red,
      Blue
  }
  ```

## ScriptableObject Conventions

- **PHẢI inherit từ `JScriptableObject`** (không dùng `ScriptableObject` trực tiếp)
- **JScriptableObject** là abstract base class tại `Assets/!/C0_Scripts/Core/Entities/JScriptableObject.cs`
  - Cung cấp access tới backend managers
  - Tất cả concrete classes PHẢI có `[Serializable]`
- **PHẢI có abstract class** với prefix `A` (e.g., `APassengerData`) inherit từ `JScriptableObject`
- **CreateAssetMenu**: Dùng `nameof()` và `GameConstants` path
- Ví dụ đúng:
  ```csharp
  public abstract class APassengerData : JScriptableObject {
      // Abstract members
  }

  [CreateAssetMenu(fileName = nameof(PassengerData), menuName = GameConstants.PlaneAwayPath + nameof(PassengerData))]
  public class PassengerData : APassengerData {
      // Implementation
  }
  ```
  
## Namespace
- dùng namespace JoyCraftSDK (core system), 
- JoyCraftSDK.Game (tất cả code cho game riêng biệt), 
- JoyCraftSDK.Test (all testing)
- JoyCraftSDK.GameUI (all UI)
