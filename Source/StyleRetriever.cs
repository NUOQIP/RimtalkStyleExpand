using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimTalkStyleExpand
{
    public static class StyleRetriever
    {
        private static Dictionary<string, float[]> _chunkEmbeddings = new Dictionary<string, float[]>();
        private static Dictionary<string, StyleChunk> _chunks = new Dictionary<string, StyleChunk>();
        private static Dictionary<string, List<StyleChunk>> _chunksByStyle = new Dictionary<string, List<StyleChunk>>();
        private static bool _initialized = false;
        private static int _chunkProgress = 0;
        private static int _chunkTotal = 0;
        private static string _chunkStyleName = "";
        private static bool _chunkCancelled = false;
        private static List<string> _pendingChunks = new List<string>();
        private static Dictionary<string, float[]> _pendingEmbeddings = new Dictionary<string, float[]>();

        public static int ChunkProgress => _chunkProgress;
        public static int ChunkTotal => _chunkTotal;
        public static string ChunkStyleName => _chunkStyleName;
        public static bool IsChunking => !string.IsNullOrEmpty(_chunkStyleName);
        public static bool WasCancelled => _chunkCancelled;
        public static bool HasPartialProgress => _chunkProgress > 0 && _chunkProgress < _chunkTotal;

        public class StyleChunk
        {
            public string StyleName;
            public string Text;
            public string ChunkId;
        }

        public static int GetChunkCount(string styleName)
        {
            if (_chunksByStyle.TryGetValue(styleName, out var chunks))
            {
                return chunks.Count;
            }
            return 0;
        }

        public static bool IsStyleChunked(string styleName)
        {
            var settings = StyleExpandSettings.Instance;
            var style = settings?.GetSelectedStyle();
            if (style == null || style.Name != styleName) return false;
            
            if (_chunksByStyle.ContainsKey(styleName) && _chunksByStyle[styleName].Count > 0)
            {
                return true;
            }
            
            return EmbeddingCache.HasCache(styleName);
        }

        public static bool CanResumeChunking(string styleName)
        {
            return EmbeddingCache.HasPartialCache(styleName);
        }

        public static void CancelChunking()
        {
            if (IsChunking)
            {
                _chunkCancelled = true;
            }
        }

        public static void ResetChunkingState()
        {
            _chunkCancelled = false;
            _chunkStyleName = "";
            _chunkProgress = 0;
            _chunkTotal = 0;
            _pendingChunks.Clear();
            _pendingEmbeddings.Clear();
        }

        public static int ChunkAllStyles()
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return 0;
            
            var processed = 0;
            var styles = settings.Styles.ToList();
            
            foreach (var style in styles)
            {
                if (_chunkCancelled) break;
                
                try
                {
                    if (!EmbeddingCache.HasCache(style.Name) || EmbeddingCache.HasPartialCache(style.Name))
                    {
                        ChunkStyle(style.Name, EmbeddingCache.HasPartialCache(style.Name));
                        processed++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to chunk style '{style.Name}': {ex.Message}");
                }
            }
            
            return processed;
        }

        public static int GetUnchunkedCount()
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return 0;
            
            return settings.Styles.Count(s => !EmbeddingCache.HasCache(s.Name) || EmbeddingCache.HasPartialCache(s.Name));
        }

        public static void Initialize()
        {
            if (_initialized) return;
            
            _chunkEmbeddings.Clear();
            _chunks.Clear();
            _chunksByStyle.Clear();
            
            ScanStyleFiles();
            
            var settings = StyleExpandSettings.Instance;
            if (settings != null)
            {
                foreach (var style in settings.Styles)
                {
                    LoadFromCache(style.Name);
                }
            }
            
            _initialized = true;
            Logger.Message($"Initialized with {_chunks.Count} chunks from {_chunksByStyle.Count} styles");
        }

        public static void ScanStyleFiles()
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return;

            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return;

            var stylesPath = Path.Combine(mod.Content.RootDir, "Styles");
            if (!Directory.Exists(stylesPath))
            {
                Directory.CreateDirectory(stylesPath);
                Logger.Message($"Created Styles directory: {stylesPath}");
                return;
            }

            var txtFiles = Directory.GetFiles(stylesPath, "*.txt");
            var existingNames = new HashSet<string>(settings.Styles.Select(s => s.Name));
            var newStyles = new List<StyleConfig>();
            var removedStyles = settings.Styles.Where(s => !File.Exists(Path.Combine(stylesPath, s.Name + ".txt"))).ToList();

            foreach (var file in txtFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!existingNames.Contains(name))
                {
                    newStyles.Add(new StyleConfig(name));
                    Logger.Message($"Found new style file: {name}");
                }
            }

            foreach (var removed in removedStyles)
            {
                settings.RemoveStyle(removed.Name);
                EmbeddingCache.Clear(removed.Name);
                Logger.Message($"Removed style (file deleted): {removed.Name}");
            }

            foreach (var newStyle in newStyles)
            {
                settings.AddStyle(newStyle);
            }

            if (string.IsNullOrEmpty(settings.SelectedStyleName) && settings.Styles.Count > 0)
            {
                settings.SelectStyle(settings.Styles[0].Name);
            }

            settings.Write();
        }

        public static void ReloadStyles()
        {
            _initialized = false;
            _chunkEmbeddings.Clear();
            _chunks.Clear();
            _chunksByStyle.Clear();
            Initialize();
        }

        public static void ChunkStyle(string styleName, bool resume = false)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null) throw new Exception("Settings not initialized");

            var style = settings.Styles.FirstOrDefault(s => s.Name == styleName);
            if (style == null) throw new Exception($"Style '{styleName}' not found");

            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) throw new Exception("Mod not loaded");

            var filePath = Path.Combine(mod.Content.RootDir, "Styles", styleName + ".txt");
            if (!File.Exists(filePath))
            {
                throw new Exception($"Style file not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var currentFileHash = EmbeddingCache.ComputeFileHash(filePath);
            var fileChanged = !EmbeddingCache.IsCacheValid(styleName, currentFileHash);

            if (resume && !CanResumeChunking(styleName))
            {
                throw new Exception("No partial progress to resume");
            }

            if (!resume && !fileChanged && EmbeddingCache.HasCache(styleName))
            {
                LoadFromCache(styleName);
                return;
            }

            _chunkCancelled = false;
            _chunkStyleName = styleName;
            _pendingChunks.Clear();
            _pendingEmbeddings.Clear();

            List<string> chunks;
            int startIndex = 0;

            if (resume)
            {
                var cached = EmbeddingCache.Load(styleName);
                if (cached != null && cached.ProcessedIndex > 0)
                {
                    chunks = cached.Chunks;
                    startIndex = cached.ProcessedIndex;
                    
                    for (int i = 0; i < startIndex; i++)
                    {
                        var chunkId = $"{styleName}_{i}";
                        if (i < cached.Embeddings.Count && cached.Embeddings[i] != null)
                        {
                            _pendingEmbeddings[chunkId] = cached.Embeddings[i];
                        }
                    }
                    
                    _pendingChunks = chunks.ToList();
                    Logger.Message($"Resuming chunking for '{styleName}' from index {startIndex}");
                }
                else
                {
                    var text = File.ReadAllText(filePath, Encoding.UTF8);
                    chunks = PrepareChunks(text, settings);
                    _pendingChunks = chunks;
                }
            }
            else
            {
                var text = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new Exception($"Style file is empty: {filePath}");
                }
                
                chunks = PrepareChunks(text, settings);
                _pendingChunks = chunks;
                ClearStyleChunks(styleName);
            }

            if (_pendingChunks.Count == 0)
            {
                throw new Exception("No chunks generated from style file");
            }

            _chunkTotal = _pendingChunks.Count;
            _chunkProgress = startIndex;

            var batchSize = settings.Chunking.BatchSize;
            var failedEmbeddings = 0;

            for (int i = startIndex; i < _pendingChunks.Count; i++)
            {
                if (_chunkCancelled)
                {
                    SaveProgressAndCleanup(styleName, filePath, currentFileHash, fileInfo);
                    Logger.Message($"Chunking cancelled for '{styleName}' at {i}/{_pendingChunks.Count}");
                    _chunkStyleName = "";
                    _chunkProgress = 0;
                    _chunkTotal = 0;
                    return;
                }

                _chunkProgress = i + 1;
                
                var chunkId = $"{styleName}_{i}";
                var chunk = new StyleChunk
                {
                    StyleName = styleName,
                    Text = _pendingChunks[i],
                    ChunkId = chunkId
                };
                
                _chunks[chunkId] = chunk;
                
                var embedding = VectorClient.GetEmbeddingSync(_pendingChunks[i]);
                if (embedding != null)
                {
                    _chunkEmbeddings[chunkId] = embedding;
                    _pendingEmbeddings[chunkId] = embedding;
                }
                else
                {
                    failedEmbeddings++;
                }

                if ((i + 1) % batchSize == 0 || i == _pendingChunks.Count - 1)
                {
                    EmbeddingCache.SaveProgress(styleName, _pendingChunks, _pendingEmbeddings, 
                        i + 1, currentFileHash, fileInfo.Length, fileInfo.LastWriteTime);
                }
            }

            FinalizeChunking(styleName, style, failedEmbeddings);
        }

        private static List<string> PrepareChunks(string text, StyleExpandSettings settings)
        {
            var chunks = SplitIntoChunks(text, settings.Retrieval.MaxChunkLength);
            
            if (settings.Chunking.EnableSampling && chunks.Count > settings.Chunking.SampleTargetChunks)
            {
                chunks = SampleChunks(chunks, settings.Chunking.SampleTargetChunks);
                Logger.Message($"Sampled {chunks.Count} chunks from {SplitIntoChunks(text, settings.Retrieval.MaxChunkLength).Count} total");
            }
            
            return chunks;
        }

        private static List<string> SampleChunks(List<string> chunks, int targetCount)
        {
            if (chunks.Count <= targetCount) return chunks;
            
            var result = new List<string>();
            var step = (double)chunks.Count / targetCount;
            
            for (int i = 0; i < targetCount; i++)
            {
                var index = (int)(i * step);
                if (index < chunks.Count)
                {
                    result.Add(chunks[index]);
                }
            }
            
            var remaining = chunks.Count - result.Count;
            if (remaining > 0 && chunks.Count > targetCount)
            {
                var midIndex = chunks.Count / 2;
                if (!result.Contains(chunks[midIndex]))
                {
                    result.Insert(result.Count / 2, chunks[midIndex]);
                }
            }
            
            return result;
        }

        private static void SaveProgressAndCleanup(string styleName, string filePath, string fileHash, FileInfo fileInfo)
        {
            EmbeddingCache.SaveProgress(styleName, _pendingChunks, _pendingEmbeddings, 
                _chunkProgress, fileHash, fileInfo.Length, fileInfo.LastWriteTime);
            
            var settings = StyleExpandSettings.Instance;
            var style = settings?.Styles.FirstOrDefault(s => s.Name == styleName);
            if (style != null)
            {
                style.ProcessedChunks = _chunkProgress;
                style.TotalChunks = _chunkTotal;
                style.IsProcessing = false;
                settings?.Write();
            }
        }

        private static void FinalizeChunking(string styleName, StyleConfig style, int failedEmbeddings)
        {
            var styleChunks = new List<StyleChunk>();
            for (int i = 0; i < _pendingChunks.Count; i++)
            {
                var chunkId = $"{styleName}_{i}";
                if (_chunks.TryGetValue(chunkId, out var chunk))
                {
                    styleChunks.Add(chunk);
                }
            }
            
            _chunksByStyle[styleName] = styleChunks;
            style.IsChunked = true;
            style.ChunkCount = _pendingChunks.Count;
            style.ProcessedChunks = _pendingChunks.Count;
            style.TotalChunks = _pendingChunks.Count;
            style.IsProcessing = false;
            
            var settings = StyleExpandSettings.Instance;
            settings?.Write();
            
            _chunkStyleName = "";
            _chunkProgress = 0;
            _chunkTotal = 0;
            _pendingChunks.Clear();
            _pendingEmbeddings.Clear();
            
            if (failedEmbeddings > 0)
            {
                Logger.Warning($"Chunked style '{styleName}': {_pendingChunks.Count} chunks, {failedEmbeddings} failed embeddings");
            }
            else
            {
                Logger.Message($"Chunked style '{styleName}': {_pendingChunks.Count} chunks");
            }
            
            if (styleChunks.Count == 0)
            {
                throw new Exception($"No valid chunks. Check API configuration.");
            }
        }
        
        public static int GetFileCharCount(string styleName)
        {
            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return 0;
            
            var filePath = Path.Combine(mod.Content.RootDir, "Styles", styleName + ".txt");
            if (!File.Exists(filePath)) return 0;
            
            return File.ReadAllText(filePath, Encoding.UTF8).Length;
        }

        private static void ClearStyleChunks(string styleName)
        {
            if (_chunksByStyle.TryGetValue(styleName, out var chunks))
            {
                foreach (var chunk in chunks)
                {
                    _chunks.Remove(chunk.ChunkId);
                    _chunkEmbeddings.Remove(chunk.ChunkId);
                }
                _chunksByStyle.Remove(styleName);
            }
        }

        private static void LoadFromCache(string styleName)
        {
            var cached = EmbeddingCache.Load(styleName);
            if (cached == null || cached.Chunks.Count == 0) return;

            var styleChunks = new List<StyleChunk>();
            
            for (int i = 0; i < cached.Chunks.Count; i++)
            {
                var chunkId = $"{styleName}_{i}";
                var chunk = new StyleChunk
                {
                    StyleName = styleName,
                    Text = cached.Chunks[i],
                    ChunkId = chunkId
                };
                
                _chunks[chunkId] = chunk;
                styleChunks.Add(chunk);
                
                if (i < cached.Embeddings.Count && cached.Embeddings[i] != null)
                {
                    _chunkEmbeddings[chunkId] = cached.Embeddings[i];
                }
            }
            
            _chunksByStyle[styleName] = styleChunks;
            
            var settings = StyleExpandSettings.Instance;
            var style = settings?.Styles.FirstOrDefault(s => s.Name == styleName);
            if (style != null)
            {
                style.IsChunked = true;
                style.ChunkCount = cached.Chunks.Count;
            }
            
            Logger.Message($"Loaded {cached.Chunks.Count} cached chunks for '{styleName}'");
        }

        private static List<string> SplitIntoChunks(string text, int maxLength)
        {
            var chunks = new List<string>();
            
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraph in paragraphs)
            {
                var sentences = SplitIntoSentences(paragraph);
                var currentChunk = new StringBuilder();
                
                foreach (var sentence in sentences)
                {
                    var trimmed = sentence.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    
                    if (currentChunk.Length + trimmed.Length + 1 > maxLength && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    
                    currentChunk.Append(trimmed);
                }
                
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                }
            }
            
            return chunks;
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var current = new StringBuilder();
            int quoteDepth = 0;
            int bracketDepth = 0;
            int parenDepth = 0;
            
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                current.Append(c);
                
                if (IsOpenQuote(c)) quoteDepth++;
                else if (IsCloseQuote(c) && quoteDepth > 0) quoteDepth--;
                else if (c == '[' || c == '【') bracketDepth++;
                else if (c == ']' || c == '】') bracketDepth = Math.Max(0, bracketDepth - 1);
                else if (c == '(' || c == '（') parenDepth++;
                else if (c == ')' || c == '）') parenDepth = Math.Max(0, parenDepth - 1);
                
                if (quoteDepth == 0 && bracketDepth == 0 && parenDepth == 0)
                {
                    bool isEnd = c == '。' || c == '！' || c == '？' || c == '.' || c == '!' || c == '?';
                    
                    if (isEnd)
                    {
                        bool shouldSplit = true;
                        
                        if (c == '.' && i + 1 < text.Length)
                        {
                            char next = text[i + 1];
                            if (char.IsDigit(next) || char.IsLower(next) || next == '.')
                            {
                                shouldSplit = false;
                            }
                        }
                        
                        if (c == '.' && i > 0 && i < text.Length - 1)
                        {
                            int start = Math.Max(0, i - 10);
                            string context = text.Substring(start, i - start + 1);
                            if (LooksLikeAbbreviation(context))
                            {
                                shouldSplit = false;
                            }
                        }
                        
                        if (shouldSplit)
                        {
                            sentences.Add(current.ToString());
                            current.Clear();
                        }
                    }
                }
            }
            
            if (current.Length > 0)
            {
                sentences.Add(current.ToString());
            }
            
            return sentences;
        }

        private static bool IsOpenQuote(char c)
        {
            return c == '"' || c == '"' || c == '「' || c == '『' || c == '“' || c == '『';
        }

        private static bool IsCloseQuote(char c)
        {
            return c == '"' || c == '"' || c == '」' || c == '』' || c == '”' || c == '』';
        }

        private static bool LooksLikeAbbreviation(string text)
        {
            var abbreviations = new[] { "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.", "Sr.", "Jr.", "vs.", "etc.", "e.g.", "i.e.", "St.", "No.", "Fig.", "Vol.", "pp.", "ca.", "cf.", "al.", "et." };
            var lower = text.ToLower().Trim();
            foreach (var abbr in abbreviations)
            {
                if (lower.EndsWith(abbr))
                    return true;
            }
            return false;
        }

        public static List<StyleChunk> Retrieve(string query, int topK = 3, float threshold = 0.5f)
        {
            if (!_initialized) Initialize();
            
            var settings = StyleExpandSettings.Instance;
            var selectedStyle = settings?.GetSelectedStyle();
            if (selectedStyle == null) return new List<StyleChunk>();

            if (!_chunksByStyle.TryGetValue(selectedStyle.Name, out var styleChunks) || styleChunks.Count == 0)
            {
                return new List<StyleChunk>();
            }

            var queryEmbedding = VectorClient.GetEmbeddingSync(query);
            if (queryEmbedding == null)
            {
                Logger.Warning("Failed to get query embedding");
                return new List<StyleChunk>();
            }

            var results = new List<(StyleChunk chunk, float similarity)>();

            foreach (var chunk in styleChunks)
            {
                float[] chunkEmbedding;
                
                if (!_chunkEmbeddings.TryGetValue(chunk.ChunkId, out chunkEmbedding))
                {
                    chunkEmbedding = VectorClient.GetEmbeddingSync(chunk.Text);
                    if (chunkEmbedding != null)
                    {
                        _chunkEmbeddings[chunk.ChunkId] = chunkEmbedding;
                    }
                }
                
                if (chunkEmbedding == null) continue;

                var similarity = VectorClient.CosineSimilarity(queryEmbedding, chunkEmbedding);
                
                if (similarity >= threshold)
                {
                    results.Add((chunk, similarity));
                }
            }

            return results
                .OrderByDescending(r => r.similarity)
                .Take(topK)
                .Select(r => r.chunk)
                .ToList();
        }

        public static string RenderQueryTemplate(string template, Pawn pawn)
        {
            if (string.IsNullOrEmpty(template)) return "";
            
            var result = template;
            
            if (pawn != null)
            {
                result = result.Replace("{{ pawn.name }}", pawn.LabelShort ?? "");
                result = result.Replace("{{ pawn.job }}", pawn.CurJob?.def?.label ?? "");
                result = result.Replace("{{ pawn.mood }}", pawn.needs?.mood?.CurLevelPercentage.ToString("P0") ?? "");
                result = result.Replace("{{ pawn.personality }}", GetPersonality(pawn));
                result = result.Replace("{{ pawn.traits }}", GetTraits(pawn));
            }
            
            return result;
        }

        private static string GetPersonality(Pawn pawn)
        {
            var story = pawn.story;
            if (story == null) return "";
            
            var traits = story.traits?.allTraits;
            if (traits == null || traits.Count == 0) return "";
            
            return string.Join(", ", traits.Take(3).Select(t => t.LabelCap));
        }

        private static string GetTraits(Pawn pawn)
        {
            var story = pawn.story;
            if (story == null) return "";
            
            var traits = story.traits?.allTraits;
            if (traits == null || traits.Count == 0) return "";
            
            return string.Join(" ", traits.Take(2).Select(t => t.LabelCap));
        }

        public static string GetStylesPath()
        {
            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return null;
            
            var path = Path.Combine(mod.Content.RootDir, "Styles");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}