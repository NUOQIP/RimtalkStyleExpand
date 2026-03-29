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

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name", "");
            Scribe_Values.Look(ref Prompt, "prompt", "");
            Scribe_Values.Look(ref IsChunked, "isChunked", false);
            Scribe_Values.Look(ref ChunkCount, "chunkCount", 0);
        }
    }

    public class VectorApiConfig : IExposable
    {
        public string Url = "http://localhost:11434/api/embeddings";
        public string ApiKey = "";
        public string Model = "nomic-embed-text";

        public void ExposeData()
        {
            Scribe_Values.Look(ref Url, "url", "http://localhost:11434/api/embeddings");
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "nomic-embed-text");
        }
    }

    public class RetrievalConfig : IExposable
    {
        public string BasePromptTemplate = "Please imitate the following writing style ({style_name}) when generating dialogue:";
        public string QueryTemplate = "{{ pawn.personality }} {{ pawn.job }}";
        public int TopK = 3;
        public float SimilarityThreshold = 0.55f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref BasePromptTemplate, "basePromptTemplate", "Please imitate the following writing style ({style_name}) when generating dialogue:");
            Scribe_Values.Look(ref QueryTemplate, "queryTemplate", "{{ pawn.personality }} {{ pawn.job }}");
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
        
        public int MinChunkLength = 120;
        public int TargetChunkLength = 450;
        public int MaxChunkLength = 900;
        public int Overlap = 0;
        
        public float BreakpointPercentileThreshold = 95f;
        
        public int BatchSize = 8;
        public int LargeFileThreshold = 15000;
        public int SampleTargetChunks = 250;
        public bool EnableSampling = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Strategy, "strategy", ChunkingStrategy.Semantic);
            Scribe_Values.Look(ref MinChunkLength, "minChunkLength", 120);
            Scribe_Values.Look(ref TargetChunkLength, "targetChunkLength", 450);
            Scribe_Values.Look(ref MaxChunkLength, "maxChunkLength", 900);
            Scribe_Values.Look(ref Overlap, "overlap", 0);
            Scribe_Values.Look(ref BreakpointPercentileThreshold, "breakpointPercentileThreshold", 95f);
            Scribe_Values.Look(ref BatchSize, "batchSize", 8);
            Scribe_Values.Look(ref LargeFileThreshold, "largeFileThreshold", 15000);
            Scribe_Values.Look(ref SampleTargetChunks, "sampleTargetChunks", 250);
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
        public string StylePromptTemplate = @"You are a writing style guide writer. Your task is to analyze the provided text sample, extract the distinctive stylistic patterns that define how the author writes independent of what they write about, and create a practical style guide to instruct other LLMs to replicate the ""{style_name}"" style.

【Requirements】
- Examine the text holistically and determine which dimensions of style are most distinctive and defining for this particular writing.
- Focus exclusively on HOW the writing works, not WHAT it contains. Extract only transferable stylistic elements that could be applied to any content.
- Let the text itself reveal what matters—different styles emphasize different elements, so adapt your analysis accordingly rather than forcing a predetermined framework.

【Forbidden】
Do not quote passages, discuss characters, describe scenes, summarize plot points, or reference specific settings or subject matter.

【Output】
- Produce a style guide within {max_tokens} tokens that captures the essence of this writing approach. Your guide should enable LLMs to replicate the style of the sample.
- Write in the same language as the input text.
- Use imperative tone.

【Sample Text】
{sample_text}";

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