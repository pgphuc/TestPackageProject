# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repo này. File này chứa **luật RIÊNG của jsdk_2**; persona / ngôn ngữ / phase-gate / format đã nằm ở global CLAUDE.md (mỗi dev tự cấu hình máy mình) — KHÔNG lặp lại ở đây.

## Mục Lục
- [Ban Chat Repo](#ban-chat-repo)
- [Tech Stack](#tech-stack)
- [Code Map 3 Tang](#code-map-3-tang)
- [Routing — System Den Doc Va Code](#routing--system-den-doc-va-code)
- [Tai Lieu Nguon](#tai-lieu-nguon)
- [Luat Rieng JSDK](#luat-rieng-jsdk)
- [AI Workflow Cho Repo Team](#ai-workflow-cho-repo-team)
- [Co Che Trong Repo](#co-che-trong-repo)

## Ban Chat Repo

- **jsdk_2 = SDK FRAMEWORK Unity reusable, dùng CHUNG cho cả team, commit vào repo.** Luôn improve + ship → ưu tiên maintainable/extendable, không hack nhanh.
- **KHÔNG phải 1 game.** Code game cụ thể (Plane Away: Passenger/Transit/Runway/Plane/Station…) **đã bị XÓA** khỏi repo này (nằm ở repo riêng `plane_away`).
- Câu hỏi đúng hướng: *"SDK cung cấp gì / cắm game mới vào thế nào"*. Câu hỏi SAI hướng: *"game này làm gì"* — không có game ở đây.
- ⚠️ Nếu thấy tài liệu/CLAUDE cũ nhắc tên class game (PassengerController2, TransitSlotManager, LevelEditorWindow…) → **đó là rác từ thời còn game, các class đó KHÔNG tồn tại.**

## Tech Stack

Unity 2022+ **URP** · VContainer (DI) · UniTask (async) · DOTween · Easy Save 3 (save) · Addressables · GenericEventBus · UnityHFSM (state machine) · EnhancedScroller v2 · PoolKit (HellTap) · MoreMountains Feel · Lofelt Nice Vibrations · Cinemachine · Unity Localization · Firebase (Analytics + Remote Config) · AppLovin MAX (ads) · Adjust · Unity IAP/UGS · Odin Inspector.

Bản đầy đủ + trạng thái từng hệ thống: `Docs/SourceOfTruth/System/JSDK_Overview.txt` §9.

## Code Map 3 Tang

| Tầng | Path | Vai trò |
|---|---|---|
| SDK core | `Assets/!_/` | Nền tảng (~444 .cs): Core Entities, SDK Managers, UI, Lives, Analytics, Save, IAP, Input… |
| Game-layer generic | `Assets/!_Game_SDK/` | Generic chờ game cắm: Feature, Booster, FTUE, Level |
| Game-specific | `Assets/!_Game/` | Chỗ trống cho game cụ thể (~10 file data/config) |

⚠️ **Path có ký tự `!_`**: Bash tool trên Windows dễ fail (history expansion / glob) → dùng **Glob** hoặc **PowerShell** để liệt kê/đọc, tránh `Bash ls`/`find`.

## Routing — System Den Doc Va Code

Trước khi grep mù codebase (3000+ .cs), tra bảng này → đọc doc SourceOfTruth + vào đúng folder code:

| Hỏi về | Đọc trước | Code gốc |
|---|---|---|
| Save / ES3 / versioning | JSDK_Overview §3.5 | `!_/SDK Managers/Save System/Core Save System/` |
| Pool / spawn / despawn | `SourceOfTruth/System/PoolManager.txt` | `!_/SDK Managers/Scripts/PoolManager/` |
| DI / scope / ProjectManagers | JSDK_Overview §3.2 | `!_/SDK Managers/Scopes Scripts/`, `Manager Scripts/ProjectManagers.cs` |
| Event / bus / OnXxx | JSDK_Overview §3.4 | `!_/SDK Managers/Scripts/Event/AEventManager.*.cs` |
| UI / panel / flow / scroll | JSDK_Overview §4 | `!_/UI/Gui/`, `!_/UI/...` |
| Lives / heart / regen | `SourceOfTruth/JSDK Systems/Live System/`, JSDK_Overview §5.1 | `!_/Lives/`, `Manager Scripts/ResourceManager.cs` |
| IAP / Ads / Booster | JSDK_Overview §6 | `!_/SDK Managers/Core IAP/`, `Scripts/Backend/Ads/`, `!_Game_SDK/7. Booster/` |
| Feature / FTUE / Tutorial | JSDK_Overview §7 | `!_Game_SDK/6. Feature/`, `!_/Tutorial/` |
| State machine | JSDK_Overview §3.7 | `!_/SDK Managers/Core State Machines/` |
| Input / click / raycast | JSDK_Overview §3.8 | `!_/SDK Managers/Scripts/Input/`, `!_/Utils/RaycastController.cs` |
| Save struct serialize | `SourceOfTruth/System/Serialized-Structures.md` | — |
| Time | `SourceOfTruth/System/Time/` | — |

## Tai Lieu Nguon

- **`Docs/SourceOfTruth/System/JSDK_Overview.txt`** = bản đồ tổng có mục lục — đọc TRƯỚC khi grep code.
- Per-system: `PoolManager.txt`, `Serialized-Structures.md`, `Time/`, `Trainings/`, `JSDK Systems/Live System/`.
- Plan / handoff / nháp đặt ở `Docs/` (`Backlog/`, `Handoffs/`, `99_scratch/`) — **KHÔNG** có `.claude/plans/`.
- **Doc-as-living**: sau khi đổi đáng kể 1 hệ thống → cập nhật SourceOfTruth doc tương ứng (phát hiện doc sai → **suggest cho user, không tự sửa**).

## Luat Rieng JSDK

- **Trước khi viết/sửa C#: đọc `.claude/rules/code-conventions.md`.** Convention rất chặt (JBase/JMonoBehaviour/JScriptableObject inherit, `Log()` không `Debug.Log`, `SetActive()` wrapper, enum `[Serializable]`+int, A-prefix abstract, namespace `JoyCraftSDK.*`).
- **Trước khi sửa code đụng asset/DI/save/addressables: đọc `.claude/rules/unity-safety.md`** — vùng compile-green nhưng vỡ runtime.
- Mọi class mới kế thừa 1 trong 3 base class. Để boot game mới trên SDK: xem JSDK_Overview §11 (hợp đồng abstract cần implement).

## AI Workflow Cho Repo Team

- **Return-channel khi spawn nhiều agent/expert**: mỗi agent trả về main **≤200 từ** (verdict + bullet); detail dài → ghi `Docs/99_scratch/<topic>-N.txt` rồi chỉ trả đường dẫn. Tránh vỡ context main.
- **Targeted read**: file lớn / có mục lục → đọc mục lục + đúng đoạn, không đọc mù toàn file. Tra Routing trước khi grep.
- **Propose-first**: task không trivial → nói trước đụng file nào, làm gì, đợi confirm (theo global rule). Sửa code C# phải hỏi confirm trước (trừ khi user nói "viết luôn").
- **Phân tầng config shared vs cá nhân**: đọc `.claude/README-AI.txt` trước khi đụng `.claude/`.

## Co Che Trong Repo

- **rules/** (auto-load): `code-conventions.md`, `unity-safety.md`, `architecture.md`, `development-workflow.md`.
- **hooks/** `asset-write-guard.js` — chặn cứng Write/Edit vào `.prefab/.unity/.asset/.meta` (đăng ký ở `settings.json`).
- **agents/** `convention-enforcer` — quét diff đối chiếu convention jsdk (read-only).
- **skills/** `/convention-check` — chạy convention-enforcer trên diff hiện tại.
- **settings.json** = permission + hook DÙNG CHUNG (commit) · **settings.local.json** = cấu hình cá nhân mỗi dev (gitignore).
