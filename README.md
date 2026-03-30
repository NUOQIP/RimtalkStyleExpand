# RimTalk StyleExpand

[English](README_EN.md) | 中文

**RimTalk 文风扩展模块 — 让对话呈现任意写作风格**

基于向量检索的文风格化 Mod。用户提供文风素材，Mod 自动分析并在 RimTalk 对话生成中注入风格特征。

## 核心功能

- **文风管理** — 一个 `.txt` 文件即一种文风，用户完全自定义
- **语义切分** — 基于 embedding 相似度的动态切分算法，确保每个片段语义完整
- **向量检索** — 根据当前角色特征自动检索最相关的文风片段
- **LLM 分析** — 可选自动分析文风素材，生成风格描述
- **RimTalk API 集成** — 自动注册 Scriban 变量，无需手动配置

## 技术架构

```
用户文风素材 (.txt)
       ↓
   语义切分器 ← Embedding API
       ↓
   向量索引缓存
       ↓
   检索引擎 ← 角色特征 query
       ↓
   Prompt 注入 → RimTalk System Prompt
```

### RimTalk 变量注册

| 变量 | 描述 |
|------|------|
| `{{style_name}}` | 当前文风名称 |
| `{{style_prompt}}` | LLM 生成的风格描述 |
| `{{style_chunks}}` | 检索到的示例片段 |
| `{{style_full}}` | 组合完整 prompt |

### PromptEntry 注入

自动在 RimTalk 预设中创建 "Style Context" 条目，位置在 System Prompt 之后：

```
System Prompt (RimTalk)
      ↓
Style Context (StyleExpand) ← {{style_full}}
      ↓
Memory Context (ExpandMemory)
      ↓
User Message
```

## 快速开始

```
1. 将文风素材放入 Styles/ 目录（.txt 文件）
2. 启动游戏 → Mod 设置 → StyleExpand
3. 配置 Embedding API（用于切分和检索）
4. 点击「扫描文风」→ 选择文风 →「切分文风」
5. 完成，RimTalk 生成的对话将呈现该文风
6. 推荐：配置 LLM API 后点击「生成描述」，效果更佳
```

> 💡 **推荐使用「生成文风描述」功能**：MOD 内置 LLM 分析功能，可自动分析文风素材并生成风格描述，比单纯使用检索片段效果更好。

### 文风素材建议

| 指标 | 推荐范围 | 说明 |
|------|----------|------|
| 中文素材 | 5,000 - 20,000 字 | 过少检索效果差，过多处理时间长 |
| 英文素材 | 3,000 - 15,000 词 | 同上 |

> 更大的文件可使用，但切分和 embedding 计算耗时增加。

## 配置要求

### 必需

- **RimTalk** Mod
- **Embedding API**（用于切分和检索）

### 可选

- **LLM API**（用于自动生成风格描述，可复用 RimTalk 配置）

### 支持的 API

- Ollama（默认）
- OpenAI Embedding API
- 其他 OpenAI 兼容 API

推荐模型：`nomic-embed-text`、`mxbai-embed-large`、`text-embedding-3-small`

## 切分参数

语义切分算法的核心参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| Breakpoint Threshold | 80% | 断点阈值，越高片段越长 |
| Min Chunk Length | 100 | 最小片段长度 |
| Target Chunk Length | 400 | 目标片段长度 |
| Max Chunk Length | 800 | 最大片段长度 |

## 项目结构

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/
│   └── ChineseSimplified/Keyed/
├── 1.5/Assemblies/
├── 1.6/Assemblies/
├── Styles/                  # 用户文风素材目录
│   └── .cache/              # 自动生成的缓存
└── Source/
    ├── API/
    │   ├── RimTalkAPIIntegration.cs   # RimTalk API 集成
    │   └── StyleVariableProvider.cs   # 变量提供器
    ├── SemanticChunker.cs             # 语义切分算法
    ├── StyleRetriever.cs              # 向量检索
    ├── VectorClient.cs                # Embedding API 客户端
    ├── LLMClient.cs                   # LLM API 客户端
    ├── EmbeddingCache.cs              # 缓存管理
    ├── PromptBuilder.cs               # Prompt 构建
    └── UI/
        ├── SettingsWindow.cs
        └── Dialog_StylePreview.cs
```

## 编译

```bash
dotnet build -p:GameVersion=1.5 -c Release
dotnet build -p:GameVersion=1.6 -c Release
```

## 依赖

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## 版本历史

### v1.0.1

- 新增「获取模型列表」功能，一键获取 Ollama/OpenAI 可用模型
- 修复 Ollama API 兼容性问题（新版 /api/embed 端点）
- 修复配置文件共享违规问题
- 改进新玩家体验：API 状态检测、自动选中已切分文风
- 优化帮助文档，添加 Ollama 启动说明

### v1.0

- 语义切分算法
- 向量检索注入
- RimTalk API 集成
- LLM 文风分析
- 完整 UI 设置界面
- 中英双语支持
- RimWorld 1.5 / 1.6 支持

## 许可证

基于 RimTalk 开发，遵循相同许可证。

## 作者

NUOQI_P

## 反馈

GitHub: https://github.com/NUOQIP/RimtalkStyleExpand