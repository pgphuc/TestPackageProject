---
description: Unity-specific safety - asset files, runtime-break zones, verify discipline
globs: ["**/*.cs", "**/*.prefab", "**/*.unity", "**/*.asset", "**/*.meta"]
alwaysApply: true
---

# Unity Safety

Vùng đặc thù Unity mà code-text-tool dễ làm vỡ "solid". Đọc trước khi sửa asset / DI / save / addressables.

## Table of Contents
- [Khong Sua Asset Qua Text](#khong-sua-asset-qua-text)
- [Vung Vo Runtime Du Compile Green](#vung-vo-runtime-du-compile-green)
- [Verify Bang Unity Editor](#verify-bang-unity-editor)
- [Meta Pairing](#meta-pairing)

## Khong Sua Asset Qua Text

- **TUYỆT ĐỐI KHÔNG Write/Edit** `.prefab` `.unity` (scene) `.asset` (ScriptableObject/SO) `.meta`. Chúng là YAML/serialized Unity-managed — chỉnh tay làm hỏng GUID/fileID reference, **vỡ im lặng** (Unity không báo lỗi compile, chỉ mất reference lúc chạy).
- Hook `asset-write-guard.js` chặn cứng việc này; nếu cần đổi prefab/scene/SO → **mô tả thay đổi cho user làm trong Unity Editor**, đừng sửa file.
- Sửa giá trị SerializeField của 1 SO: hướng dẫn user chỉnh trong Inspector, không sửa `.asset`.

## Vung Vo Runtime Du Compile Green

Code dưới đây compile xanh nhưng vỡ lúc chạy — **không tool text nào bắt được, phải cẩn thận tay**:

- **VContainer / ProjectManagers**: thêm manager mà quên `Register` trong scope → `ProjectManagers.Instance.<X>` null runtime. Sửa register phải đối chiếu cả 3: abstract → concrete → exposed accessor.
- **Save versioning (ES3)**: đổi field `SavedXxxData` mà không bump version / không viết migration → **hỏng save cũ của người chơi đang chơi**. Luôn theo incremental versioning.
- **Addressables**: đổi `AssetReference` / address / `AssetID` enum mà không build lại addressables → load fail runtime.
- **enum**: đổi/chèn giá trị int của enum `[Serializable]` đã được serialize vào save/prefab → lệch mapping dữ liệu cũ. Thêm member mới phải gán int MỚI, không tái dùng/chèn giữa.

## Verify Bang Unity Editor

- **Không có `tsc`/`npm test` chạy nhanh** để verify C# Unity. Đừng giả định "viết xong là đúng".
- Sau khi sửa code đụng các vùng trên: **báo user compile trong Unity Editor** (hoặc chạy play scene liên quan) để xác nhận — đó là bước verify thật. Nêu rõ scene/flow cần test (vd scene transition, level reload).

## Meta Pairing

- Mỗi file `.cs` đi kèm 1 `.cs.meta` (Unity tự sinh). **Đừng tạo `.meta` bằng tay** — để Unity generate khi import.
- Xóa/đổi tên `.cs` → `.meta` tương ứng cũng phải theo (qua Unity hoặc xóa cả cặp), đừng để `.meta` mồ côi.
