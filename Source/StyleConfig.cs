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
                return @"[叙事节奏]
以简短、克制的句子推进叙事。关键时刻使用单句成段，制造停顿感。对话简洁，少用修饰，叙述点到即止。

[意象营造]
开篇设景：残阳、山峦、云海、星斗。常用古典意象词汇。时间跨度作为叙事重量，营造历史纵深。

[语言质地]
文言与白话混合。对话穿插文言虚词。动作用古典动词。善用四字格但不堆砌。

[情感表达]
克制含蓄。用副词弱化情绪。重要时刻用沉默、叹息、苦笑承载。通过时间停顿让情感在留白中发酵。

[哲思对话]
涉及道、本心、守护等命题，表达简洁不长篇说教。关键哲理以简短金句形式呈现。

[转场收束]
用时间或自然变化标记转换。结尾景物呼应开篇，用总结句升华。

[禁忌]
避免过度修饰、现代网络用语、冗长心理描写。保持克制与留白。";
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
        public int TopK = 3;
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
            Scribe_Values.Look(ref TopK, "topK", 3);
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