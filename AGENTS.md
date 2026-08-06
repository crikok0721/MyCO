# MyCO repository instructions

## Scope

MyCO 是 Windows 10/11 x64 上的 .NET 8 WPF 启动器 + 嵌入式 TypeScript Runtime，
用于美化官方 Codex/ChatGPT Desktop 渲染器。本地、可逆、隐私安全、失败安全。

**GitHub:** https://github.com/crikok0721/MyCO
**当前版本:** `0.99.2`（本地增量开发，尚未发布）
**阶段:** Phase 3 — Beta 测试与公开测试前稳定性整改

## Source of truth（阅读顺序）

1. `CLAUDE.md` — 主入口（Claude Code 使用）
2. `docs/REQUIREMENTS.md` — 当前唯一需求账本，先按模块筛选需求 ID
3. `docs/REQUIREMENTS_AUDIT.md` — 历史证据、冲突和恢复批次
4. `docs/CONTEXT.md` — 当前状态、Blockers、验证结果
5. `docs/PROJECT_CONTEXT.md` — 产品目标与长期约束
6. `docs/architecture.md` — 架构与数据流
7. `docs/DECISIONS.md` — 设计决策
8. `docs/TASK_LIST.md` — 当前优先级
9. `docs/TECH_STACK.md` — 技术栈细节

历史文档：`docs/archive/CODEX_HANDOFF.md`、`docs/archive/development-notes.md`

## Build & test

```powershell
Push-Location .\src\MyCO.Runtime; npm ci; npm run check; Pop-Location
dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build
.\scripts\build-release.ps1
```

大陆网络慢时可加 `-UseChinaMirrors`，但不要提交区域源设置。

复杂任务编码前必须完成 Requirement Impact Check：列出受影响需求 ID、必须保持不变的安全/架构边界、关联回归风险和证据缺口。发现冲突时记录 Superseded/Unknown 关系，不得静默删除有效要求。用户明确授权自主执行时仍先增加失败测试，完成后按需求 ID更新代码、自动化和视觉/系统证据；历史 HANDOFF 只作证据，不作为当前事实来源。

## Key rules（不可逾越的安全边界）

- **优先私有 CDP 管道**；TCP 仅可在每次获得用户明确同意后回退，随机端口、127.0.0.1。
- **不修改官方文件**：不碰 app.asar、官方二进制、配置文件、Cookie、凭据或网络流量。
- **失败安全**：DOM 置信度 < 0.72 不装饰；PID/路径/启动时间不匹配不执行关闭操作。
- **仅装饰 Assistant 正文**：保留原生 User 气泡、代码、Diff、工具、状态、按钮、编辑器和输入区。
- **幂等安装、完整销毁**：install() 可重复运行；destroy() 移除所有 MyCO 状态。
- **重启仅限精确验证的进程树**：不按进程名批量终止。
- **配置原子写入**：版本化、向后兼容、损坏时备份并恢复默认值。
- **不提交、推送、创建 PR 或发布**，除非用户明确要求。
- **完成位置反馈**：每次任务完成时，最终报告必须单独说明修改后程序的启动路径和启动方式；若本次未生成程序或无法验证可执行文件，必须明确写明“未生成/未验证”，不得指向旧版本或不相关的二进制。
- **所有 UI 文本保持四种语言**：en-US、zh-CN、zh-TW、ja-JP。

## Code rules

- C#: nullable enabled, async cancellation for I/O, immutable records for contracts.
- TypeScript: strict mode, `data-myco-*` hooks, 不依赖生成/压缩的 class 名。
- Runtime: `src/MyCO.Runtime/dist/MyCO.runtime.js` 是生成的，不要手工编辑。
- 配置写入保持原子性和向后兼容。
- 日志使用白名单，不得包含消息正文、提示词、代码、凭据。

## Key files

| Area | Path |
|------|------|
| WPF UI | `src/MyCO.Manager/` |
| Core services | `src/MyCO.Core/` |
| TypeScript Runtime | `src/MyCO.Runtime/src/` |
| 生成的 bundle | `src/MyCO.Runtime/dist/MyCO.runtime.js` |
| 版本号 | `eng/MyCO.Version.props` |
| .NET 测试 | `tests/MyCO.Tests/` |
| Runtime 测试 | `src/MyCO.Runtime/tests/` |
| 发布脚本 | `scripts/build-release.ps1` |
| 图标源文件 | `assets/MyCO-source.ico` |
| CI | `.github/workflows/build.yml` |

## Git hygiene

- 保留工作区中不相关的用户修改。
- 不要提交生成物、本地验收工件、机器特定路径。
- 发布包应来自 GitHub Actions 或 `scripts/build-release.ps1`。
- 发布时包含 SHA-256 校验和。
