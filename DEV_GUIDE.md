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

## 二、当前版本（v1.4.1）状态

**已完成功能：**

| 功能 | 文件 | 说明 |
|------|------|------|
| API 集成 | `API/RimTalkAPIIntegration.cs` | 自动注册变量到 RimTalk |
| 变量提供 | `API/StyleVariableProvider.cs` | `{{style_name}}` 等变量实现 |
| 向量检索 | `VectorClient.cs` | 单例模式，异步 API，LRU缓存，重试机制 |
| 文本切分 | `StyleRetriever.cs` | 段落+句子二级切分，语义边界检测 |
| Embedding缓存 | `EmbeddingCache.cs` | JSON格式，保存到 `Styles/.cache/` |
| 文风管理 | `StyleExpandSettings.cs` | 单选模式，扫描txt文件 |
| Prompt构建 | `PromptBuilder.cs` | 基础prompt + 文风描述 + 检索片段 |
| 配置界面 | `UI/SettingsWindow.cs` | 完整UI，状态提示 |
| 预览窗口 | `UI/Dialog_StylePreview.cs` | 独立预览对话框 |
| 多语言 | `Languages/` | 中英双语 |
| Harmony补丁 | `Patches/RimTalkPatches.cs` | 反射方式注入Prompt |
| 分批切分 | `StyleRetriever.cs` | 支持中断后继续 |
| 大文件采样 | `StyleRetriever.cs` | 超过阈值自动采样 |
| LLM生成描述 | `LLMClient.cs` | 自动分析文风，分段采样，新prompt设计 |
| 文件监听 | `StyleWatcher.cs` | 自动检测文件变化 |

**未实现（v1.5计划）：**
- 更多预置文风

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
│   ├── *.txt                    # 文风文件
│   └── .cache/*.json            # 切分+embedding缓存
└── Source/
    ├── StyleExpandMod.cs        # Mod入口
    ├── StyleExpandSettings.cs   # 设置存储
    ├── StyleConfig.cs           # 数据结构定义
    ├── StyleRetriever.cs        # 扫描、切分、检索
    ├── EmbeddingCache.cs        # JSON缓存读写
    ├── VectorClient.cs          # 向量API（单例+异步）
    ├── LLMClient.cs             # LLM API（支持复用RimTalk配置）
    ├── VariableHelper.cs        # RimTalk变量获取
    ├── PromptBuilder.cs         # Prompt构建
    ├── StyleWatcher.cs          # 文件变化监听
    ├── Logger.cs                # 日志工具
    ├── API/
    │   ├── RimTalkAPIIntegration.cs   # API集成入口
    │   └── StyleVariableProvider.cs   # 变量提供器
    ├── Patches/
    │   └── RimTalkPatches.cs    # Harmony补丁
    └── UI/
        ├── SettingsWindow.cs    # 设置界面
        └── Dialog_StylePreview.cs # 预览窗口
```

---

## 四、关键代码说明

### 4.1 API 变量自动注册

```csharp
// API/RimTalkAPIIntegration.cs
[StaticConstructorOnStartup]
public static class RimTalkAPIIntegration
{
    static RimTalkAPIIntegration()
    {
        LongEventHandler.ExecuteWhenFinished(Initialize);
    }
    
    private static void RegisterVariables()
    {
        // 注册 Context 变量
        RegisterContextVariable("style_base_prompt", GetBasePrompt, "Base style instruction");
        RegisterContextVariable("style_name", GetStyleName, "Current style name");
        RegisterContextVariable("style_prompt", GetStylePrompt, "Style description");
        RegisterContextVariable("style_chunks", GetStyleChunks, "Retrieved examples");
        RegisterContextVariable("style_full", GetStyleFull, "Complete style prompt");
    }
}
```

### 4.2 LLM API 复用 RimTalk 配置

```csharp
// LLMClient.cs
private static (string url, string apiKey, string model) GetRimTalkActiveConfig(string overrideModel)
{
    // 使用 GetActiveConfig() 获取 RimTalk 当前活动配置
    var activeConfig = getActiveConfigMethod.Invoke(settingsInstance, null);
    
    // 获取 ApiConfig 字段
    string apiKey = apiKeyField?.GetValue(activeConfig) as string ?? "";
    string baseUrl = baseUrlField?.GetValue(activeConfig) as string ?? "";
    
    // 自动补充 API 端点路径
    if (!baseUrl.Contains("/v1/") && !baseUrl.Contains("/api/"))
    {
        baseUrl = baseUrl.TrimEnd('/') + "/v1/chat/completions";
    }
    
    return (baseUrl, apiKey, model);
}
```

### 4.3 VectorClient 单例模式

```csharp
// VectorClient.cs
public class VectorClient
{
    public static VectorClient Instance { get; }
    
    // 异步 API
    public async Task<float[]> GetEmbeddingAsync(string text, VectorApiConfig config);
    
    // 同步 API（向后兼容）
    public static float[] GetEmbeddingSync(string text, VectorApiConfig config);
    
    // 重试机制：最多3次 + 指数退避
    private const int MAX_RETRIES = 3;
    
    // LRU 缓存：最大1000条
    private const int MAX_CACHE_SIZE = 1000;
    
    // Ollama 兼容：自动使用 prompt 字段
    var isOllama = config.Url.Contains(":11434");
    var inputField = isOllama ? "prompt" : "input";
}
```

### 4.4 切分逻辑

```csharp
// StyleRetriever.cs
SplitIntoChunks(text, maxLength)
  → 先按 \n\n 分段落
  → 再按 。！？.!? 分句子
  → 组合成不超过maxLength的片段
```

### 4.5 检索流程

```csharp
// StyleRetriever.Retrieve()
1. 渲染Query模板（替换{{ pawn.name }}等变量）
2. 调用VectorClient.GetEmbeddingSync(query)
3. 遍历所有片段，计算余弦相似度（仅使用缓存的embedding）
4. 返回TopK个最相似片段
```

---

## 五、注入给LLM的Prompt结构

```
[Style Instruction]
Please imitate the following writing style (Tsundere) when generating dialogue:
                    ↑ {{style_base_prompt}}

## Tsundere Style
Style Features:...
                    ↑ {{style_prompt}}

### Style Examples:
- 哼，才不是...
- 笨蛋！谁要...
                    ↑ {{style_chunks}}
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

## 七、v1.5 开发计划

按优先级排序：

| 优先级 | 功能 | 说明 |
|--------|------|------|
| P1 | 更多预置文风 | 内置常见文风模板 |
| P2 | 根据社区反馈迭代 | - |

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
推荐模型：`nomic-embed-text`

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
feat: Add style_base_prompt variable
fix: Fix style list position offset issue
refactor: Remove unused code and optimize performance
```

---

## 十一、快速定位问题

| 问题 | 检查文件 |
|------|---------|
| API连接失败 | `VectorClient.cs`, UI中的API配置 |
| 变量未注册 | `API/RimTalkAPIIntegration.cs` |
| 切分结果不对 | `StyleRetriever.SplitIntoChunks()` |
| 检索不到片段 | `StyleRetriever.Retrieve()`, 缓存文件 |
| Prompt没注入 | `Patches/RimTalkPatches.cs`, `PromptBuilder.cs` |
| UI显示问题 | `UI/SettingsWindow.cs`, 多语言xml |
| 设置没保存 | `StyleExpandSettings.cs` |

---

**最后更新：** 2026-03-23
**当前版本：** v1.4.1
**下一版本：** v1.5