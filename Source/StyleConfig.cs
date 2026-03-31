using System;
using System.Collections.Generic;
using Verse;

namespace RimTalkStyleExpand
{
    public class StyleConfig : IExposable
    {
        public string Name;
        public string Prompt;
        public bool IsChunked;
        public int ChunkCount;

        public StyleConfig()
        {
            Name = "";
            Prompt = "";
            IsChunked = false;
            ChunkCount = 0;
        }

        public StyleConfig(string name)
        {
            Name = name;
            Prompt = "";
            IsChunked = false;
            ChunkCount = 0;
        }

        public StyleConfig(string name, string prompt)
        {
            Name = name;
            Prompt = prompt;
            IsChunked = false;
            ChunkCount = 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name", "");
            Scribe_Values.Look(ref Prompt, "prompt", "");
            Scribe_Values.Look(ref IsChunked, "isChunked", false);
            Scribe_Values.Look(ref ChunkCount, "chunkCount", 0);
        }
        
        public static string GetDefaultPrompt(string styleName)
        {
            if (styleName == "古风仙侠")
            {
                return @"# 古风仙侠对话风格指南
## 宏观写作风格
- 古风文言文和白话混合
- 以景物渲染情绪。四字短句或对偶句式定调：「残阳如血」「白衣胜雪」，让环境成为情感的外化。
- 对话前必有动作或神态铺垫。不直接抛出台词，让语气具象化。

## 对话构建原则
### 称谓系统
- 长辈对晚辈：直呼其名或「这孩子」
- 晚辈对长辈：「师父」「前辈」+敬语
- 平辈间：姓氏+「姑娘/公子」，或直呼全名

### 语气层次
- 长者语气：多用「罢了」「不过是」「何止」等淡然词，句式舒缓
- 年轻修士：保持恭敬但不卑微

### 句式特征
- 禁用现代口语
- 多用文言虚词：「可是」「却」「便」「方才」
- 陈述多用倒装：「我的道，是剑」而非「剑是我的道」
- 感叹多用省略：「好剑。」「不错，不错。」

## 叙述性对话技巧
- 人物回忆往事时，用「那一战」「那时」等指代词制造距离感。数字具体化：「三百年」「七天七夜」「二十年」，强化时间厚重感。
- 长段独白分层递进：先陈述事实→再说结果→最后点明意义。

## 意境营造手法
### 留白艺术
- 关键信息截断：「他说：'玄'」戛然而止，制造悬念。
- 用沉默传递情绪。

### 哲理植入
不直接说教，借角色之口自然流露：「修炼的意义，因人而异」「剑是用来守护的」。

### 动作细节
- 动作必写详细过程：「犹豫片刻，将断剑递了过去」「接过断剑，仔细端详」。
- 多用「端详」「打量」「目光投向」等文雅动词。

## 收束技巧
- 结尾可有景物呼应开头：「夕阳西沉」对应「残阳如血」。
- 涉及哲理时，可在最后一句升华主题，用「这是...也是...」句式扩大格局。
- 人物离去用「并肩」「身影」等意象，营造余韵。

## 禁忌事项
- 禁用网络流行语、表情描写
- 避免过度煽情的「啊」「呀」语气词
- 不用「哈哈」「呵呵」等笑声拟声词，改用「轻笑」「苦笑」
- 对话不加冗余的「我认为」「我想说」等现代表达";
            }
            return "";
        }
    }

    public class VectorApiConfig : IExposable
    {
        public string Url = "http://localhost:11434/api/embed";
        public string ApiKey = "";
        public string Model = "mxbai-embed-large";

        public void ExposeData()
        {
            Scribe_Values.Look(ref Url, "url", "http://localhost:11434/api/embed");
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "mxbai-embed-large");
        }
    }

    public class RetrievalConfig : IExposable
    {
        public string FullPromptTemplate = @"Write in the **{{style_name}}** style.

Read the style guide below. You must apply these style characteristics in all your outputs.

[Style Guide]
{{style_prompt}}

[Style Examples]
{{style_chunks}}

For reference only on language form; apply flexibly based on the current scene.";
        public string QueryTemplate = "";
        public int TopK = 2;
        public float SimilarityThreshold = 0.55f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref FullPromptTemplate, "fullPromptTemplate", @"Write in the **{{style_name}}** style.

Read the style guide below. You must apply these style characteristics in all your outputs.

[Style Guide]
{{style_prompt}}

[Style Examples]
{{style_chunks}}

For reference only on language form; apply flexibly based on the current scene.");
            Scribe_Values.Look(ref QueryTemplate, "queryTemplate", "");
            Scribe_Values.Look(ref TopK, "topK", 2);
            Scribe_Values.Look(ref SimilarityThreshold, "similarityThreshold", 0.55f);
        }
    }

    public class DebugConfig : IExposable
    {
        public bool ShowQuery = false;
        public bool ShowChunks = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ShowQuery, "showQuery", false);
            Scribe_Values.Look(ref ShowChunks, "showChunks", false);
        }
    }

    public class ChunkingConfig : IExposable
    {
        public ChunkingStrategy Strategy = ChunkingStrategy.Semantic;
        
        public int MinChunkLength = 100;
        public int TargetChunkLength = 400;
        public int MaxChunkLength = 800;
        public int Overlap = 50;
        
        public float BreakpointPercentileThreshold = 80f;
        
        public int BatchSize = 10;
        public int LargeFileThreshold = 20000;
        public int SampleTargetChunks = 300;
        public bool EnableSampling = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Strategy, "strategy", ChunkingStrategy.Semantic);
            Scribe_Values.Look(ref MinChunkLength, "minChunkLength", 100);
            Scribe_Values.Look(ref TargetChunkLength, "targetChunkLength", 400);
            Scribe_Values.Look(ref MaxChunkLength, "maxChunkLength", 800);
            Scribe_Values.Look(ref Overlap, "overlap", 50);
            Scribe_Values.Look(ref BreakpointPercentileThreshold, "breakpointPercentileThreshold", 80f);
            Scribe_Values.Look(ref BatchSize, "batchSize", 10);
            Scribe_Values.Look(ref LargeFileThreshold, "largeFileThreshold", 20000);
            Scribe_Values.Look(ref SampleTargetChunks, "sampleTargetChunks", 300);
            Scribe_Values.Look(ref EnableSampling, "enableSampling", true);
        }
    }

    public class LlmApiConfig : IExposable
    {
        public bool UseRimTalkApi = true;
        public string Url = "http://localhost:11434/api/generate";
        public string ApiKey = "";
        public string Model = "llama3";
        public int MaxTokens = 800;
        public string StylePromptTemplate = @"You are a writing style guide writer. Your task is to analyze the provided text sample, extract its distinctive style patterns, and create a practical style guide to instruct other LLMs to replicate the ""{{style_name}}"" writing style for text output.

【Important Note】
This style guide will be used for RP dialogue generation in RimTalk, a RimWorld mod.

【Core Requirements】
- Examine the text holistically and determine which style dimensions are most defining and distinctive for this writing, rather than applying a predetermined analytical framework.
- Focus only on ""how it is written,"" not ""what is written."" Extract style elements that can be transferred to any content.
- Focus on: macro style, writing perspective, writing structure, writing philosophy/principles, word choice and sentence construction, rhetorical devices, description techniques (action, appearance, expression descriptions, etc.), and writing skills.

【Prohibitions】
- Ignore content-specific elements that only exist in this text (e.g., character settings).
- Do not include formatting rules.
- Avoid abstract theories unless they can be directly applied to dialogue generation.

【Output Requirements】
- Strictly limit to {{max_tokens}} tokens.
- Write in the same language as the input text.
- Use imperative tone to guide practice, not analytical reports.
- Output should enable other LLMs to directly execute style replication, not merely understand style characteristics.

【Sample Text】
{{sample_text}}";

        public void ExposeData()
        {
            Scribe_Values.Look(ref UseRimTalkApi, "useRimTalkApi", true);
            Scribe_Values.Look(ref Url, "url", "http://localhost:11434/api/generate");
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "llama3");
            Scribe_Values.Look(ref MaxTokens, "maxTokens", 800);
            Scribe_Values.Look(ref StylePromptTemplate, "stylePromptTemplate", StylePromptTemplate);
        }
    }
}