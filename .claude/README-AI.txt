================================================================================
  README-AI — Cách dùng Claude Code trên jsdk_2 (SDK framework Unity, team-shared)
================================================================================
Đọc file này 1 lần khi onboard. Mục đích: dev mới (và "Claude clone" của họ)
hiểu ngay (1) repo này dùng AI thế nào, (2) cái gì CHIA SẺ cả team vs cái gì
CÁ NHÂN mỗi máy, (3) cơ chế gì đang chạy. Repo dùng CHUNG → đừng commit config
riêng của máy mình đè người khác.

================================================================================
  MỤC LỤC
================================================================================
  1.  Bản chất repo (1 dòng)
  2.  Ba tầng cấu hình — SHARED vs CÁ NHÂN
  3.  Checklist setup cho dev mới
  4.  Cơ chế đang chạy trong repo (commit)
  5.  Cách làm việc với AI trên jsdk (quy ước)
  6.  Cái CỐ Ý KHÔNG có (và vì sao)
  7.  Khi nào & cách sửa setup

================================================================================
  1. BẢN CHẤT REPO
================================================================================
jsdk_2 = SDK FRAMEWORK Unity reusable, KHÔNG phải 1 game. Code game cụ thể đã
xóa (repo plane_away). Bản đồ hệ thống: Docs/SourceOfTruth/System/JSDK_Overview.txt.
Luật AI riêng của repo: .claude/CLAUDE.md.

================================================================================
  2. BA TẦNG CẤU HÌNH — SHARED vs CÁ NHÂN
================================================================================
Phân biệt rạch ròi, nếu không sẽ commit nhầm config máy mình lên repo team:

  TẦNG A — USER-GLOBAL (mỗi dev, máy mình, KHÔNG nằm trong repo)
    Vị trí: C:\Users\<bạn>\.claude\  (hoặc ~/.claude/ trên mac)
    Chứa: persona, ngôn ngữ, model default, sở thích cá nhân, memory cá nhân.
    -> CLAUDE.md project KHÔNG lặp lại mấy cái này; nó nằm ở đây.

  TẦNG B — PROJECT-SHARED (commit vào repo, áp cho MỌI dev)
    Vị trí: .claude/ trong repo (trừ settings.local.json).
    Chứa: CLAUDE.md (luật riêng jsdk), rules/, hooks/, agents/, skills/,
          settings.json (permission + hook dùng chung), .claude-code.json,
          README-AI.txt (file này).
    -> Sửa mấy file này = ảnh hưởng cả team. Đổi đáng kể -> báo team.

  TẦNG C — PROJECT-LOCAL (mỗi dev, trong repo NHƯNG gitignore)
    Vị trí: .claude/settings.local.json
    Chứa: permission cá nhân, path máy mình, lệnh destructive riêng.
    -> ĐÃ được .gitignore. Đừng ép-commit nó.

QUY TẮC VÀNG: file trong repo .claude/ chỉ chứa cái ĐÚNG cho mọi dev. Cái phụ
thuộc 1 người / 1 máy / 1 phong cách -> để tầng A hoặc C, KHÔNG đẩy lên repo.
Hook/permission shared dùng $CLAUDE_PROJECT_DIR, KHÔNG hardcode path user.

================================================================================
  3. CHECKLIST SETUP CHO DEV MỚI
================================================================================
  [ ] 1. Cài Claude Code. Cấu hình persona/ngôn ngữ/model ở TẦNG A (~/.claude/
         CLAUDE.md) theo ý mình — KHÔNG sửa CLAUDE.md trong repo cho mục đích này.
  [ ] 2. (Nếu muốn hook asset-guard chạy) cài Node.js, đảm bảo `node` trên PATH.
         Không có Node -> hook tự bỏ qua (fail-safe), không chặn gì, nhưng mất
         lớp bảo vệ chống sửa nhầm .prefab/.unity/.asset/.meta.
  [ ] 3. Permission cá nhân (path máy mình, lệnh riêng) -> .claude/settings.local
         .json (đã gitignore). KHÔNG thêm vào settings.json (đó là của team).
  [ ] 4. Đọc .claude/CLAUDE.md + Docs/SourceOfTruth/System/JSDK_Overview.txt.
  [ ] 5. Trước khi nhờ AI viết C#: nhớ convention chặt (.claude/rules/
         code-conventions.md) — AI đã được trỏ tự đọc, nhưng bạn nên biết.

================================================================================
  4. CƠ CHẾ ĐANG CHẠY TRONG REPO (commit, áp mọi dev)
================================================================================
  rules/ (auto-load vào context):
    - code-conventions.md   luật code C# (base class, log, SetActive, enum, ns...)
    - unity-safety.md       vùng vỡ runtime + cấm sửa asset + verify qua Editor
    - architecture.md       kiến trúc core (DI/event/save/UI/file org) — path-scoped .cs
    - development-workflow.md quy trình thêm feature/save/config — path-scoped .cs

  hooks/asset-write-guard.js  (đăng ký ở settings.json):
    Chặn CỨNG Write/Edit vào .prefab/.unity/.asset/.meta (asset Unity sửa qua
    text -> vỡ im lặng). Block qua stdout JSON deny. Fail-safe: lỗi/thiếu Node
    -> bỏ qua, không chặn. Tắt tạm 1 session: env CLAUDE_HOOKS_DISABLE=1.

  agents/convention-enforcer.md  (model sonnet, read-only):
    Quét diff C# đối chiếu convention jsdk, báo vi phạm (không sửa).

  skills/convention-check  ->  gõ /convention-check:
    Chạy convention-enforcer trên diff hiện tại, báo PASS/FAIL + fix list.

  settings.json: permission an toàn dùng chung (git read, diagnostics) + đăng ký
    hook. settings.local.json: phần cá nhân (gitignore).

================================================================================
  5. CÁCH LÀM VIỆC VỚI AI TRÊN JSDK (quy ước)
================================================================================
  - Đọc SourceOfTruth/Routing (bảng trong CLAUDE.md) TRƯỚC khi grep mù 3000+ .cs.
  - Convention chặt: mọi class kế thừa JBase/JMonoBehaviour/JScriptableObject;
    Log() không Debug.Log; SetActive() wrapper; enum [Serializable]+int; A-prefix.
  - Propose-first cho task không trivial; sửa C# hỏi confirm trước (trừ "viết luôn").
  - Spawn nhiều agent/expert: ép return-channel <=200 từ/agent + detail ghi
    Docs/99_scratch/ (tránh vỡ context main).
  - Sửa code đụng DI/Save/Addressables/enum -> verify bằng compile trong Unity
    Editor (không có tsc/npm test nhanh). Xem unity-safety.md.
  - Đừng sửa .prefab/.unity/.asset/.meta bằng tay -> mô tả cho user làm trong Editor.

================================================================================
  6. CÁI CỐ Ý KHÔNG CÓ (và vì sao)
================================================================================
Setup này tham khảo 1 setup Claude Code khác (của 1 CTO làm web/backend, solo,
auto-mode). Các thứ sau CỐ Ý KHÔNG port vì không hợp Unity team:
  - 123.txt/456.txt workspace + vps-exec/vps-command-prep + @relay:
      kênh chạy lệnh VPS prod. jsdk là Unity client, không có VPS -> vô nghĩa.
  - context-watch HARD-block (chặn theo ngưỡng token):
      là preference của 1 người auto-mode; áp cả team = mỗi dev bị chặn giữa
      việc -> friction. Bỏ.
  - read-guard deny cứng file >300 dòng:
      file C# Unity dài là bình thường -> deny nhầm. Thay bằng rule mềm
      (targeted read) trong CLAUDE.md.
  - knowledge-graph 24 file (14 leaf domain backend):
      over-engineering. Thay bằng 1 bảng Routing gọn trong CLAUDE.md, tận dụng
      Docs/SourceOfTruth có sẵn.
  - milestone closeout / self-prompt macro prose:
      gắn workflow cá nhân; với team nên dùng skill (discoverable) thay macro.
Nguyên tắc: chỉ port cơ chế có ROI rõ cho team, dạng warn/rule mềm; chỉ 1 hook
deny CỨNG là asset-write-guard (đặc thù Unity, lợi ích "solid" rõ nhất).

================================================================================
  7. KHI NÀO & CÁCH SỬA SETUP
================================================================================
  - Thêm/sửa luật áp mọi dev      -> .claude/CLAUDE.md hoặc rules/<tên>.md (commit).
  - Thêm hook                     -> .claude/hooks/<tên>.js (block qua stdout JSON
                                     deny, fail-safe exit im khi lỗi) + đăng ký ở
                                     settings.json mục hooks. Không đăng ký = không chạy.
  - Thêm agent                    -> .claude/agents/<tên>.md (frontmatter name/
                                     description/tools/model). Mặc định model sonnet.
  - Thêm skill                    -> .claude/skills/<tên>/SKILL.md. Gõ /<tên>.
  - Permission cá nhân            -> settings.local.json (gitignore), KHÔNG settings.json.
  - Đổi setup đáng kể             -> báo team + cập nhật README-AI.txt này (doc-as-living).
================================================================================
  HẾT
================================================================================
