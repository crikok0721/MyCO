# Project Context

Last updated: 2026-07-29

## 项目目标

MyCO 是 Windows 10/11 x64 上的本地 WPF 管理器与可逆外观层。它通过受控
CDP 会话将内置 TypeScript Runtime 注入官方 Codex/ChatGPT Desktop 渲染器，
仅装饰可确认的会话展示区域，不修改官方安装文件、用户资料、凭据或网络流量。

核心产品约束：

- 优先使用继承的私有 CDP 管道；TCP 仅可在每次获得用户明确同意后回退。
- DOM 识别不确定时安全失败；只装饰 Assistant 正文。
- 保留原生 User 气泡、代码、Diff、工具、状态、按钮、编辑器和输入区。
- `install()` 幂等，`destroy()` 必须完全移除 MyCO 产生的状态。
- 重启只允许操作已捕获并重新验证的精确 Desktop 进程树。
- MyCO 与 Codex 生命周期独立；退出 MyCO 不应关闭 Codex。
- 配置写入原子、可迁移、向后兼容；日志使用显式白名单且不含聊天正文。

## 当前阶段

Phase 3：Beta 测试与公开测试前稳定性整改。

当前共享版本为 `0.3.0-beta.1`。Beta 1 系统性故障的代码修复、自动化回归、
本机 Manager 实机检查和隔离 Desktop 三轮重启验证已经完成。真实登录会话中的
端到端重启与消息视觉验收，以及外部分发前的签名/干净环境验证仍未完成。

当前工作树已将工程与对外品牌迁移为 MyCO，版本号保持不变；旧用户目录、登录
启动项、单实例内核名和已注入 Runtime 均有明确升级兼容路径。

结论：修复并关闭 `TASK_LIST.md` 中的 Blocker 后可以公开测试；当前不应宣称
已经达到无条件公开 Beta 标准。

## 已实现能力

- 单实例 WPF Manager、三语界面、主题、托盘、启动设置和原子配置。
- 官方 Desktop 安装/运行发现、私有 CDP 管道、显式同意的环回 TCP 回退。
- 多渲染器 Runtime 注入、健康修复、兼容性降级、Safe Mode 和可逆销毁。
- Assistant/User 头像昵称；仅 Assistant 正文气泡化。
- Automatic 与 Whole 气泡模式；代码、Diff、工具和交互面保持原生。
- 精确身份重启、进程树静默期、自动安全强制兜底和 readiness 检测。
- 开发专用 CdpProbe 与隔离 VisualAcceptance 工具。
- 自包含 `win-x64` ZIP 构建与 SHA-256 输出。

## 资料优先级

1. `docs/HANDOFF.md`：当前工作树与下一步。
2. `docs/architecture.md`：架构边界与数据流。
3. `docs/DECISIONS.md`：已确认设计选择。
4. `docs/TASK_LIST.md`：当前优先级。
5. `docs/DEVELOPMENT_LOG.md`：变更与验证记录。
6. `docs/CODEX_HANDOFF.md`、`docs/development-notes.md`：历史证据。
