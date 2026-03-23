using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public static class EmbeddingCache
    {
        private static readonly string CacheDir = "Styles/.cache";

        public class CachedStyle
        {
            public List<string> Chunks = new List<string>();
            public List<float[]> Embeddings = new List<float[]>();
            public int ProcessedIndex = 0;
            public string FileHash = "";
            public long FileSize = 0;
            public DateTime LastModified = DateTime.MinValue;
        }

        public static string ComputeFileHash(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }

        public static void SaveProgress(string styleName, List<string> chunks, Dictionary<string, float[]> embeddings, 
            int processedIndex, string fileHash, long fileSize, DateTime lastModified)
        {
            try
            {
                var mod = LoadedModManager.GetMod<StyleExpandMod>();
                if (mod == null) return;

                var cachePath = Path.Combine(mod.Content.RootDir, CacheDir);
                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                }

                var filePath = Path.Combine(cachePath, SanitizeFileName(styleName) + ".json");
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"fileHash\": \"{fileHash}\",");
                sb.AppendLine($"  \"fileSize\": {fileSize},");
                sb.AppendLine($"  \"lastModified\": \"{lastModified:O}\",");
                sb.AppendLine($"  \"processedIndex\": {processedIndex},");
                sb.AppendLine("  \"chunks\": [");
                
                for (int i = 0; i < chunks.Count; i++)
                {
                    sb.Append("    \"");
                    sb.Append(EscapeJsonString(chunks[i]));
                    sb.Append("\"");
                    if (i < chunks.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                
                sb.AppendLine("  ],");
                sb.AppendLine("  \"embeddings\": [");
                
                var embeddingList = new List<float[]>();
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkId = $"{styleName}_{i}";
                    if (embeddings.TryGetValue(chunkId, out var emb))
                    {
                        embeddingList.Add(emb);
                    }
                    else
                    {
                        embeddingList.Add(null);
                    }
                }
                
                for (int i = 0; i < embeddingList.Count; i++)
                {
                    var emb = embeddingList[i];
                    if (emb == null)
                    {
                        sb.Append("    null");
                    }
                    else
                    {
                        sb.Append("    [");
                        sb.Append(string.Join(", ", emb));
                        sb.Append("]");
                    }
                    if (i < embeddingList.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save embedding cache: {ex.Message}");
            }
        }

        public static void Save(string styleName, List<string> chunks, Dictionary<string, float[]> embeddings)
        {
            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return;
            
            var styleFilePath = Path.Combine(mod.Content.RootDir, "Styles", styleName + ".txt");
            var fileHash = ComputeFileHash(styleFilePath);
            var fileInfo = new FileInfo(styleFilePath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
            var lastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue;
            
            SaveProgress(styleName, chunks, embeddings, chunks.Count, fileHash, fileSize, lastModified);
            Logger.Message($"Saved cache for '{styleName}': {chunks.Count} chunks");
        }

        public static CachedStyle Load(string styleName)
        {
            try
            {
                var mod = LoadedModManager.GetMod<StyleExpandMod>();
                if (mod == null) return null;

                var filePath = Path.Combine(mod.Content.RootDir, CacheDir, SanitizeFileName(styleName) + ".json");
                if (!File.Exists(filePath)) return null;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                return ParseCache(json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load embedding cache: {ex.Message}");
                return null;
            }
        }

        public static bool HasCache(string styleName)
        {
            try
            {
                var mod = LoadedModManager.GetMod<StyleExpandMod>();
                if (mod == null) return false;

                var filePath = Path.Combine(mod.Content.RootDir, CacheDir, SanitizeFileName(styleName) + ".json");
                return File.Exists(filePath);
            }
            catch
            {
                return false;
            }
        }

        public static bool HasPartialCache(string styleName)
        {
            var cached = Load(styleName);
            return cached != null && cached.ProcessedIndex > 0 && cached.ProcessedIndex < cached.Chunks.Count;
        }

        public static bool IsCacheValid(string styleName, string currentFileHash)
        {
            var cached = Load(styleName);
            if (cached == null) return false;
            
            if (!string.IsNullOrEmpty(cached.FileHash) && !string.IsNullOrEmpty(currentFileHash))
            {
                return cached.FileHash == currentFileHash;
            }
            
            return cached.ProcessedIndex >= cached.Chunks.Count;
        }

        public static int GetProcessedIndex(string styleName)
        {
            var cached = Load(styleName);
            return cached?.ProcessedIndex ?? 0;
        }

        public static void Clear(string styleName)
        {
            try
            {
                var mod = LoadedModManager.GetMod<StyleExpandMod>();
                if (mod == null) return;

                var filePath = Path.Combine(mod.Content.RootDir, CacheDir, SanitizeFileName(styleName) + ".json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                var mod = LoadedModManager.GetMod<StyleExpandMod>();
                if (mod == null) return;

                var cachePath = Path.Combine(mod.Content.RootDir, CacheDir);
                if (Directory.Exists(cachePath))
                {
                    foreach (var file in Directory.GetFiles(cachePath, "*.json"))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private static string EscapeJsonString(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static CachedStyle ParseCache(string json)
        {
            var result = new CachedStyle();
            
            try
            {
                result.FileHash = ParseStringValue(json, "fileHash");
                result.FileSize = ParseLongValue(json, "fileSize");
                result.LastModified = ParseDateTimeValue(json, "lastModified");
                result.ProcessedIndex = ParseIntValue(json, "processedIndex");

                var chunksStart = json.IndexOf("\"chunks\"");
                if (chunksStart >= 0)
                {
                    var arrayStart = json.IndexOf("[", chunksStart);
                    var arrayEnd = FindMatchingBracket(json, arrayStart);
                    var arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                    result.Chunks = ParseStringArray(arrayContent);
                }

                var embeddingsStart = json.IndexOf("\"embeddings\"");
                if (embeddingsStart >= 0)
                {
                    var arrayStart = json.IndexOf("[", embeddingsStart);
                    var arrayEnd = FindMatchingBracket(json, arrayStart);
                    var arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                    result.Embeddings = ParseEmbeddingArray(arrayContent);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to parse cache: {ex.Message}");
            }

            return result;
        }

        private static string ParseStringValue(string json, string key)
        {
            var keyIndex = json.IndexOf($"\"{key}\"");
            if (keyIndex < 0) return "";
            
            var colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0) return "";
            
            var quoteStart = json.IndexOf('"', colonIndex);
            if (quoteStart < 0) return "";
            
            var quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return "";
            
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static int ParseIntValue(string json, string key)
        {
            var keyIndex = json.IndexOf($"\"{key}\"");
            if (keyIndex < 0) return 0;
            
            var colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0) return 0;
            
            var start = colonIndex + 1;
            while (start < json.Length && (char.IsWhiteSpace(json[start]) || json[start] == '"'))
                start++;
            
            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            
            if (int.TryParse(json.Substring(start, end - start), out var value))
                return value;
            
            return 0;
        }

        private static long ParseLongValue(string json, string key)
        {
            var keyIndex = json.IndexOf($"\"{key}\"");
            if (keyIndex < 0) return 0;
            
            var colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0) return 0;
            
            var start = colonIndex + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            
            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            
            if (long.TryParse(json.Substring(start, end - start), out var value))
                return value;
            
            return 0;
        }

        private static DateTime ParseDateTimeValue(string json, string key)
        {
            var strValue = ParseStringValue(json, key);
            if (string.IsNullOrEmpty(strValue)) return DateTime.MinValue;
            
            if (DateTime.TryParse(strValue, out var result))
                return result;
            
            return DateTime.MinValue;
        }

        private static int FindMatchingBracket(string s, int start)
        {
            int depth = 1;
            for (int i = start + 1; i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']') depth--;
                if (depth == 0) return i;
            }
            return s.Length - 1;
        }

        private static List<string> ParseStringArray(string content)
        {
            var result = new List<string>();
            var i = 0;
            
            while (i < content.Length)
            {
                var quoteStart = content.IndexOf('"', i);
                if (quoteStart < 0) break;
                
                var sb = new StringBuilder();
                var j = quoteStart + 1;
                
                while (j < content.Length)
                {
                    var c = content[j];
                    if (c == '\\' && j + 1 < content.Length)
                    {
                        var next = content[j + 1];
                        switch (next)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append(next); break;
                        }
                        j += 2;
                    }
                    else if (c == '"')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                        j++;
                    }
                }
                
                result.Add(sb.ToString());
                i = j + 1;
            }
            
            return result;
        }

        private static List<float[]> ParseEmbeddingArray(string content)
        {
            var result = new List<float[]>();
            var i = 0;
            
            while (i < content.Length)
            {
                while (i < content.Length && content[i] != '[' && content[i] != 'n') i++;
                
                if (i >= content.Length) break;
                
                if (content[i] == 'n')
                {
                    if (content.Substring(i, 4) == "null")
                    {
                        result.Add(null);
                        i += 4;
                        continue;
                    }
                }
                
                if (content[i] == '[')
                {
                    var end = FindMatchingBracket(content, i);
                    var numContent = content.Substring(i + 1, end - i - 1);
                    var parts = numContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var embedding = new float[parts.Length];
                    
                    for (int k = 0; k < parts.Length; k++)
                    {
                        if (float.TryParse(parts[k].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                        {
                            embedding[k] = val;
                        }
                    }
                    
                    result.Add(embedding);
                    i = end + 1;
                }
            }
            
            return result;
        }
    }
}