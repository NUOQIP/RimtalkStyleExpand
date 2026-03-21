using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public static class VectorClient
    {
        private static readonly Dictionary<string, float[]> _embeddingCache = new Dictionary<string, float[]>();
        
        public static float[] GetEmbeddingSync(string text, VectorApiConfig config = null)
        {
            if (_embeddingCache.TryGetValue(text, out var cached))
            {
                return cached;
            }
            
            if (config == null)
            {
                config = StyleExpandSettings.Instance?.VectorApi;
            }
            
            if (config == null || string.IsNullOrEmpty(config.Url))
            {
                Logger.Error("Vector API config is null or URL is empty");
                return null;
            }

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(config.Url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 30000;

                if (!string.IsNullOrEmpty(config.ApiKey))
                {
                    request.Headers.Add("Authorization", "Bearer " + config.ApiKey);
                }

                var requestBody = $"{{\"model\":\"{config.Model}\",\"input\":\"{EscapeJson(text)}\"}}";
                var bytes = Encoding.UTF8.GetBytes(requestBody);
                request.ContentLength = bytes.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var responseJson = reader.ReadToEnd();
                    var embedding = ParseEmbeddingResponse(responseJson);
                    
                    if (embedding != null)
                    {
                        _embeddingCache[text] = embedding;
                    }
                    
                    return embedding;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting embedding: {ex.Message}");
                return null;
            }
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        private static float[] ParseEmbeddingResponse(string json)
        {
            try
            {
                var dataStart = json.IndexOf("\"data\"");
                if (dataStart < 0)
                {
                    var embeddingStart = json.IndexOf("\"embedding\"");
                    if (embeddingStart >= 0)
                    {
                        return ParseOllamaEmbedding(json, embeddingStart);
                    }
                    return null;
                }

                var arrayStart = json.IndexOf("[", dataStart);
                if (arrayStart < 0) return null;

                var firstObjStart = json.IndexOf("{", arrayStart);
                if (firstObjStart < 0) return null;

                var embeddingArrayStart = json.IndexOf("\"embedding\"", firstObjStart);
                if (embeddingArrayStart < 0) return null;

                var arrayBegin = json.IndexOf("[", embeddingArrayStart);
                var arrayEnd = json.IndexOf("]", arrayBegin);
                
                if (arrayBegin < 0 || arrayEnd < 0) return null;

                var arrayContent = json.Substring(arrayBegin + 1, arrayEnd - arrayBegin - 1);
                return ParseFloatArray(arrayContent);
            }
            catch
            {
                return null;
            }
        }

        private static float[] ParseOllamaEmbedding(string json, int embeddingStart)
        {
            var arrayBegin = json.IndexOf("[", embeddingStart);
            var arrayEnd = json.IndexOf("]", arrayBegin);
            
            if (arrayBegin < 0 || arrayEnd < 0) return null;

            var arrayContent = json.Substring(arrayBegin + 1, arrayEnd - arrayBegin - 1);
            return ParseFloatArray(arrayContent);
        }

        private static float[] ParseFloatArray(string content)
        {
            var parts = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new float[parts.Length];
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i].Trim(), out var value))
                {
                    result[i] = value;
                }
            }
            
            return result;
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return 0f;

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0)
                return 0f;

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public static void ClearCache()
        {
            _embeddingCache.Clear();
        }
    }
}