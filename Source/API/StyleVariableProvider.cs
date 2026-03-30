using System;
using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;

namespace RimTalkStyleExpand
{
    public static class StyleVariableProvider
    {
        private static string _cachedQuery;
        private static List<StyleRetriever.StyleChunk> _cachedChunks;
        private static int _cacheTick;
        private static readonly int CacheDuration = 60;
        
        private static string _lastContext;
        private static Pawn _lastPawn;
        private static int _contextTick;
        
        public static void CacheContext(Pawn pawn, string context)
        {
            _lastPawn = pawn;
            _lastContext = context ?? "";
            _contextTick = Find.TickManager?.TicksGame ?? 0;
        }
        
        public static string GetCachedContext()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick - _contextTick > 60)
            {
                return null;
            }
            return _lastContext;
        }
        
        public static string GetStyleName(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";
            
            var style = settings.GetSelectedStyle();
            return style?.Name ?? "";
        }
        
        public static string GetStyleBasePrompt(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";
            
            var style = settings.GetSelectedStyle();
            if (style == null) return "";
            
            var basePrompt = settings.Retrieval.BasePromptTemplate ?? "";
            return basePrompt.Replace("{{style_name}}", style.Name);
        }
        
        public static string GetStylePrompt(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";
            
            var style = settings.GetSelectedStyle();
            if (style == null) return "";
            
            return style.Prompt ?? "";
        }
        
        public static string GetStyleChunks(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";
            
            var style = settings.GetSelectedStyle();
            if (style == null || !style.IsChunked) return "";
            
            try
            {
                string query = GetCachedContext();
                
                if (string.IsNullOrEmpty(query))
                {
                    query = ExtractQueryFromContext(context);
                }
                
                if (string.IsNullOrEmpty(query))
                {
                    query = style.Name;
                }
                
                var chunks = GetCachedChunks(query, settings);
                if (chunks == null || chunks.Count == 0) return "";
                
                var sb = new StringBuilder();
                for (int i = 0; i < chunks.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {chunks[i].Text}");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[StyleExpand] GetStyleChunks error: {ex.Message}");
                }
                return "";
            }
        }
        
        public static string GetStyleFull(object context)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null || !settings.IsEnabled) return "";
            
            var style = settings.GetSelectedStyle();
            if (style == null) return "";
            
            var sb = new StringBuilder();
            
            // Base prompt
            var basePrompt = GetStyleBasePrompt(context);
            if (!string.IsNullOrEmpty(basePrompt))
            {
                sb.AppendLine(basePrompt);
            }
            
            // Style Guide
            var stylePrompt = GetStylePrompt(context);
            if (!string.IsNullOrEmpty(stylePrompt))
            {
                sb.AppendLine();
                sb.AppendLine("[Style Guide]");
                sb.AppendLine(stylePrompt);
            }
            
            // Style Examples
            var chunks = GetStyleChunks(context);
            if (!string.IsNullOrEmpty(chunks))
            {
                sb.AppendLine();
                sb.AppendLine("[Style Examples]");
                sb.Append(chunks);
            }
            
            return sb.ToString();
        }
        
        private static string ExtractQueryFromContext(object context)
        {
            if (context == null) return "";
            
            try
            {
                var contextType = context.GetType();
                
                var promptProp = contextType.GetProperty("Prompt");
                if (promptProp != null)
                {
                    var prompt = promptProp.GetValue(context) as string;
                    if (!string.IsNullOrEmpty(prompt)) return prompt;
                }
                
                var userMessageProp = contextType.GetProperty("UserMessage");
                if (userMessageProp != null)
                {
                    var userMessage = userMessageProp.GetValue(context) as string;
                    if (!string.IsNullOrEmpty(userMessage)) return userMessage;
                }
                
                var lastUserMessageProp = contextType.GetProperty("LastUserMessage");
                if (lastUserMessageProp != null)
                {
                    var lastUserMessage = lastUserMessageProp.GetValue(context) as string;
                    if (!string.IsNullOrEmpty(lastUserMessage)) return lastUserMessage;
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }
        
        private static List<StyleRetriever.StyleChunk> GetCachedChunks(string query, StyleExpandSettings settings)
        {
            var currentTick = Find.TickManager?.TicksGame ?? 0;
            
            if (_cachedChunks != null && _cachedQuery == query && currentTick - _cacheTick < CacheDuration)
            {
                return _cachedChunks;
            }
            
            _cachedQuery = query;
            _cacheTick = currentTick;
            _cachedChunks = StyleRetriever.Retrieve(query, settings.Retrieval.TopK, settings.Retrieval.SimilarityThreshold);
            
            return _cachedChunks;
        }
        
        public static void ClearCache()
        {
            _cachedQuery = null;
            _cachedChunks = null;
            _cacheTick = 0;
            _lastContext = null;
            _lastPawn = null;
            _contextTick = 0;
        }
    }
}