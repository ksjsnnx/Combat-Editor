# Combat Editor（Unity 战斗序列编辑器）
演示地址：https://www.bilibili.com/video/BV1muDEBdELi/?spm_id_from=333.1387.homepage.video_card.click&vd_source=fb788accbfb25dd98553828fe30890a1
一个基于 `ScriptableObject` 的轻量级战斗时间轴编辑器，用于在 Unity 中可视化编排技能动作，并在运行时回放轨道片段（动画、位移、判定、特效、音效、事件等）。

## 功能概览

- 可视化时间轴编辑：轨道 + 片段（Clip）结构，支持拖拽移动、拉伸时长、播放头洗带。
- 多轨道类型：`Animation`、`Movement`、`Hitbox`、`Effect`、`Audio`、`Camera`、`Event`。
- 编辑器侧预览：支持在编辑器窗口里实时采样动画与位移，并绘制 Hitbox / Movement Gizmos。
- 运行时播放器：按时间触发片段开始/结束逻辑，可循环播放、跳转时间、事件回调。
- 数据安全：自动分配 GUID、时间范围约束、片段排序与基础数据校验。

## 目录结构

```text
Assets/Scripts/CombatEditor
├─ Runtime
│  ├─ CombatSequenceAsset.cs            # 序列资源与数据操作 API
│  ├─ CombatTrackType.cs                # 轨道枚举、CombatTrack / CombatClip 数据结构
│  ├─ CombatSequencePlayer.cs           # 运行时回放与触发
│  └─ CombatSequencePreviewBindings.cs  # 预览绑定与 Gizmos 绘制
├─ Editor
│  ├─ CombatSequenceEditorWindow.cs     # 时间轴编辑窗口 + 编辑器预览
│  ├─ CombatSequenceAssetInspector.cs   # 资源 Inspector 扩展入口
│  └─ CombatSequenceAssetMenus.cs       # 菜单创建示例资源
└─ CombatEditorConfig
   └─ PlayerNormalCombat.asset          # 示例战斗配置（项目内）
```

## 快速开始

### 1) 创建战斗序列资源

方式 A（推荐）：

- 在 Project 面板右键：`Assets/Create/Combat/Create Sample ARPG Sequence`
- 会自动创建一个示例序列并打开编辑器窗口。

方式 B（空资源）：

- 在编辑器窗口中点击 `New`，保存为 `.asset`。

### 2) 打开编辑器窗口

- 菜单：`Window/Combat/Combat Sequence Editor`
- 或选中 `CombatSequenceAsset` 后，Inspector 点击 `Open Combat Editor`。

### 3) 绑定预览对象（可选但强烈建议）

在场景中给角色挂载 `CombatSequencePreviewBindings`，并绑定：

- `Owner Root`：角色根节点
- `Animation Root`：动画采样根节点
- `Movement Root`：位移应用节点
- `Effect Root`：特效生成父节点
- `Animator`、`AudioSource`

然后在编辑器窗口顶部把 `previewBindings` 指向该组件，即可看到编辑器预览与 Gizmos。

## 时间轴编辑说明

- `Add Track`：新增轨道，按类型分离数据与逻辑。
- 轨道头按钮：
  - `M`：静音轨道（跳过执行）
  - `L`：锁定轨道（禁止拖拽编辑）
  - `+`：在当前时间新增片段
- 片段编辑：
  - 左键拖拽片段：移动开始时间
  - 拖拽左右边缘：调整时长
  - 右键：复制/删除片段
- 右侧 Inspector：根据选中对象（Sequence / Track / Clip）显示对应属性。

## 数据模型

- `CombatSequenceAsset`
  - `Duration`：总时长（最小 0.25 秒）
  - `FrameRate`：编辑器刻度帧率（最小 1）
  - `Tracks`：轨道列表
- `CombatTrack`
  - `trackType`、`displayName`、`color`
  - `muted`、`locked`
  - `clips`
- `CombatClip`
  - 通用字段：`startTime`、`duration`、`displayName`、`color`
  - 按轨道类型携带专属 payload（如动画状态名、位移曲线、伤害参数、事件参数等）

## 运行时接入

给角色挂载 `CombatSequencePlayer`，并指定：

- `sequence`：要播放的 `CombatSequenceAsset`
- `previewBindings`：可选（用于动画/位移/特效/音效引用）

可用 API：

- `Play()`：从 0 开始播放
- `Stop()`：停止并恢复位移/清理特效
- `SetTime(float time)`：跳转到指定时间并应用当前帧状态

可订阅事件：

- `ClipStarted(CombatTrack, CombatClip)`
- `ClipFinished(CombatTrack, CombatClip)`
- `GameplayEventFired(CombatClip)`（通常用于对接战斗逻辑层）

## 当前实现边界

- `Camera` 轨道目前主要用于数据承载（运行时默认未执行具体摄像机效果逻辑）。
- `Hitbox` 轨道在当前播放器中用于可视化与时序数据，不直接生成物理碰撞体；通常由你的战斗系统消费 `CombatClip` 数据执行命中判定。
- 动画播放依赖 `animationState`（Animator 状态名），请确保状态机中存在同名状态。

## 推荐工作流

1. 先用 `Animation + Movement` 打基础节奏。
2. 再补 `Hitbox + Event` 对齐伤害窗和连段窗口。
3. 最后添加 `Effect + Audio` 做表现层。
4. 通过 `GameplayEventFired` 将编辑器数据桥接到实际战斗逻辑（伤害结算、状态切换、连招窗口等）。

## 许可与说明

- 该模块面向教学/原型开发场景，适合作为战斗系统时间轴层的基础版本。

