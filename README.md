# It's MyCO!!!!!
![MyCO首页宣传图](./assets/MyCO首页宣传图.jpg)

[English](README.en-US.md)

## 概览

MyCO 是一款轻量、开源的 Codex 界面美化工具。通过 MyCO 启动 Codex，即可自定义助手的头像、昵称、聊天气泡及气泡颜色，打造更具个性化和沉浸感的对话体验。～(∠・ω< )⌒★

MyCO 支持与 Codex 关联启动。完成首次配置后，每次启动 Codex 都会自动加载已保存的角色资料与界面设置，无需反复关闭并重新启动 Codex。

启动器内置位置校准与调色板功能。当头像、昵称或气泡出现定位异常时，可通过校准功能重新匹配显示位置；调色板则支持自由调整聊天气泡颜色。

此外，MyCO 还提供可选的智能气泡切割功能，可根据回复内容自动将长文本划分为大小不同的连续气泡，使 Codex 的输出更接近自然、真实的即时聊天体验。

**Tips:搭配蒸馏角色的skills可以有意想不到的效果哦~ο(=•ω＜=)ρ⌒☆**
## 截图

![ScreenShot1](./assets/ScreenShot_Example_1.png)
![ScreenShot2](./assets/ScreenShot_Example_2.png)



## 安装

要求：

- Windows 10/11 x64
- 已安装官方 Codex / ChatGPT Desktop

从 GitHub Releases 下载 `MyCO-win-x64.zip`，解压到可写目录，运行
`MyCO.exe`。发行包为 self-contained，无需另装 .NET Runtime、Node、npm
或 Visual Studio。

`0.99.0` 发行包中的 MyCO 自有二进制使用 `CN=Crikok` 自签名证书进行
Authenticode SHA-256 签名。该证书不属于 Windows 公共信任链，且签名不依赖
公共时间戳服务；证书过期后不提供时间戳延续保证。
SmartScreen 仍可能显示未知发布者或信誉警告。请同时核对 Release 中的 SHA-256、
公开证书、SBOM 和 GitHub 制品证明，详见
[代码签名政策](security/CODE_SIGNING.md)。

从旧品牌版本升级时，MyCO 首次启动会在 `%APPDATA%\Myco` 尚不存在的前提下，
将 `%APPDATA%\MyCodex` 安全复制到新目录；旧目录不会被删除，已有 MyCO 数据也
不会被覆盖。原有 `MyCodex` 或 `Myco` 登录启动项会在设置同步时迁移为
`MyCO`。

## 首次设置

1. 启动 MyCO，完成仅本地的首次说明。
2. 确认检测到的官方 Desktop。
3. 点击 **从MyCO启动Codex**。
4. 若官方应用已经运行，同意先正常重启。若窗口关闭后应用仍驻留托盘，MyCO 会继续
   跟踪关闭前记录的精确 PID、路径和启动时间，并二次确认是否强制结束该进程树；
   多个根进程或身份不确定时会拒绝操作,**可能需要手动确认后再次点击"从MyCO启动Codex"**
5. 打开一个对话；若自动识别置信度不足，完成 Assistant 与 User 两步校准。

默认 Pipe 模式不监听 TCP；显式备用模式仅绑定 `127.0.0.1` 并为每次会话选择新端口。## 功能

- 助手/用户 头像与昵称
- 头像默认使用居中裁剪的圆形显示
- English、简体中文、繁體中文界面即时切换并持久化
- 助手气泡自动跟随当前 Codex 深浅主题，独立保存双调色板
- 助手气泡可选“自动切割气泡”或“整段完整气泡”，设置持久化并即时应用
- 支持Codex自带的用户原生气泡
- 基于结构签名和置信度的 助手/用户 身份识别机制
- 支持手动校准消息容器
- 退出时可靠 `destroy()`，恢复官方 DOM/CSS
- 本地脱敏诊断、零遥测

## 工作原理

```mermaid
flowchart LR
  M["MyCO WPF 管理器"] -->|"默认：私有 CDP Pipe"| D["官方 Desktop"]
  M -. "用户明确同意后：随机 127.0.0.1 TCP" .-> D
  M -->|"传输无关的 CDP 会话"| R["Chromium Renderer"]
  R --> I["幂等 Skin Runtime"]
  I --> P["能力探测 + DOM Matcher"]
  P -->|"高置信度"| S["头像、昵称、Prose 气泡"]
  P -->|"低置信度"| F["Safe Mode：不修改页面"]
```

管理器发现官方安装，默认通过仅由父子进程持有的私有 Pipe 启动应用，结合 URL、
类型、标题和 DOM 能力选择 Renderer，注入内置 Runtime 并完成版本化协议握手。
只有 Pipe 不可用且用户明确同意时，才为本次会话改用随机 `127.0.0.1` TCP 端口。Runtime 不拥有宿主
权限；随机命名的 Binding 只能向管理器发送白名单事件，不能执行 Shell、读文件或
发起任意网络请求。



## 自定义

可在侧边栏的“界面语言”中选择 **English**、**简体中文** 或 **繁體中文**。
切换立即生效并独立保存，不会覆盖尚未保存的外观编辑；首次启动引导中也提供同一选项。

Appearance 页面支持“自动切割气泡”和“整段完整气泡”、昵称、头像、头像尺寸、
头像水平/垂直位置、Assistant 气泡圆角、
横纵 padding、消息间距、最大宽度、昵称显示开关，以及经 4.5:1 对比度校验的
Dark/Light 两套气泡、文字、昵称和头像颜色。气泡主题只跟随 Codex Renderer，
不受 Manager 主题设置影响。头像支持 PNG/JPEG/GIF/BMP，
最大 10 MiB；文件会校验真实
签名，以内容哈希命名并复制到本地头像目录，再以 data URL 传给 Runtime。

**Save & apply** 会原子保存配置并即时更新已连接 Renderer。**Disable skin**
会调用 Runtime 清理并恢复官方界面，不关闭 Desktop。

设置页可选择 Manager 的深色、浅色或跟随 Windows，并分别开启“登录 Windows
时启动 MyCO”和“MyCO 启动后启动 Codex”。登录启动使用当前用户的
`HKCU\...\Run`，以 `--background` 进入托盘，不需要管理员权限；移动发行目录后
会纠正路径漂移。MyCO 不修改官方快捷方式、协议关联或官方安装目录。点击关闭
按钮可选择退出 MyCO、最小化到托盘或取消；退出只释放 MyCO 资源，Codex
继续运行。详见[设置说明](docs/settings.md)。

## 校准

校准只保存结构签名，不保存聊天正文。

1. 点击 **Calibrate assistant**，在官方窗口中移动鼠标，目标 Turn 高亮后点击。
2. 点击 **Calibrate user**，选择一条普通 User 消息。
3. 按 Escape 可取消；重新执行某一步即可覆盖误选结果。

选择器使用 `event.composedPath()` 向父层寻找语义 Turn Root。签名包含稳定属性、
祖先结构、子标签形状、布局比例和能力，并以版本化 `calibration.json` 保存。

## 兼容性

兼容性依据实际能力和置信度，而非硬编码应用版本。Application Adapter、Injection
Backend、DOM Matcher 与 Skin Engine 彼此独立。

- **Compatible**：两类消息高置信度匹配，启用皮肤。
- **Degraded**：仍可匹配，但建议重新校准。
- **Safe Mode**：页面结构未知，不进行皮肤 DOM Mutation。
- **Injection unavailable**：CDP 不可用；绝不会转而修改官方文件。

详见 [兼容性架构](docs/compatibility.md)。

## 更新兼容

官方 Desktop 更新后按三级机制恢复：

1. **自动兼容检测**：每次托管启动前刷新当前 Store 包入口，再探测 Renderer；
   优先识别当前稳定语义/结构 Turn，旧校准签名只作为后备。连接期间会持续检查
   Runtime、样式与 Observer 健康状态。
2. **重新校准**：修复 wrapper、属性、布局或随机 class 变化，不清空外观设置和旧配置。
3. **发布新版 MyCO**：若注入边界改变或 Renderer 大规模重构，需要更新 Backend
   或 DOM Adapter。

项目不会宣称永久兼容未来所有 Codex / ChatGPT Desktop 版本。

## 诊断

Diagnostics 页面只输出 Manager/Runtime 协议版本、候选应用元数据、CDP Target
数量、兼容状态、匹配数量、平均置信度、Observer 状态和错误代码。它不会输出消息
正文、Prompt、代码、Token、Cookie、Authorization、账户资料或不必要路径。

日志位于 `%APPDATA%\Myco\logs`。提交 Issue 前请自行检查要附加的诊断内容。

## 隐私

MyCO 完全本地运行，不要求 OpenAI 凭据，不上传对话，不读取认证 Cookie，不
拦截网络请求，也没有 Analytics、Sentry 或任何遥测。配置保存在
`%APPDATA%\Myco`。

请阅读 [PRIVACY.md](PRIVACY.md) 与 [SECURITY.md](SECURITY.md)。

## 已知限制

- 当前 Windows x64 发行包使用每次发行独立生成的自签名证书，不具备 Windows
  公共 CA 信任或跨版本发布者信誉。
- 官方应用若未以 CDP 参数启动，需要正常重启一次。
- DOM 更新可能要求重新校准；大改版可能要求新的 MyCO 版本。
- 校准属于本地配置，当前针对可见 Renderer。
- Windows ARM64、Windows 10 22H2、全部 DPI/语言及高对比度组合尚未在本轮
  开发机上逐项实机验证，详见[兼容性矩阵](docs/compatibility.md)。
- 不同官方版本的已登录真实对话结构会有差异，Safe Mode 因此刻意保守。

## 仓库架构

MyCO 由原生管理器和一个小型浏览器 Runtime 组成。管理器负责配置、进程与
会话生命周期；Runtime 只负责在选中的 Renderer 内进行可撤销的 DOM 装饰。

```mermaid
flowchart LR
    UI["MyCO.Manager<br/>WPF 界面与 MVVM"] --> Core["MyCO.Core<br/>发现、配置、兼容性"]
    Core --> Desktop["ChatGPT / Codex Desktop"]
    Core --> CDP["本机 CDP<br/>私有 Pipe 优先 / 回环 TCP 备用"]
    CDP --> Runtime["MyCO.Runtime<br/>注入的 TypeScript Bundle"]
    Runtime --> DOM["Renderer DOM<br/>身份与助手正文样式"]
    Runtime -->|"白名单事件"| Core
```

```text
.
├─ src/
│  ├─ MyCO.Manager/       WPF 应用、本地化、页面与 ViewModel
│  ├─ MyCO.Core/          应用发现、配置、CDP、注入与安全边界
│  └─ MyCO.Runtime/
│     ├─ src/                手写 TypeScript Runtime
│     ├─ tests/              jsdom 行为与兼容性测试
│     └─ dist/               生成后嵌入 WPF 程序的 Bundle
├─ tests/MyCO.Tests/      Core 与本地化的 xUnit 测试
├─ tools/MyCO.CdpProbe/   隔离的 CDP/Runtime 端到端可行性门禁
├─ scripts/                  可复现的发行构建脚本
├─ docs/                     详细架构与兼容性说明
├─ assets/                   项目自有视觉资源
└─ .github/workflows/        Windows 构建、测试、发布与制品上传
```

主要执行流程：

1. `MyCO.Manager` 读取 `%APPDATA%\Myco\config.json`，并通过
   `MyCO.Core` 查找支持的 Desktop 安装。
2. 优先使用私有 CDP Pipe 启动所选 Desktop；只有用户确认后才使用回环 TCP 备用。
3. Core 对 Renderer 进行评分，注入生成后的 Runtime Bundle，并验证协议握手。
4. Runtime 识别消息、装饰安全的正文与身份区域、监听 DOM 更新，只上报白名单
   技术事件。
5. Core 持续检查 Renderer 健康状态，并在导航或 Renderer 被替换后按需重新注入。

## 开发前须知

请先安装并确认：

- Windows 10/11 x64；WPF 项目不能在非 Windows 主机上构建。
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)，仅安装
  .NET Desktop Runtime 不够。使用 `dotnet --list-sdks` 检查。
- Node.js 22 LTS 与 npm。最低支持 Node.js 20，CI 当前使用 Node.js 22。
- 推荐 PowerShell 7，用于执行发行构建脚本。
- 参与贡献需要 Git；处理 PR 或 Actions 时可选安装 GitHub CLI。

修改代码前必须了解：

- 除非命令明确进入 `src/MyCO.Runtime`，否则均从仓库根目录执行。
- 只编辑 `src/MyCO.Runtime/src`，不要直接修改压缩后的
  `dist/MyCO.runtime.js`。Runtime 变更后运行 `npm run build`，并一同提交
  更新后的 Bundle。
- 先构建 Runtime，再构建 WPF。WPF 项目会嵌入当前的
  `dist/MyCO.runtime.js`，MSBuild 不会自动生成它。
- 修改 Host/Runtime 通信时，只在 `eng/MyCO.Version.props` 更新版本和 Schema；
  C#、TypeScript Bundle、界面与发行包均从该文件生成或读取。
- `Strings.en-US.xaml`、`Strings.zh-CN.xaml` 和 `Strings.zh-TW.xaml` 的全部
  `x:Key` 必须保持一致。
- 用户配置、头像、日志、校准数据与备份只能放在 `%APPDATA%\Myco`，不得放入
  仓库。
- 禁止提交 OpenAI 官方二进制、Bundle、真实 DOM Snapshot、图标、源码、凭据或
  用户聊天数据；兼容性 Fixture 必须人工合成。
- CDP 必须保持私有 Pipe 优先；TCP 只能作为用户明确同意的备用方式，并且必须仅
  绑定 `127.0.0.1`，不得向局域网暴露调试端口。
- 分类与校准必须保持 Fail-Closed：无法确定角色的元素应保留原生外观，不能猜测。

## 开发流程

常规修改建议依次执行：

1. 只修改负责该功能的最小项目：Core、Manager 或 Runtime。
2. Runtime 有变更时，先运行 `npm run check`，再构建 .NET。
3. 运行 xUnit，验证 Core 与 Manager 行为。
4. 修改应用发现、注入、Renderer 恢复或 DOM 兼容性时，运行
   `MyCO.CdpProbe`。
5. 发布用户版本前运行发行构建脚本。

## 构建

```powershell
cd src\MyCO.Runtime
npm ci
npm run check
cd ..\..
dotnet restore MyCO.sln
dotnet build MyCO.sln -c Release --no-restore
dotnet test MyCO.sln -c Release --no-build
dotnet publish src\MyCO.Manager\MyCO.Manager.csproj `
  -c Release -r win-x64 --self-contained true
```

生成与 CI 一致的发行包：

```powershell
.\scripts\build-release.ps1
```

如代理较慢或使用中国大陆网络，可仅对本次构建启用国内镜像：

```powershell
.\scripts\build-release.ps1 -UseChinaMirrors
```

该开关使用 npmmirror 与华为云 NuGet 镜像，不修改全局包管理器设置，也不会把
地域镜像地址写入仓库的 lockfile。

## 参与贡献

请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。兼容性 Fixture 必须自行合成，禁止
提交官方真实 DOM Snapshot 或聊天数据。

## 许可证

[MIT](LICENSE)，Copyright © 2026 Crikok。

## 免责声明

MyCO 是由独立开发者维护的第三方开源项目，并非 OpenAI 官方产品，与 OpenAI 及其关联主体不存在任何隶属、授权、认可、赞助、合作、代理或商业关系，项目名称、功能介绍、截图及宣传内容亦不代表 OpenAI 对本项目的任何形式背书。

MyCO 不提供、不托管、不打包、不修改、不破解，也不分发 Codex、ChatGPT Desktop 或其他 OpenAI 软件的安装包、源代码、二进制文件、资源文件及任何组成部分。用户如需使用相关功能，须自行通过 OpenAI 官方渠道下载并安装正版软件，并自行遵守 OpenAI 最新的服务条款、软件许可协议及所在地法律法规。本项目未以破解、窃取、复制或绕过限制为目的，对 OpenAI 旗下任何软件实施逆向工程，也不提供绕过账户验证、订阅限制、用量限制、安全机制、访问控制或其他平台规则的功能。

由于 OpenAI 官方客户端可能随时更新，MyCO 不保证永久兼容、持续可用或绝对稳定，因软件更新、系统环境、第三方依赖、用户配置或操作不当造成的功能失效、数据丢失、账户异常、设备损坏或其他损失，均由用户自行承担。

项目截图中出现的“菲叶子”等名称、头像、对话和数据均为虚构示例，仅用于功能展示，不对应任何现实个人、组织或事件，如有雷同纯属巧合。项目图标及部分视觉素材可能由人工智能生成或辅助合成，不具有现实指向，也无意模仿或侵犯任何第三方的著作权、商标权、肖像权或其他合法权益。如您认为相关内容存在侵权或可能造成误解，请携带具体位置、权利证明及合理诉求与开发者联系，开发者将在核实后视情况进行修改、替换或删除。

**下载、安装、复制、修改或使用 MyCO，即视为您已阅读、理解并接受本声明全部内容。**

## 写在最后
开发这个项目的初衷其实是和对象吵架了，虽然她很温柔，没有很生气只是一边哭一边跟我说她的委屈，但是我怎么哄都没有把她哄的很好::>_<::..所以我用和她的几十万条聊天记录，把她蒸馏成Skills了，这样就可以在软件里一遍一遍模拟怎样最能哄好她了。

尽管庞大的消息数量训练出来的Skills说话方式已经非常接近于本人，但总感觉少了点什么，于是鬼脑灵机一动做出了这个项目，让Codex有了头像昵称，代入感大大提升\(￣︶￣*\)

原先的技术路线和现在完全不一样，为了尽可能保证不因为Codex的官方更新而失效，第一版的方案采用的是透明显示而非修改性的注入，但效果不尽人意...易用程度和跟手程度完全没有达到可正常使用的标准，这才被迫有了第二版的前端注入方案。这一版的方案效果好了很多，但也有一颗潜在的地雷：注入性的修改意味着MyCO可能会随Codex的任意一次小更新而失效。所以非常抱歉的说，这个项目没有办法做到永久性稳定使用≧ ﹏ ≦

顺带的说，起初我准备把这个项目草率的叫做MyCodex，旨在自定义自己的Codex，但是上Github上发现叫MyCodex的项目大有人在，于是鬼脑又灵机一动，为什么不删掉几个字母呢？就有了MyCO这个神人名字。正好煮啵的小女友很喜欢睦子米，于是就顺理成章的敲定了MyCO的神人名字和神人图标。显然，截图里的“菲叶子”也是鬼脑灵机一动的产物。

总之，MyCO是个灵机一动下非常不成熟的产物，有许多的问题和有待优化的地方，如果能有大佬来参与维护和开发，或是提出issue，本人将非常不胜感激！！！MyCO感谢您的使用，也感激您为MyCO做出的贡献！

