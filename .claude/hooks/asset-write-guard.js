#!/usr/bin/env node
/*
 * asset-write-guard.js  —  PreToolUse hook (Write|Edit|MultiEdit)
 *
 * Chặn CỨNG việc sửa file asset Unity qua text tool (.prefab/.unity/.asset/.meta).
 * Lý do: chúng là YAML/serialized Unity-managed; sửa tay làm hỏng GUID/fileID,
 * vỡ IM LẶNG (không compile error). Đổi prefab/scene/SO -> làm trong Unity Editor.
 *
 * Cơ chế: block qua stdout JSON (permissionDecision:deny) — KHÔNG dùng exit code
 * (một số shell nuốt exit 2). FAIL-SAFE: bất kỳ lỗi nào -> exit im, KHÔNG chặn
 * (cơ chế giám sát hỏng không được làm hỏng session của dev).
 *
 * Tắt tạm cho 1 session: đặt env CLAUDE_HOOKS_DISABLE=1.
 */
'use strict';

const BLOCKED = /\.(prefab|unity|asset|meta)$/i;

function allowSilently() { process.exit(0); }

try {
  if (process.env.CLAUDE_HOOKS_DISABLE) allowSilently();

  let raw = '';
  process.stdin.on('data', (c) => { raw += c; });
  process.stdin.on('end', () => {
    try {
      const data = JSON.parse(raw || '{}');
      const input = data.tool_input || {};
      // Write/Edit/MultiEdit đều dùng file_path
      const path = input.file_path || input.filePath || '';

      if (path && BLOCKED.test(path)) {
        const ext = (path.match(BLOCKED) || [])[1] || 'asset';
        const reason =
          `Chặn ghi file Unity asset (.${ext}). File .prefab/.unity/.asset/.meta là ` +
          `YAML serialized do Unity quản lý — sửa qua text làm hỏng GUID/fileID, vỡ ` +
          `im lặng lúc runtime. Hãy mô tả thay đổi để user chỉnh trong Unity Editor/` +
          `Inspector. (Xem .claude/rules/unity-safety.md. Tắt tạm: CLAUDE_HOOKS_DISABLE=1)`;
        process.stdout.write(JSON.stringify({
          hookSpecificOutput: {
            hookEventName: 'PreToolUse',
            permissionDecision: 'deny',
            permissionDecisionReason: reason,
          },
        }));
        process.exit(0);
      }
      allowSilently();
    } catch (_) { allowSilently(); }
  });
  process.stdin.on('error', allowSilently);
} catch (_) { allowSilently(); }
