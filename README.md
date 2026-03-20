# RimTalk StyleExpand

[English](README_EN.md) | 中文

**让 RimTalk 生成的对话模仿指定文风**

基于向量检索的文风扩展 Mod，支持自定义文风。

## 功能特性

- **文风管理**：一个 txt 文件 = 一种文风
- **向量检索**：根据角色特征自动检索相似文风片段
- **Scriban 变量**：支持在 RimTalk 高级模板中自定义使用
- **缓存机制**：切分结果和 embedding 缓存到本地
- **多语言支持**：中文 / English
- **多版本支持**：RimWorld 1.5 / 1.6
- **分批切分**：大文件支持中断后继续
- **智能采样**：超大文件自动采样代表性片段
- **LLM 描述生成**：自动分析文风生成风格描述
- **批量处理**：一键切分所有文风
- **文件监听**：自动检测文风文件变化

## 快速开始

1. 放置 `.txt` 文风文件到 `Styles/` 目录
2. 启动游戏，打开 Mod 设置
3. 点击 **扫描文风** 扫描文件
4. 选择一个文风
5. 点击 **切分文风** 处理
6. 完成！

### 文风文件建议

| 语言 | 推荐字数 | 说明 |
|------|---------|------|
| 中文 | 5,000 - 50,000 字 | 太少检索效果差，太多处理时间长 |
| 英文 | 3,000 - 30,000 词 | 同上 |

> 更大的文件可以使用，但切分和 embedding 计算时间会变长。

## 配置要求

- **RimTalk** Mod（必须）
- **向量 API**（本地 Ollama 或云端 API）

### 支持的 API

- Ollama（默认 `http://localhost:11434/api/embeddings`）
- OpenAI Embedding API
- 其他 OpenAI 兼容 API

推荐模型：`bge-small-zh`、`text-embedding-3-small`

## Scriban 变量

在 RimTalk 高级模式中使用：

| 变量 | 说明 |
|------|------|
| `{{style_name}}` | 当前文风名称 |
| `{{style_base_prompt}}` | 基础指令 prompt |
| `{{style_prompt}}` | 文风描述 |
| `{{style_chunks}}` | 检索到的示例片段 |
| `{{style_full}}` | 完整文风 prompt（以上组合） |

### 示例模板

```
[Style Instruction]
{{style_base_prompt}}

{{style_prompt}}

{{style_chunks}}
```

## 文件结构

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/StyleExpand.xml
│   └── ChineseSimplified/Keyed/StyleExpand.xml
├── 1.5/Assemblies/
│   ├── RimTalk-StyleExpand.dll
│   └── Newtonsoft.Json.dll
├── 1.6/Assemblies/
│   ├── RimTalk-StyleExpand.dll
│   └── Newtonsoft.Json.dll
├── Styles/
│   ├── Tsundere.txt
│   ├── Classical.txt
│   ├── Satirical.txt
│   └── .cache/           # 自动生成的缓存
└── Source/
```

## 项目进度

### v1.2（当前版本）

- [x] 文风预览功能
  - 变量选择器（从 RimTalk 获取）
  - 手动输入预览
  - 模板渲染预览
- [x] 切分算法优化
  - 保持引号括号完整性
  - 语义边界检测
  - 缩写识别

### v1.1

- [x] 分批切分 + 中断后继续
- [x] 大文件采样策略
- [x] LLM 自动生成文风提示词
  - 支持复用 RimTalk API 配置
  - 支持单独配置生成专用 API
- [x] 批量切分按钮
- [x] 文件变化自动重载
- [x] 进度条显示
- [x] 切分配置选项

### v1.0

- [x] 向量检索功能
- [x] Prompt 注入
- [x] 文风管理（单选模式）
- [x] 配置界面
- [x] 文本切分（段落+句子）
- [x] Embedding 缓存
- [x] 手动重载按钮
- [x] 打开文件夹按钮
- [x] Scriban 变量注册
- [x] 基础 prompt 可编辑
- [x] 帮助文档窗口
- [x] 状态提示/错误提示
- [x] 重置按钮
- [x] 中英双语支持
- [x] 切分进度显示
- [x] 大文件警告
- [x] RimWorld 1.5 / 1.6 支持

### v1.3（计划中）

- [ ] 更多预置文风
- [ ] 性能优化

## 编译

```bash
# 编译 1.5 版本
dotnet build -p:GameVersion=1.5

# 编译 1.6 版本
dotnet build -p:GameVersion=1.6
```

## 依赖

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## 许可证

基于 RimTalk 开发，遵循相同许可证。

## 作者

NUOQI_P