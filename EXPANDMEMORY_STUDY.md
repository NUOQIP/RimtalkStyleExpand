# RimTalk-ExpandMemory 学习报告

**分析日期：** 2026-03-23  
**项目路径：** `RimTalk-ExpandMemory-main/Source`  
**项目规模：** 91 个 .cs 文件，约 25,000+ 行代码

---

## 一、API 调用方式

### 1.1 RimWorld API 调用

**核心命名空间：**
```csharp
using Verse;           // RimWorld 核心工具类
using UnityEngine;     // Unity 引擎 API
using HarmonyLib;      // Harmony 补丁框架
```

**WorldComponent - 世界级组件：**
```csharp
// 文件: MemoryManager.cs
public class MemoryManager : WorldComponent
{
    public override void WorldComponentTick()
    {
        base.WorldComponentTick();
        
        // 使用 RimWorld 时间 API
        if (Find.TickManager.TicksGame - lastDecayTick >= DecayInterval)
        {
            DecayAllMemories();
            lastDecayTick = Find.TickManager.TicksGame;
        }
    }
}
```

**ThingComp - 实体级组件：**
```csharp
// 文件: FourLayerMemoryComp.cs
public class FourLayerMemoryComp : ThingComp
{
    public override void PostExposeData()
    {
        base.PostExposeData();
        // Scribe API - 存档序列化
        Scribe_Collections.Look(ref activeMemories, "activeMemories", LookMode.Deep);
    }
}
```

**Pawn 数据访问：**
```csharp
// 文件: CommonKnowledgeLibrary.cs
private string BuildCompletePawnInfoText(Verse.Pawn pawn)
{
    if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
    if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
    if (pawn.story?.traits != null)
    if (pawn.skills != null)
    if (pawn.health != null)
    if (pawn.relations != null)
}
```

### 1.2 RimTalk API 集成（反射方式）

**获取程序集和类型：**
```csharp
// 文件: RimTalkAPIIntegration.cs
[StaticConstructorOnStartup]
public static class RimTalkAPIIntegration
{
    private static Assembly _rimTalkAssembly;
    private static Type _promptAPIType;
    
    private static bool DetectNewAPI()
    {
        _rimTalkAssembly = GetRimTalkAssembly();
        _promptAPIType = _rimTalkAssembly.GetType("RimTalk.API.RimTalkPromptAPI");
    }
}
```

**注册 Mustache 变量：**
```csharp
private static void RegisterVariables()
{
    var registerPawnVar = _promptAPIType.GetMethod("RegisterPawnVariable");
    
    Func<Pawn, string> memoryProvider = MemoryVariableProvider.GetPawnMemory;
    registerPawnVar.Invoke(null, new object[]
    {
        MOD_ID,
        "memory",
        memoryProvider,
        "Character's personal memories",
        100
    });
}
```

**读取 RimTalk 配置：**
```csharp
// 文件: IndependentAISummarizer.cs
private static bool TryLoadFromRimTalk()
{
    Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "RimTalk");
    
    Type type = assembly.GetType("RimTalk.Settings");
    MethodInfo method = type.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
    object obj = method.Invoke(null, null);
    
    FieldInfo field = type.GetField("ApiKey");
    apiKey = field.GetValue(obj) as string;
}
```

### 1.3 Harmony 补丁机制

**Patch 类型统计：**

| Patch 文件 | 类型 | 目标方法 |
|-----------|------|---------|
| `InjectMemoryCompPatch.cs` | Postfix | `ThingWithComps.InitializeComps` |
| `RimTalkConversationCapturePatch.cs` | Postfix | `PlayLogEntry_RimTalkInteraction` 构造函数 |
| `Patch_GenerateAndProcessTalkAsync.cs` | Prefix | `TalkService.GenerateAndProcessTalkAsync` |

**动态注入组件：**
```csharp
// 文件: InjectMemoryCompPatch.cs
[HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
public static class InjectMemoryCompPatch
{
    private static readonly FieldInfo allCompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
    
    [HarmonyPostfix]
    public static void Postfix(ThingWithComps __instance)
    {
        if (__instance is Pawn pawn && pawn.RaceProps?.Humanlike == true)
        {
            var comp = new PawnMemoryComp();
            comp.parent = pawn;
            var compsList = allCompsField?.GetValue(pawn) as List<ThingComp>;
            compsList.Add(comp);
            comp.Initialize(new CompProperties_PawnMemory());
        }
    }
}
```

**动态查找目标方法：**
```csharp
// 文件: Patch_GenerateAndProcessTalkAsync.cs
[HarmonyPatch]
public static class Patch_GenerateAndProcessTalkAsync
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "RimTalk");
        var talkServiceType = rimTalkAssembly.GetType("RimTalk.Service.TalkService");
        return talkServiceType.GetMethod("GenerateAndProcessTalkAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
    }
    
    [HarmonyPrefix]
    public static void Prefix(object talkRequest)
    {
        // 向量增强注入逻辑
    }
}
```

---

## 二、性能优化策略

### 2.1 缓存机制

#### (1) O(1) LRU 对话缓存

```csharp
// 文件: ConversationCache.cs
public class ConversationCache : IExposable
{
    // LinkedList + Dictionary 组合实现 O(1) LRU
    private LinkedList<CacheEntry> lruList = new LinkedList<CacheEntry>();
    private Dictionary<string, LinkedListNode<CacheEntry>> cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
    
    public string TryGet(string cacheKey)
    {
        if (cacheMap.TryGetValue(cacheKey, out var node))
        {
            entry.useCount++;
            totalHits++;
            
            // 移动到链表头部 - O(1)
            lruList.Remove(node);
            lruList.AddFirst(node);
            return entry.dialogue;
        }
        totalMisses++;
        return null;
    }
    
    public void Add(string cacheKey, string dialogue)
    {
        var newNode = new LinkedListNode<CacheEntry>(newEntry);
        lruList.AddFirst(newNode);
        cacheMap[cacheKey] = newNode;
        
        if (cacheMap.Count > MaxCacheSize)
        {
            EvictLRU_O1(); // 移除尾部
        }
    }
}
```

#### (2) 提示词缓存

```csharp
// 文件: PromptCache.cs
public class CacheEntry : IExposable
{
    public string memoryPrompt;
    public string knowledgePrompt;
    public string fullPrompt;
    public int pawnMemoryCount;
    public int knowledgeCount;
    
    // 智能失效检测
    public bool IsValid(int currentMemoryCount, int currentKnowledgeCount, int currentTick, int expireTicks)
    {
        if (Math.Abs(pawnMemoryCount - currentMemoryCount) > 5) return false;
        if (Math.Abs(knowledgeCount - currentKnowledgeCount) > 10) return false;
        if (currentTick - timestamp > expireTicks) return false;
        return true;
    }
}
```

#### (3) 向量数据库缓存

```csharp
// 文件: VectorService.cs
private Dictionary<string, float[]> _loreVectors = new Dictionary<string, float[]>();
private Dictionary<string, string> _contentHashes = new Dictionary<string, string>();

// 增量同步（只处理变化的内容）
public void SyncKnowledgeLibrary(CommonKnowledgeLibrary library)
{
    foreach (var entry in entriesToProcess)
    {
        string currentHash = ComputeHash(entry.content);
        if (!_contentHashes.ContainsKey(entry.id) || _contentHashes[entry.id] != currentHash)
        {
            entriesToUpdate.Add(entry); // 只更新变化的
        }
    }
}
```

### 2.2 异步处理

**异步 AI 调用：**
```csharp
// 文件: IndependentAISummarizer.cs
public static string SummarizeMemories(Pawn pawn, List<MemoryEntry> memories, string promptTemplate)
{
    Task.Run(async () =>
    {
        try
        {
            string result = await CallAIAsync(prompt);
            if (result != null)
            {
                lock (completedSummaries)
                {
                    completedSummaries[cacheKey] = result;
                }
                
                // 回调队列（主线程安全）
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() => cb(result));
                }
            }
        }
        finally
        {
            lock (pendingSummaries)
            {
                pendingSummaries.Remove(cacheKey);
            }
        }
    });
    
    return null; // 返回 null 表示异步处理中
}

// 主线程处理回调（每 tick 处理少量）
public static void ProcessPendingCallbacks(int maxPerTick = 5)
{
    int processed = 0;
    lock (mainThreadActions)
    {
        while (mainThreadActions.Count > 0 && processed < maxPerTick)
        {
            mainThreadActions.Dequeue()?.Invoke();
            processed++;
        }
    }
}
```

**异步向量搜索：**
```csharp
// 文件: VectorService.cs
public async Task<List<(string id, float similarity)>> FindBestLoreIdsAsync(
    string userMessage, int topK = 5, float threshold = 0.7f)
{
    float[] queryVector = await GetEmbeddingAsync(userMessage).ConfigureAwait(false);
    
    foreach (var kvp in _loreVectors)
    {
        float similarity = CosineSimilarity(queryVector, kvp.Value);
        if (similarity >= threshold)
            similarities.Add((kvp.Key, similarity));
    }
    
    return similarities.OrderByDescending(s => s.similarity).Take(topK).ToList();
}
```

### 2.3 内存管理

**冷启动缓冲：**
```csharp
// 文件: MemoryManager.cs
private int sessionStartTick = -1;
private const int COLD_START_DELAY = 200; // 启动后延迟200 ticks

public override void WorldComponentTick()
{
    if (sessionStartTick == -1) sessionStartTick = Find.TickManager.TicksGame;
    if (Find.TickManager.TicksGame - sessionStartTick < COLD_START_DELAY) return;
}
```

**总结队列（分批处理）：**
```csharp
// 文件: MemoryManager.cs
private Queue<Pawn> summarizationQueue = new Queue<Pawn>();
private int nextSummarizationTick = 0;
private const int SUMMARIZATION_DELAY_TICKS = 900; // 15秒间隔

private void ProcessSummarizationQueue()
{
    if (summarizationQueue.Count == 0) return;
    if (currentTick < nextSummarizationTick) return;
    
    Pawn pawn = summarizationQueue.Dequeue();
    
    if (summarizationQueue.Count > 0)
        nextSummarizationTick = currentTick + SUMMARIZATION_DELAY_TICKS;
}
```

**AI 缓存大小限制：**
```csharp
// 文件: IndependentAISummarizer.cs
private const int MAX_CACHE_SIZE = 100;
private const int CACHE_CLEANUP_THRESHOLD = 120;

if (completedSummaries.Count >= CACHE_CLEANUP_THRESHOLD)
{
    var toRemove = completedSummaries.Keys
        .OrderBy(k => k, StringComparer.Ordinal)
        .Take(MAX_CACHE_SIZE / 2)
        .ToList();
    
    foreach (var key in toRemove)
        completedSummaries.Remove(key);
}
```

---

## 三、代码优雅性

### 3.1 设计模式使用

#### (1) 单例模式
```csharp
// 文件: VectorService.cs
public static VectorService Instance
{
    get
    {
        if (_instance == null)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                    _instance = new VectorService();
            }
        }
        return _instance;
    }
}
```

#### (2) 策略模式
```csharp
// 文件: SceneAnalyzer.cs
public static DynamicWeights GetDynamicWeights(SceneType scene, float confidence = 1.0f)
{
    var weights = new DynamicWeights();
    
    switch (scene)
    {
        case SceneType.Combat:
            weights.TimeDecay = 0.8f;
            weights.RecencyWindow = 15000;
            break;
        case SceneType.Social:
            weights.TimeDecay = 0.05f;
            weights.RecencyWindow = 1800000;
            break;
    }
    return weights;
}
```

#### (3) 观察者模式
```csharp
// 文件: IndependentAISummarizer.cs
private static readonly Dictionary<string, List<Action<string>>> callbackMap = new Dictionary<string, List<Action<string>>>();

public static void RegisterCallback(string cacheKey, Action<string> callback)
{
    lock (callbackMap)
    {
        if (!callbackMap.TryGetValue(cacheKey, out var callbacks))
        {
            callbacks = new List<Action<string>>();
            callbackMap[cacheKey] = callbacks;
        }
        callbacks.Add(callback);
    }
}
```

#### (4) 模板方法模式
```csharp
// 文件: UnifiedMemoryInjector.cs
public static string Inject(Pawn pawn, string dialogueContext)
{
    // Step 1: 采集 ABM
    var abmList = ABMCollector.Collect(pawn, maxABMRounds);
    
    // Step 2: 计算剩余配额
    int remainingQuota = maxTotalMemories - abmList.Count;
    
    // Step 3: 采集 ELS/CLPA
    var elsList = ELSCollector.Collect(pawn, dialogueContext, remainingQuota);
    
    // Step 4: 合并记忆列表
    var allMemories = new List<MemoryEntry>();
    allMemories.AddRange(abmList);
    allMemories.AddRange(elsList);
    
    // Step 5: 统一编号格式化
    return MemoryFormatter.Format(allMemories, startIndex: 1);
}
```

### 3.2 代码组织结构

```
Source/
├── RimTalkMod.cs                    # 模组入口
├── RimTalkSettings.cs               # 设置定义
├── Settings/
│   └── SettingsUIDrawers.cs         # UI 绘制辅助
├── API/
│   ├── RimTalkAPIIntegration.cs     # RimTalk API 集成
│   ├── MemoryVariableProvider.cs    # 记忆变量提供者
│   ├── KnowledgeVariableProvider.cs # 知识变量提供者
│   └── MustacheVariableHelper.cs    # 模板变量辅助
├── Patches/
│   ├── InjectMemoryCompPatch.cs     # 组件注入 Patch
│   └── ...                          # 其他 Patch
├── Memory/
│   ├── MemoryManager.cs             # 记忆管理器
│   ├── FourLayerMemoryComp.cs       # 四层记忆组件
│   ├── MemoryTypes.cs               # 类型定义
│   ├── ConversationCache.cs         # 对话缓存
│   ├── PromptCache.cs               # 提示词缓存
│   ├── VectorDB/
│   │   ├── VectorService.cs         # 向量服务
│   │   └── InMemoryVectorStore.cs   # 内存向量存储
│   ├── AI/
│   │   ├── AIRequestManager.cs      # AI 请求管理
│   │   └── IndependentAISummarizer.cs # AI 总结器
│   ├── Injection/
│   │   ├── UnifiedMemoryInjector.cs # 统一注入调度
│   │   ├── ABMCollector.cs          # ABM 采集器
│   │   └── ELSCollector.cs          # ELS 采集器
│   ├── UI/
│   │   ├── MainTabWindow_Memory.cs  # 主窗口
│   │   └── Dialog_*.cs              # 各种对话框
│   └── Monitoring/
│       └── PerformanceMonitor.cs    # 性能监控
```

### 3.3 错误处理方式

**Patch 异常捕获：**
```csharp
// 文件: InjectMemoryCompPatch.cs
[HarmonyPostfix]
public static void Postfix(ThingWithComps __instance)
{
    try
    {
        // 核心逻辑...
    }
    catch (Exception ex)
    {
        Log.Error($"[RimTalk Memory] Failed to inject PawnMemoryComp: {ex.Message}\n{ex.StackTrace}");
    }
}
```

**重试机制：**
```csharp
// 文件: IndependentAISummarizer.cs
private static async Task<string> CallAIAsync(string prompt)
{
    const int MAX_RETRIES = 3;
    
    for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
    {
        try
        {
            // API 调用...
        }
        catch (WebException ex)
        {
            bool shouldRetry = false;
            
            if (errorResponse.StatusCode == HttpStatusCode.ServiceUnavailable ||
                errorResponse.StatusCode == (HttpStatusCode)429)
            {
                shouldRetry = true;
            }
            
            if (attempt >= MAX_RETRIES || !shouldRetry)
            {
                Log.Error($"[AI Summarizer] Failed after {attempt} attempts.");
                return null;
            }
            
            await Task.Delay(RETRY_DELAY_MS * attempt);
        }
    }
}
```

**向后兼容性处理：**
```csharp
// 文件: BackCompatibilityFix.cs
[StaticConstructorOnStartup]
public static class BackCompatibilityFix
{
    static BackCompatibilityFix()
    {
        ForceInitialize();
    }
    
    public static void ForceInitialize()
    {
        try
        {
            var memoryManagerType = typeof(MemoryManager);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(memoryManagerType.TypeHandle);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimTalk BackCompat] Initialization failed: {ex.Message}");
        }
    }
}
```

---

## 四、用户体验设计

### 4.1 UI 设计

**Partial Class 组织大型 UI：**
```csharp
// 文件: MainTabWindow_Memory.cs
/// 文件结构：
/// - MainTabWindow_Memory.cs (主文件 - 字段定义和入口)
/// - MainTabWindow_Memory_TopBar.cs (TopBar 绘制)
/// - MainTabWindow_Memory_Controls.cs (控制面板)
/// - MainTabWindow_Memory_Timeline.cs (时间线绘制)
/// - MainTabWindow_Memory_Actions.cs (批量操作)
/// - MainTabWindow_Memory_ImportExport.cs (导入导出)

public partial class MainTabWindow_Memory : MainTabWindow
{
    private List<MemoryEntry> cachedFilteredMemories;
    private bool filtersDirty = true;
    
    public void InvalidateCache()
    {
        lastMemoryCount = -1;
        filtersDirty = true;
    }
}
```

**预设卡片设计：**
```csharp
// 文件: RimTalkSettings.cs
private void DrawPresetCard(Rect rect, string title, int memoryCount, int knowledgeCount, int tokenEstimate)
{
    Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.18f, 0.6f));
    Widgets.DrawHighlightIfMouseover(rect);
    
    if (Widgets.ButtonInvisible(rect))
    {
        maxInjectedMemories = memoryCount;
        maxInjectedKnowledge = knowledgeCount;
        Messages.Message($"已应用 {title} 预设", MessageTypeDefOf.PositiveEvent);
    }
}
```

### 4.2 反馈机制

**消息反馈：**
```csharp
// 文件: MemoryManager.cs
Messages.Message(
    $"{pawn.LabelShort}: {scmCount}条短期记忆已总结",
    MessageTypeDefOf.TaskCompletion,
    false
);

Messages.Message("所有殖民者手动总结完成", MessageTypeDefOf.PositiveEvent, false);
```

**进度提示：**
```csharp
// 文件: VectorService.cs
LongEventHandler.ExecuteWhenFinished(() =>
{
    Messages.Message($"正在更新向量库... ({entriesToUpdate.Count} 条新增)", 
        MessageTypeDefOf.NeutralEvent, false);
});

LongEventHandler.ExecuteWhenFinished(() =>
{
    Messages.Message($"向量库已更新 ({syncedCount} 条)", 
        MessageTypeDefOf.PositiveEvent, false);
});
```

**开发模式日志：**
```csharp
if (Prefs.DevMode)
{
    Log.Message($"[Memory Injection] Scene: {SceneAnalyzer.GetSceneDisplayName(sceneType)}");
}
```

### 4.3 配置项设计

```csharp
// 文件: RimTalkSettings.cs
public class RimTalkMemoryPatchSettings : ModSettings
{
    // 记忆容量配置
    public int maxActiveMemories = 6;
    public int maxSituationalMemories = 20;
    public int maxEventLogMemories = 50;
    
    // 衰减速率
    public float scmDecayRate = 0.01f;
    public float elsDecayRate = 0.005f;
    
    // AI 配置
    public bool useRimTalkAIConfig = true;
    public string independentApiKey = "";
    public string independentApiUrl = "";
    public string independentModel = "gpt-3.5-turbo";
    
    // 缓存设置
    public bool enableConversationCache = true;
    public int conversationCacheSize = 200;
    
    // 动态注入权重
    public float weightTimeDecay = 0.3f;
    public float weightImportance = 0.3f;
    public float weightKeywordMatch = 0.4f;
    
    // 向量增强
    public bool enableVectorEnhancement = false;
    public float vectorSimilarityThreshold = 0.75f;
}
```

---

## 五、可借鉴到 StyleExpand 的改进

| 方面 | ExpandMemory 做法 | StyleExpand 可改进 |
|------|-------------------|-------------------|
| API 集成 | 集中在 `RimTalkAPIIntegration.cs` | 整合现有分散代码 |
| 异步处理 | 回调队列 + 每Tick处理 | 切分/生成改用此模式 |
| UI 组织 | Partial Class 分离 | SettingsWindow 可拆分 |
| 缓存 | 多层缓存 + 智能失效 | 添加提示词缓存 |
| 错误处理 | 重试机制 + 异常捕获 | API 调用加重试 |
| 配置设计 | 预设卡片 + 可折叠区域 | 简化配置流程 |

---

## 六、四层记忆系统架构

```
ABM (Active Buffer Memory) - 超短期记忆
    ↓ 总结（每日/手动）
ELS (Event Log Summary) - 中期记忆
    ↓ 归档（按间隔）
CLPA (Colony Lore & Persona Archive) - 长期记忆
```

## 七、核心数据流

```
RimTalk 对话请求
    ↓
RimTalkAPIIntegration (Mustache 变量注册)
    ↓
MemoryVariableProvider.GetPawnMemory()
    ↓
UnifiedMemoryInjector.Inject()
    ├── ABMCollector (对话优先)
    ├── ELSCollector (关键词匹配)
    └── VectorService (向量增强，异步)
    ↓
MemoryFormatter.Format()
    ↓
注入到 Prompt
```

---

## 八、项目亮点

1. **模块化设计** - Partial Class 和命名空间清晰分离
2. **性能优先** - 多层缓存（LRU O(1)、提示词缓存、向量缓存）
3. **异步友好** - AI 调用和向量搜索均异步，不阻塞主线程
4. **向后兼容** - 完善的旧存档兼容处理
5. **可扩展性** - 策略模式支持不同场景的动态权重配置
6. **错误恢复** - 重试机制和异常捕获确保稳定性

---

**报告完成时间：** 2026-03-23  
**分析文件数：** 91 个 .cs 文件  
**代码行数：** 约 25,000+ 行