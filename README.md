# RimTalk StyleExpand

[English](README_EN.md) | 中文

**让 RimTalk 生成的对话模仿指定文风**

基于向量检索的文风扩展 Mod，支持自定义文风。

## 功能特性

- **文风管理**：一个 txt 文件 = 一种文风
- **向量检索**：根据角色特征自动检索相似文风片段
- **自动变量注册**：自动注册 `{{style_xxx}}` 变量到 RimTalk
- **缓存机制**：切分结果和 embedding 缓存到本地
- **多语言支持**：中文 / English
- **多版本支持**：RimWorld 1.5 / 1.6
- **分批切分**：大文件支持中断后继续
- **智能采样**：超大文件自动采样代表性片段
- **LLM 描述生成**：自动分析文风生成风格描述
- **文件监听**：自动检测文风文件变化
- **异步处理**：切分和生成操作不阻塞游戏主线程

## 快速开始

1. 放置 `.txt` 文风文件到 `Styles/` 目录
2. 启动游戏，打开 Mod 设置
3. 配置 **Embedding API**（用于切分）
4. 点击 **扫描文风** 扫描文件
5. 选择一个文风，点击 **切分文风**
6. 完成！

### 文风文件建议

| 语言 | 推荐字数 | 说明 |
|------|---------|------|
| 中文 | 5,000 - 20,000 字 | 太少检索效果差，太多处理时间长 |
| 英文 | 3,000 - 15,000 词 | 同上 |

> 更大的文件可以使用，但切分和 embedding 计算时间会变长。

## 配置要求

- **RimTalk** Mod（必须）
- **Embedding API**（用于切分和检索）
- **LLM API**（可选，用于自动生成文风描述）

### 支持的 API

- Ollama（默认 `http://localhost:11434/api/embeddings`）
- OpenAI Embedding API
- 其他 OpenAI 兼容 API

推荐模型：`nomic-embed-text`、`text-embedding-3-small`

### 使用 RimTalk API 配置

LLM API 配置可选择复用 RimTalk 的配置：
- 支持 RimTalk 的所有提供商（Google、OpenAI、DeepSeek、Player2、本地）
- 自动获取 API URL、Key、Model

## Scriban 变量

StyleExpand 自动注册以下变量到 RimTalk，在高级模式中直接使用：

| 变量 | 说明 |
|------|------|
| `{{style_name}}` | 当前文风名称 |
| `{{style_prompt}}` | 文风描述 |
| `{{style_chunks}}` | 检索到的示例片段 |
| `{{style_full}}` | 完整文风 prompt（以上组合） |

### 示例模板

```
[Style Instruction]
Please imitate the following writing style ({{style_name}}) when generating dialogue:

{{style_prompt}}

{{style_chunks}}
```

## 注入机制

### 注入方式

StyleExpand 使用 RimTalk 官方 API 注入：

1. **注册变量**：自动注册 `{{style_xxx}}` 变量
2. **添加 PromptEntry**：在 RimTalk 预设中添加 "Style Context" 条目
3. **注入位置**：在 System Prompt 之后

### 简单模式 vs 高级模式

| 特性 | 简单模式 | 高级模式 |
|------|----------|----------|
| 注入位置 | System Prompt 后 | System Prompt 后 |
| PromptEntry 可见 | ❌ 界面隐藏 | ✅ 可见可编辑 |
| 文风生效 | ✅ | ✅ |

两种模式文风都会生效，区别仅在于用户能否看到/编辑 PromptEntry。

### RimTalk Prompt 结构

```
System Prompt           ← RimTalk 默认
     ↓
Style Context           ← 我们注入的
{{style_full}}
     ↓
Memory Context          ← ExpandMemory（如果安装）
     ↓
User Message
```

## 文件结构

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/StyleExpand.xml
│   └── ChineseSimplified/Keyed/StyleExpand.xml
├── 1.5/Assemblies/
│   └── RimTalk-StyleExpand.dll
├── 1.6/Assemblies/
│   └── RimTalk-StyleExpand.dll
├── Styles/
│   ├── Tsundere.txt
│   ├── Classical.txt
│   └── .cache/           # 自动生成的缓存
└── Source/
    ├── API/
    │   ├── RimTalkAPIIntegration.cs
    │   └── StyleVariableProvider.cs
    ├── UI/
    │   ├── SettingsWindow.cs
    │   └── Dialog_StylePreview.cs
    ├── VectorClient.cs
    ├── StyleRetriever.cs
    └── ...
```

## 项目进度

### v1.5（当前版本）

- [x] 语义切分算法（2026年最新RAG方案）
  - 基于句子embedding相似度动态确定切分点
  - 三种切分策略：递归、语义、混合
  - 断点百分位阈值可配置（默认95%）
  - 确保每个片段语义完整连贯
- [x] 切分参数优化
  - 新增：最小/最大片段长度、片段重叠
  - 默认参数针对文风复刻场景优化
- [x] 设置界面重构
  - 切分策略选择UI
  - 语义切分参数配置
  - 参数说明提示

#### 推荐参数配置（文风复刻场景）

| 参数 | 推荐值 | 说明 |
|------|--------|------|
| 切分策略 | 语义 | 确保场景/对话完整切分 |
| 断点阈值 | 95% | 高阈值=更大chunk=更完整场景 |
| 最小片段 | 120 | 单句太短无文风参考价值 |
| 目标片段 | 450 | 一个对话场景≈300-500字 |
| 最大片段 | 900 | 复杂场景也能完整保留 |
| 片段重叠 | 0 | 语义切分已保证连贯 |
| 检索数量 | 3 | 3个完整片段≈1200字文风素材 |
| 相似度阈值 | 0.55 | 过滤不相关，保证质量 |
| 采样数量 | 250 | 覆盖全文风格变化 |
| 大文件阈值 | 15000 | 约4000字中文开始采样 |

**核心目标：每个返回片段都是完整的、可学习的风格单元。**

### v1.4.4

- [x] 注入方式重构（参考 ExpandMemory）
  - 使用 RimTalk 官方 API 注册变量和 PromptEntry
  - 用户可在 RimTalk 界面中看到/编辑 "Style Context" PromptEntry
  - 支持开关、调整位置、自定义内容
  - 保留 Harmony Patch 作为后备方案
- [x] 简单模式支持
  - 添加 context 缓存机制
  - 简单模式下使用文风名称作为检索 query 回退
  - 确保两种模式都能正常注入文风
- [x] Scriban 变量
  - `{{style_name}}` - 当前文风名称
  - `{{style_prompt}}` - 文风描述
  - `{{style_chunks}}` - 检索到的示例片段
  - `{{style_full}}` - 完整文风 prompt

### v1.4.3

- [x] 设置界面优化
  - 修复大文件缓存导致设置界面卡顿（轻量级元数据读取）
  - 目标片段长度移至切分参数，改名"目标片段长度"
  - 切分参数顺序调整：基础参数 → 大文件采样（折叠）
  - 文风分析提示词模板可编辑
  - 重置模板按钮右对齐
- [x] 分块算法重构
  - 参考 LangChain RecursiveCharacterTextSplitter
  - 递归分割：段落 → 句末 → 分句 → 字符
  - 最小片段长度 100 字符，目标 400 字符
  - Overlap 50 字符保持上下文连贯
  - 小片段自动合并
- [x] 配置调整
  - 大文件阈值从 50,000 降至 20,000 字符
  - 推荐字数同步调整

### v1.4.2

- [x] 性能优化
  - 修复大缓存文件导致设置界面卡顿
  - 新增 `GetMetadata()` 轻量级读取
  - 新增 `ParseChunkCountOnly()` 快速统计

### v1.4.1

- [x] API 调用优化
  - 添加重试机制（最多3次 + 指数退避）
  - 修复 Ollama API 兼容性（prompt 字段）
  - 修复中文系统下 embedding 解析失败（CultureInfo）
- [x] 缓存优化
  - 添加 LRU 缓存淘汰机制（最大1000条）
  - 避免 embedding 缓存内存溢出
- [x] LLM Prompt 重构
  - 新 prompt 设计：不预设分析维度，让 LLM 自行判断
  - 分段采样：开头/中间/结尾各取一部分，共10%
  - 输出约500字的风格描述
- [x] 代码重构
  - RimTalk API 集成集中到 `RimTalkAPIIntegration.cs`
  - 移除冗余 UI 元素
- [x] 推荐模型更新
  - Ollama: `nomic-embed-text`

### v1.4

- [x] UI 重构
  - 重新设计模块顺序，优化用户体验
  - 添加模块功能说明
  - 高级设置可折叠
  - 简化切分按钮逻辑
  - 文风提示词编辑框放大 + 滚动条
  - 高级设置添加 Scriban 变量说明
- [x] 性能优化
  - 使用 LongEventHandler 后台执行切分和生成
  - 优化检索逻辑，只使用缓存的 embedding
  - 新增 `RetrieveWithScores` 方法避免重复计算
- [x] Bug 修复
  - 修复文风列表位置偏移问题
  - 修复缓存清除后状态不刷新
  - 修复预览窗口取消勾选变量不删除
  - 修复相似度阈值显示问题
  - 修复 RimTalk API 配置复用时模型名错误
- [x] RimTalk API 集成
  - LLM API 支持复用 RimTalk 配置
  - 自动适配 API 网关 URL
- [x] 代码清理
  - 删除未使用的代码和字段
  - 移除不必要的配置项
  - 移除 LLM 生成中的 Example Phrases 要求

### v1.3

- [x] 架构重构（借鉴 ExpandMemory 模式）
- [x] 独立预览窗口

### v1.2

- [x] 文风预览功能
- [x] 切分算法优化

### v1.1

- [x] 分批切分 + 中断后继续
- [x] 大文件采样策略
- [x] LLM 自动生成文风提示词
- [x] 文件变化自动重载

### v1.0

- [x] 向量检索功能
- [x] Prompt 注入
- [x] 文风管理（单选模式）
- [x] 配置界面
- [x] 中英双语支持
- [x] RimWorld 1.5 / 1.6 支持

### v1.5（计划中）

- [ ] 更多预置文风

## 编译

```bash
# 编译 1.5 版本
dotnet build -p:GameVersion=1.5

# 编译 1.6 版本
dotnet build -p:GameVersion=1.6
```

## 文档

- [开发文档](DEV_GUIDE.md) - 架构说明、开发计划

## 依赖

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## 许可证

基于 RimTalk 开发，遵循相同许可证。

## 作者

NUOQI_P