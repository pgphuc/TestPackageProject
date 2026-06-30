---
name: convention-check
description: Kiểm tra code C# đang thay đổi có tuân thủ convention jsdk_2 không (JBase inherit, Log() không Debug.Log, SetActive wrapper, enum [Serializable]+int, A-prefix, namespace JoyCraftSDK.*). Chạy trước khi commit hoặc sau khi viết code. Read-only, báo PASS/FAIL + fix list.
---

# /convention-check

Quét code C# đang thay đổi, đối chiếu convention jsdk_2, báo vi phạm. Không sửa code.

## Cách chạy

1. Lấy phạm vi cần check:
   - Nếu user đưa file/folder cụ thể → dùng cái đó.
   - Mặc định → lấy diff đang dở: `git diff --name-only` + `git diff --name-only --staged`, lọc `.cs`.
   - Nếu không có file `.cs` nào thay đổi → báo "không có C# thay đổi để check" và dừng.

2. Spawn agent **convention-enforcer** (read-only, sonnet) trên các file `.cs` đó, yêu cầu trả về theo return-channel ≤200 từ (verdict PASS/FAIL + `file:line — vi phạm — fix`). Detail dài → agent ghi `Docs/99_scratch/conv-check-<topic>.txt`.

3. Trình bày kết quả cho user: PASS/FAIL + danh sách vi phạm. **KHÔNG tự sửa** — chỉ báo (user review rồi quyết).

## Ghi chú
- Convention đầy đủ ở `.claude/rules/code-conventions.md`.
- Đây là kiểm tra convention jsdk, KHÁC `/code-review` (tìm bug/correctness) — bổ sung, không thay thế.
