using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimTalkStyleExpand
{
    public static class PromptBuilder
    {
        public static string BuildStylePrompt(Pawn pawn)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle == null) return "";

            var sb = new StringBuilder();
            
            var basePrompt = settings.Retrieval.BasePromptTemplate.Replace("{{style_name}}", selectedStyle.Name);
            sb.AppendLine("[Style Instruction]");
            sb.AppendLine(basePrompt);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(selectedStyle.Prompt))
            {
                sb.AppendLine($"## {selectedStyle.Name} Style");
                sb.AppendLine(selectedStyle.Prompt);
                sb.AppendLine();
            }

            var query = StyleRetriever.RenderQueryTemplate(settings.Retrieval.QueryTemplate, pawn);
            
            if (settings.Debug.ShowQuery)
            {
                Logger.Message($"Query: {query}");
            }

            var chunks = StyleRetriever.Retrieve(
                query, 
                settings.Retrieval.TopK, 
                settings.Retrieval.SimilarityThreshold
            );

            if (chunks.Count > 0)
            {
                sb.AppendLine($"### Style Examples:");
                
                foreach (var chunk in chunks)
                {
                    sb.AppendLine($"- {chunk.Text}");
                }
                
                sb.AppendLine();

                if (settings.Debug.ShowChunks)
                {
                    Logger.Message($"Retrieved {chunks.Count} chunks for style '{selectedStyle.Name}'");
                }
            }
            else
            {
                if (settings.Debug.ShowChunks)
                {
                    Logger.Message($"No chunks retrieved for style '{selectedStyle.Name}' (is it chunked?)");
                }
            }

            return sb.ToString();
        }

        public static string GetBasePrompt()
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle == null) return "";

            return settings.Retrieval.BasePromptTemplate.Replace("{{style_name}}", selectedStyle.Name);
        }

        public static string GetStylePromptSection()
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle == null || string.IsNullOrEmpty(selectedStyle.Prompt)) return "";

            var sb = new StringBuilder();
            sb.AppendLine($"## {selectedStyle.Name} Style");
            sb.AppendLine(selectedStyle.Prompt);
            return sb.ToString().TrimEnd();
        }

        public static string GetRetrievedChunksSection(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle == null) return "";

            Pawn pawn = null;
            
            try
            {
                var contextType = context?.GetType();
                var currentPawnProp = contextType?.GetProperty("CurrentPawn");
                if (currentPawnProp != null)
                {
                    pawn = currentPawnProp.GetValue(context) as Pawn;
                }
            }
            catch { }

            if (pawn == null) return "";

            var query = StyleRetriever.RenderQueryTemplate(settings.Retrieval.QueryTemplate, pawn);
            var chunks = StyleRetriever.Retrieve(
                query, 
                settings.Retrieval.TopK, 
                settings.Retrieval.SimilarityThreshold
            );

            if (chunks.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("### Style Examples:");
            
            foreach (var chunk in chunks)
            {
                sb.AppendLine($"- {chunk.Text}");
            }
            
            return sb.ToString().TrimEnd();
        }

        public static string GetFullStylePrompt(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle == null) return "";

            var sb = new StringBuilder();
            
            sb.AppendLine("[Style Instruction]");
            sb.AppendLine(GetBasePrompt());
            sb.AppendLine();
            
            var styleSection = GetStylePromptSection();
            if (!string.IsNullOrEmpty(styleSection))
            {
                sb.AppendLine(styleSection);
                sb.AppendLine();
            }
            
            var chunksSection = GetRetrievedChunksSection(context);
            if (!string.IsNullOrEmpty(chunksSection))
            {
                sb.AppendLine(chunksSection);
            }

            return sb.ToString().TrimEnd();
        }

        public static string InjectIntoSystemPrompt(string systemPrompt, string stylePrompt)
        {
            if (string.IsNullOrEmpty(stylePrompt)) return systemPrompt;

            var insertionPoint = systemPrompt.IndexOf('\n');
            if (insertionPoint < 0) insertionPoint = systemPrompt.Length;

            var sb = new StringBuilder();
            sb.Append(systemPrompt.Substring(0, insertionPoint));
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(stylePrompt);
            
            if (insertionPoint < systemPrompt.Length)
            {
                sb.Append(systemPrompt.Substring(insertionPoint));
            }

            return sb.ToString();
        }
    }
}