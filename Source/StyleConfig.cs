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
        public int MaxChunkLength = 200;
        public float SimilarityThreshold = 0.5f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref BasePromptTemplate, "basePromptTemplate", "Please imitate the following writing style ({style_name}) when generating dialogue:");
            Scribe_Values.Look(ref QueryTemplate, "queryTemplate", "{{ pawn.personality }} {{ pawn.job }}");
            Scribe_Values.Look(ref TopK, "topK", 3);
            Scribe_Values.Look(ref MaxChunkLength, "maxChunkLength", 200);
            Scribe_Values.Look(ref SimilarityThreshold, "similarityThreshold", 0.5f);
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
        public int BatchSize = 10;
        public int LargeFileThreshold = 50000;
        public int SampleTargetChunks = 500;
        public bool EnableSampling = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref BatchSize, "batchSize", 10);
            Scribe_Values.Look(ref LargeFileThreshold, "largeFileThreshold", 50000);
            Scribe_Values.Look(ref SampleTargetChunks, "sampleTargetChunks", 500);
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

        public void ExposeData()
        {
            Scribe_Values.Look(ref UseRimTalkApi, "useRimTalkApi", true);
            Scribe_Values.Look(ref Url, "url", "http://localhost:11434/api/generate");
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "llama3");
            Scribe_Values.Look(ref MaxTokens, "maxTokens", 800);
        }
    }
}