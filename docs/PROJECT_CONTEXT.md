# Project Context

Last updated: 2026-08-06

## 项目目标

MyCO 是 Windows 10/11 x64 上带有轻度二次元表达的 AI 软件美化插件。其本地
WPF 管理器负责角色、头像、昵称、气泡、主题、预览和运行状态；可逆外观层通过
受控 CDP 会话将内置 TypeScript Runtime 注入官方 Codex/ChatGPT Desktop
渲染器，仅装饰可确认的会话展示区域，不修改官方安装文件、用户资料、凭据或
网络流量。即使隐藏角色图片，Manager 本身也必须保持成熟、专业和适合长期使用。

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

当前增量开发目标为 `0.99.2`，尚未发布。本轮代码已切换到配置 schema 7、几何
基线版本 2（头像 35px、User 头像垂直基线 -4px）、当前 renderer 实时应用结果、
多 renderer 汇总、独立角色位置、Assistant 正文宽度、
启动三选项语义与 MyCO 自有入口事务、托盘通知身份和欢迎页精简，并通过源码
构建与自动化测试。真实登录会话中的 500ms 外观更新、Windows 10/11 通知视觉、
八种启动组合及 100%--200% DPI 验收仍是发布前人工门禁。

当前工作树已将工程与对外品牌迁移为 MyCO；旧用户目录、登录
启动项、单实例内核名和已注入 Runtime 均有明确升级兼容路径。

Manager 第三阶段视觉重构已采用角色优先的信息架构：角色与外观成为主页核心，
连接状态和原有生命周期命令收敛到侧栏连接坞。窗口、图标、卡片、状态块与控件
遵循分层圆角矩形和实心表面体系，不改变 Runtime、配置、进程或 IPC 边界。

结论：修复并关闭 `TASK_LIST.md` 中的 Blocker 后可以公开测试；当前不应宣称
已经达到无条件公开 Beta 标准。

`0.99.1` 的发行工程采用每次构建独立生成的 `CN=Crikok` 自签名 Authenticode
证书，并同时发布 SHA-256、SPDX SBOM 与 GitHub 制品证明。该方案无需外部证书
申请，但不属于 Windows 公共信任链，不能保证消除 SmartScreen 警告。

## 已实现能力

- 单实例 WPF Manager、四语界面、主题、托盘、启动设置和原子配置。
- 角色优先主页、可点击身份入口、侧栏连接坞、原生圆角窗口和统一圆角图标。
- 官方 Desktop 安装/运行发现、私有 CDP 管道、显式同意的环回 TCP 回退。
- 多渲染器 Runtime 注入、健康修复、兼容性降级、Safe Mode 和可逆销毁。
- 保存外观时对所有活动 renderer 并发应用并汇总结果；零活动 renderer 或部分
  失败不会显示为应用成功，连续请求按事务顺序保证最新配置最终生效。
- Assistant/User 头像昵称；仅 Assistant 正文气泡化。
- Assistant/User 的头像和昵称各有独立水平/垂直位置；Assistant 普通正文宽度
  独立可调，不改变 User、表格、代码、Diff、工具或状态区域。
- 默认主题跟随 Windows，头像支持 Manager 内圆形遮罩裁剪；新用户 Assistant
  默认昵称为“菲叶子”并使用打包 Logo。
- Automatic 与 Whole 气泡模式；代码、Diff、工具和交互面保持原生。
- 精确身份重启、进程树静默期、自动安全强制兜底和 readiness 检测。
- 开发专用 CdpProbe 与隔离 VisualAcceptance 工具。
- 自包含 `win-x64` ZIP 构建与 SHA-256 输出。
- 三个独立启动设置、单实例 `--codex-launch` 转发，以及带 reparse、ownership、
  generation 与精确快照保护的当前用户 MyCO 自有快捷方式/协议事务。

## 资料优先级

1. `docs/CONTEXT.md`：当前状态、验证结果与未完成门禁。
2. `docs/architecture.md`：架构边界与数据流。
3. `docs/DECISIONS.md`：已确认设计选择。
4. `docs/TASK_LIST.md`：当前优先级。
5. `docs/DEVELOPMENT_LOG.md`：变更与验证记录。
6. `docs/PROJECT_CONTEXT.md`：产品目标与长期约束。
7. `docs/HANDOFF.md` 与 `docs/archive/`：历史交接，不作为当前产物路径来源。
需求状态以 `docs/REQUIREMENTS.md` 为唯一事实来源；审计证据和恢复批次见
`docs/REQUIREMENTS_AUDIT.md`。自动化测试不替代真实 Codex、WPF、Windows
通知和启动矩阵验收。
