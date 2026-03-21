using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalkStyleExpand
{
    /// <summary>
    /// 向量客户端 - 单例模式
    /// 支持：
    /// - 异步 API（不阻塞主线程）
    /// - 内容哈希缓存（检测变更）
    /// - 批量 embedding 获取
    /// </summary>
    public class VectorClient
    {
        private static VectorClient _instance;
        private static readonly object _lock = new object();
        
        public static VectorClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VectorClient();
                        }
                    }
                }
                return _instance;
            }
        }
        
        private readonly Dictionary<string, float[]> _embeddingCache = new Dictionary<string, float[]>();
        private readonly Dictionary<string, string> _contentHashes = new Dictionary<string, string>();
        
        private VectorClient()
        {
        }
        
        #region 同步 API（向后兼容）
        
        public static float[] GetEmbeddingSync(string text, VectorApiConfig config = null)
        {
            return Instance.GetEmbeddingInternal(text, config);
        }
        
        public float[] GetEmbeddingInternal(string text, VectorApiConfig config = null)
        {
            return GetEmbeddingAsync(text, config).GetAwaiter().GetResult();
        }
        
        #endregion
        
        #region 异步 API
        
        public async Task<float[]> GetEmbeddingAsync(string text, VectorApiConfig config = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            
            var hash = ComputeHash(text);
            
            lock (_embeddingCache)
            {
                if (_embeddingCache.TryGetValue(hash, out var cached))
                {
                    return cached;
                }
            }
            
            config = config ?? StyleExpandSettings.Instance?.VectorApi;
            if (config == null || string.IsNullOrEmpty(config.Url))
            {
                Logger.Error("Vector API config is null or URL is empty");
                return null;
            }
            
            var embedding = await GetEmbeddingFromApiAsync(text, config).ConfigureAwait(false);
            
            if (embedding != null)
            {
                lock (_embeddingCache)
                {
                    _embeddingCache[hash] = embedding;
                    _contentHashes[hash] = hash;
                }
            }
            
            return embedding;
        }
        
        public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, VectorApiConfig config = null)
        {
            var results = new List<float[]>();
            
            if (texts == null || texts.Count == 0) return results;
            
            config = config ?? StyleExpandSettings.Instance?.VectorApi;
            if (config == null || string.IsNullOrEmpty(config.Url))
            {
                Logger.Error("Vector API config is null or URL is empty");
                return results;
            }
            
            foreach (var text in texts)
            {
                var embedding = await GetEmbeddingAsync(text, config).ConfigureAwait(false);
                results.Add(embedding);
                
                await Task.Delay(100).ConfigureAwait(false);
            }
            
            return results;
        }
        
        private async Task<float[]> GetEmbeddingFromApiAsync(string text, VectorApiConfig config)
        {
            return await Task.Run(() => GetEmbeddingFromApi(text, config)).ConfigureAwait(false);
        }
        
        private float[] GetEmbeddingFromApi(string text, VectorApiConfig config)
        {
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
                    return ParseEmbeddingResponse(responseJson);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting embedding: {ex.Message}");
                return null;
            }
        }
        
        #endregion
        
        #region 工具方法
        
        private static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
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
        
        #endregion
        
        #region 缓存管理
        
        public void ClearCache()
        {
            lock (_embeddingCache)
            {
                _embeddingCache.Clear();
                _contentHashes.Clear();
            }
        }
        
        public int GetCacheCount()
        {
            lock (_embeddingCache)
            {
                return _embeddingCache.Count;
            }
        }
        
        public bool HasCached(string text)
        {
            var hash = ComputeHash(text);
            lock (_embeddingCache)
            {
                return _embeddingCache.ContainsKey(hash);
            }
        }
        
        public void ExportCache(out List<string> hashes, out List<float[]> embeddings)
        {
            lock (_embeddingCache)
            {
                hashes = new List<string>(_embeddingCache.Keys);
                embeddings = new List<float[]>(_embeddingCache.Values);
            }
        }
        
        public void ImportCache(List<string> hashes, List<float[]> embeddings)
        {
            if (hashes == null || embeddings == null) return;
            
            lock (_embeddingCache)
            {
                for (int i = 0; i < hashes.Count && i < embeddings.Count; i++)
                {
                    if (embeddings[i] != null)
                    {
                        _embeddingCache[hashes[i]] = embeddings[i];
                    }
                }
            }
        }
        
        #endregion
    }
}