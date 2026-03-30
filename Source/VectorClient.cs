using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalkStyleExpand
{
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
        
        private const int MAX_CACHE_SIZE = 1000;
        private const int CLEANUP_THRESHOLD = 1200;
        private const int MAX_RETRIES = 3;
        private const int BASE_RETRY_DELAY_MS = 500;
        
        private readonly LinkedList<string> _lruList = new LinkedList<string>();
        private readonly Dictionary<string, float[]> _embeddingCache = new Dictionary<string, float[]>();
        private readonly Dictionary<string, string> _contentHashes = new Dictionary<string, string>();
        private readonly object _cacheLock = new object();
        
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
            
            lock (_cacheLock)
            {
                if (_embeddingCache.TryGetValue(hash, out var cached))
                {
                    MoveToHead(hash);
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
                lock (_cacheLock)
                {
                    if (_embeddingCache.Count >= CLEANUP_THRESHOLD)
                    {
                        EvictLRU(MAX_CACHE_SIZE / 2);
                    }
                    
                    _embeddingCache[hash] = embedding;
                    _contentHashes[hash] = hash;
                    
                    _lruList.Remove(hash);
                    _lruList.AddFirst(hash);
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
            var truncatedText = text.Length > 400 ? text.Substring(0, 400) : text;
            
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
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

                    var isOllama = config.Url.Contains(":11434") || config.Url.Contains("ollama");
                    var isOldOllamaApi = config.Url.Contains("/api/embeddings");
                    var inputField = isOldOllamaApi ? "prompt" : "input";
                    var requestBody = $"{{\"model\":\"{config.Model}\",\"{inputField}\":\"{EscapeJson(truncatedText)}\"}}";
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
catch (WebException ex)
                    {
                        var shouldRetry = ShouldRetry(ex);
                        
                        if (attempt >= MAX_RETRIES || !shouldRetry)
                        {
                            var preview = text.Length > 100 ? text.Substring(0, 100) + "..." : text;
                            Logger.Error($"Error getting embedding after {attempt} attempts: {ex.Message}. Text preview: {preview}");
                            return null;
                        }
                        
                        var delay = BASE_RETRY_DELAY_MS * attempt * 2;
                        Logger.Warning($"Embedding API failed (attempt {attempt}), retrying in {delay}ms: {ex.Message}");
                        System.Threading.Thread.Sleep(delay);
                    }
                catch (Exception ex)
                {
                    Logger.Error($"Error getting embedding: {ex.Message}");
                    return null;
                }
            }
            
            return null;
        }
        
        private static bool ShouldRetry(WebException ex)
        {
            if (ex.Response is HttpWebResponse response)
            {
                var code = (int)response.StatusCode;
                return code == 429 || code == 500 || code == 502 || code == 503 || code == 504;
            }
            return ex.Status == WebExceptionStatus.Timeout || 
                   ex.Status == WebExceptionStatus.ConnectionClosed ||
                   ex.Status == WebExceptionStatus.ConnectFailure;
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
                // 新版 Ollama API: {"embeddings": [[...]]}
                var embeddingsStart = json.IndexOf("\"embeddings\"");
                if (embeddingsStart >= 0)
                {
                    var outerArrayStart = json.IndexOf("[", embeddingsStart);
                    if (outerArrayStart >= 0)
                    {
                        var innerArrayStart = json.IndexOf("[", outerArrayStart + 1);
                        var innerArrayEnd = json.IndexOf("]", innerArrayStart);
                        if (innerArrayStart >= 0 && innerArrayEnd >= 0)
                        {
                            var embeddingsContent = json.Substring(innerArrayStart + 1, innerArrayEnd - innerArrayStart - 1);
                            return ParseFloatArray(embeddingsContent);
                        }
                    }
                }
                
                // OpenAI API format: {"data": [{"embedding": [...]}]}
                var dataStart = json.IndexOf("\"data\"");
                if (dataStart < 0)
                {
                    // 旧版 Ollama API: {"embedding": [...]}
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
                if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
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
        
        private void MoveToHead(string hash)
        {
            _lruList.Remove(hash);
            _lruList.AddFirst(hash);
        }
        
        private void EvictLRU(int count)
        {
            for (int i = 0; i < count && _lruList.Count > 0; i++)
            {
                var oldest = _lruList.Last.Value;
                _lruList.RemoveLast();
                _embeddingCache.Remove(oldest);
                _contentHashes.Remove(oldest);
            }
            
            if (Prefs.DevMode)
            {
                Logger.Message($"[VectorClient] Evicted {count} cache entries, remaining: {_embeddingCache.Count}");
            }
        }
        
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _embeddingCache.Clear();
                _contentHashes.Clear();
                _lruList.Clear();
            }
        }
        
        public int GetCacheCount()
        {
            lock (_cacheLock)
            {
                return _embeddingCache.Count;
            }
        }
        
        public bool HasCached(string text)
        {
            var hash = ComputeHash(text);
            lock (_cacheLock)
            {
                return _embeddingCache.ContainsKey(hash);
            }
        }
        
        public void ExportCache(out List<string> hashes, out List<float[]> embeddings)
        {
            lock (_cacheLock)
            {
                hashes = new List<string>(_embeddingCache.Keys);
                embeddings = new List<float[]>(_embeddingCache.Values);
            }
        }
        
        public void ImportCache(List<string> hashes, List<float[]> embeddings)
        {
            if (hashes == null || embeddings == null) return;
            
            lock (_cacheLock)
            {
                for (int i = 0; i < hashes.Count && i < embeddings.Count; i++)
                {
                    if (embeddings[i] != null)
                    {
                        _embeddingCache[hashes[i]] = embeddings[i];
                        
                        _lruList.Remove(hashes[i]);
                        _lruList.AddFirst(hashes[i]);
                    }
                }
                
                if (_embeddingCache.Count > MAX_CACHE_SIZE)
                {
                    EvictLRU(_embeddingCache.Count - MAX_CACHE_SIZE);
                }
            }
        }
        
        #endregion
    }
}