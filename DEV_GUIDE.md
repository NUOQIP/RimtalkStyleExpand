# RimTalk StyleExpand 开发文档

**仓库地址：** https://github.com/NUOQIP/RimtalkStyleExpand

---

## 一、项目概述

让 RimTalk 生成的对话模仿指定文风（古风、傲娇、鲁迅等）。

**核心原理：**
```
玩家放置 txt 文风素材
    ↓
文本切分成片段 + 计算向量 Embedding
    ↓
角色对话时，根据特征检索相似片段
    ↓
注入文风提示词 + 检索片段到 RimTalk
    ↓
AI 模仿文风生成对话
```

---

## 二、当前版本（v1.2）状态

**已完成功能：**

| 功能 | 文件 | 说明 |
|------|------|------|
| 向量检索 | `VectorClient.cs` | 同步HTTP调用向量API，余弦相似度计算 |
| 文本切分 | `StyleRetriever.cs` | 段落+句子二级切分，语义边界检测 |
| Embedding缓存 | `EmbeddingCache.cs` | JSON格式，保存到 `Styles/.cache/`，支持进度保存 |
| 文风管理 | `StyleExpandSettings.cs` | 单选模式，扫描txt文件 |
| Prompt构建 | `PromptBuilder.cs` | 基础prompt + 文风描述 + 检索片段 |
| Scriban变量 | `StyleExpandMod.cs` | 注册到RimTalk API |
| 配置界面 | `SettingsWindow.cs` | 完整UI，状态提示，帮助窗口 |
| 多语言 | `Languages/` | 中英双语 |
| Harmony补丁 | `RimTalkPatches.cs` | 反射方式注入Prompt |
| 分批切分 | `StyleRetriever.cs` | 支持中断后继续，进度保存 |
| 大文件采样 | `StyleRetriever.cs` | 超过阈值自动采样代表性片段 |
| LLM生成描述 | `LLMClient.cs` | 自动分析文风生成描述 |
| 批量切分 | `StyleRetriever.cs` | 一键切分所有文风 |
| 文件监听 | `StyleWatcher.cs` | FileSystemWatcher自动重载 |
| 文风预览 | `SettingsWindow.cs` | 变量选择器，手动/模板预览 |
| 变量获取 | `VariableHelper.cs` | 从RimTalk获取可用变量列表 |

**未实现（v1.3计划）：**
- 更多预置文风
- 性能优化

---

## 三、代码架构

```
StyleExpand/
├── About/About.xml              # Mod元数据
├── Languages/
│   ├── English/Keyed/StyleExpand.xml
│   └── ChineseSimplified/Keyed/StyleExpand.xml
├── 1.5/Assemblies/              # RimWorld 1.5 编译输出
├── 1.6/Assemblies/              # RimWorld 1.6 编译输出
├── Styles/
│   ├── *.txt                    # 文风文件（一个txt=一种文风）
│   └── .cache/*.json            # 切分+embedding缓存（含进度）
└── Source/
    ├── StyleExpandMod.cs        # Mod入口，注册Scriban变量
    ├── StyleExpandSettings.cs   # 设置存储
    ├── StyleConfig.cs           # 数据结构定义
    ├── StyleRetriever.cs        # 扫描、切分、检索、批量处理
    ├── EmbeddingCache.cs        # JSON缓存读写，进度保存
    ├── VectorClient.cs          # 向量API调用
    ├── LLMClient.cs             # LLM API调用，文风描述生成
    ├── VariableHelper.cs        # 从RimTalk获取可用变量
    ├── PromptBuilder.cs         # Prompt构建
    ├── StyleWatcher.cs          # 文件变化监听
    ├── Logger.cs                # 日志工具
    ├── Patches/
    │   └── RimTalkPatches.cs    # Harmony补丁（反射）
    └── UI/
        └── SettingsWindow.cs    # 设置界面 + 帮助窗口
```

---

## 四、关键代码说明

### 4.1 文风文件结构
```
Styles/
├── Tsundere.txt      ← 文风名 = 文件名
├── Classical.txt
└── Satirical.txt
```

### 4.2 Scriban变量注册
```csharp
// StyleExpandMod.cs:35-77
RimTalkPromptAPI.RegisterContextVariable("RimTalk.StyleExpand", "style_name", ...);
RimTalkPromptAPI.RegisterContextVariable("RimTalk.StyleExpand", "style_base_prompt", ...);
RimTalkPromptAPI.RegisterContextVariable("RimTalk.StyleExpand", "style_prompt", ...);
RimTalkPromptAPI.RegisterContextVariable("RimTalk.StyleExpand", "style_chunks", ...);
RimTalkPromptAPI.RegisterContextVariable("RimTalk.StyleExpand", "style_full", ...);
```

### 4.3 切分逻辑
```csharp
// StyleRetriever.cs
SplitIntoChunks(text, maxLength)
  → 先按 \n\n 分段落
  → 再按 。！？.!? 分句子
  → 组合成不超过maxLength的片段
```

### 4.4 检索流程
```csharp
// StyleRetriever.Retrieve()
1. 渲染Query模板（替换{{ pawn.name }}等变量）
2. 调用VectorClient.GetEmbeddingSync(query)
3. 遍历所有片段，计算余弦相似度
4. 返回TopK个最相似片段
```

### 4.5 Harmony补丁（反射方式）
```csharp
// RimTalkPatches.cs
// 由于没有RimTalk.dll引用，使用反射获取类型
var apiType = Type.GetType("RimTalk.API.RimTalkPromptAPI, RimTalk");
```

---

## 五、注入给LLM的Prompt结构

```
[Style Instruction]
Please imitate the following writing style (Tsundere) when generating dialogue:
                    ↑ 基础prompt（可编辑，默认值在RetrievalConfig.BasePromptTemplate）

## Tsundere Style
Style Features:...
                    ↑ 文风prompt（可编辑，在StyleConfig.Prompt）

### Style Examples:
- 哼，才不是...
- 笨蛋！谁要...
                    ↑ 检索切片（来自txt文件，向量检索）
```

---

## 六、编译方式

```bash
cd StyleExpand/Source

# 编译 1.5 版本
dotnet build -p:GameVersion=1.5

# 编译 1.6 版本
dotnet build -p:GameVersion=1.6
```

**输出位置：**
- `1.5/Assemblies/RimTalk-StyleExpand.dll`
- `1.6/Assemblies/RimTalk-StyleExpand.dll`

---

## 七、v1.3 开发计划

按优先级排序：

| 优先级 | 功能 | 说明 |
|--------|------|------|
| P1 | 更多预置文风 | 内置常见文风模板 |
| P2 | 性能优化 | 缓存优化、并发请求 |
| P3 | 根据社区反馈迭代 | - |

---

## 八、注意事项

### 8.1 编译警告
```
warning MSB3245: 未能解析此引用。未能找到程序集"RimTalk"
warning MSB3245: 未能解析此引用。未能找到程序集"Scriban"
```
**这是正常的**，因为RimTalk.dll和Scriban.dll在游戏运行时由RimTalk Mod提供。

### 8.2 多语言文件
修改UI文字时，需同步更新：
- `Languages/English/Keyed/StyleExpand.xml`
- `Languages/ChineseSimplified/Keyed/StyleExpand.xml`

### 8.3 推荐文风文件大小
- 中文：5,000 - 50,000 字
- 英文：3,000 - 30,000 词

### 8.4 向量API配置
默认：`http://localhost:11434/api/embeddings`（Ollama）
推荐模型：`bge-small-zh`

---

## 九、依赖关系

```
RimTalk StyleExpand
    ├── Harmony 2.x（补丁框架）
    ├── RimTalk（必须安装）
    │   └── RimTalkPromptAPI（注册变量）
    ├── Newtonsoft.Json（JSON解析）
    └── Scriban（模板引擎，RimTalk已有）
```

---

## 十、Git提交规范

```
feat: 新功能
fix: 修复bug
docs: 文档更新
refactor: 重构
chore: 构建/配置

示例：
feat: Add batch chunking with resume support
fix: Fix embedding cache parsing error
docs: Update v1.1 roadmap
```

---

## 十一、快速定位问题

| 问题 | 检查文件 |
|------|---------|
| API连接失败 | `VectorClient.cs`, UI中的API配置 |
| 切分结果不对 | `StyleRetriever.SplitIntoChunks()` |
| 检索不到片段 | `StyleRetriever.Retrieve()`, 缓存文件 |
| Prompt没注入 | `RimTalkPatches.cs`, `PromptBuilder.cs` |
| UI显示问题 | `SettingsWindow.cs`, 多语言xml |
| 设置没保存 | `StyleExpandSettings.cs` |

---

**最后更新：** 2026-03-20
**当前版本：** v1.2
**下一版本：** v1.3