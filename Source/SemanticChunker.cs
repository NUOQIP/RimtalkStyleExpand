using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public enum ChunkingStrategy
    {
        Recursive = 0,
        Semantic = 1,
        Hybrid = 2
    }

    public class SemanticChunker
    {
        private readonly ChunkingConfig _config;
        private readonly VectorApiConfig _vectorApi;

        public SemanticChunker(ChunkingConfig config, VectorApiConfig vectorApi)
        {
            _config = config ?? new ChunkingConfig();
            _vectorApi = vectorApi ?? new VectorApiConfig();
        }

        public List<string> Chunk(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            switch (_config.Strategy)
            {
                case ChunkingStrategy.Semantic:
                    return ChunkSemantic(text);
                case ChunkingStrategy.Hybrid:
                    return ChunkHybrid(text);
                case ChunkingStrategy.Recursive:
                default:
                    return ChunkRecursive(text);
            }
        }

        private List<string> ChunkRecursive(string text)
        {
            return RecursiveSplit(text, _config.TargetChunkLength, _config.MinChunkLength, _config.Overlap);
        }

        private List<string> ChunkSemantic(string text)
        {
            var sentences = SplitIntoSentences(text);
            if (sentences.Count == 0)
                return new List<string>();

            if (sentences.Count == 1)
            {
                return sentences[0].Length <= _config.MaxChunkLength 
                    ? new List<string> { sentences[0] } 
                    : RecursiveSplit(sentences[0], _config.TargetChunkLength, _config.MinChunkLength, _config.Overlap);
            }

            var sentenceEmbeddings = GetSentenceEmbeddings(sentences);
            if (sentenceEmbeddings == null || sentenceEmbeddings.Count != sentences.Count)
            {
                Logger.Warning("SemanticChunker: Failed to get embeddings, falling back to recursive");
                return ChunkRecursive(text);
            }

            var breakpoints = FindBreakpoints(sentenceEmbeddings, _config.BreakpointPercentileThreshold);
            var chunks = CreateChunksFromBreakpoints(sentences, breakpoints);

            chunks = PostProcessChunks(chunks);

            Logger.Message($"SemanticChunker: Created {chunks.Count} chunks from {sentences.Count} sentences with {breakpoints.Count} breakpoints");

            return chunks;
        }

        private List<string> ChunkHybrid(string text)
        {
            var paragraphs = SplitByParagraphs(text);
            if (paragraphs.Count <= 1)
                return ChunkSemantic(text);

            var allChunks = new List<string>();

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                if (paragraph.Length <= _config.TargetChunkLength)
                {
                    allChunks.Add(paragraph.Trim());
                }
                else
                {
                    var subChunks = ChunkSemantic(paragraph);
                    allChunks.AddRange(subChunks);
                }
            }

            allChunks = MergeSmallChunks(allChunks, _config.MinChunkLength);

            return allChunks;
        }

        private List<string> SplitIntoSentences(string text)
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
                            var sentence = current.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(sentence))
                            {
                                sentences.Add(sentence);
                            }
                            current.Clear();
                        }
                    }
                }
            }

            if (current.Length > 0)
            {
                var remaining = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    sentences.Add(remaining);
                }
            }

            return sentences;
        }

        private List<float[]> GetSentenceEmbeddings(List<string> sentences)
        {
            var embeddings = new List<float[]>();
            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 3;

            for (int i = 0; i < sentences.Count; i++)
            {
                var embedding = VectorClient.GetEmbeddingSync(sentences[i], _vectorApi);
                if (embedding == null)
                {
                    Logger.Warning($"SemanticChunker: Failed to get embedding for sentence {i}");
                    embeddings.Add(null);
                    consecutiveFailures++;
                    
                    if (consecutiveFailures >= maxConsecutiveFailures)
                    {
                        Logger.Error($"SemanticChunker: {maxConsecutiveFailures} consecutive failures, aborting semantic chunking");
                        return null;
                    }
                }
                else
                {
                    embeddings.Add(embedding);
                    consecutiveFailures = 0;
                }
                
                if (i < sentences.Count - 1)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }

            var validCount = embeddings.Count(e => e != null);
            if (validCount < embeddings.Count * 0.5)
            {
                Logger.Error("SemanticChunker: Too many failed embeddings");
                return null;
            }

            for (int i = 0; i < embeddings.Count; i++)
            {
                if (embeddings[i] == null)
                {
                    int prev = i - 1;
                    while (prev >= 0 && embeddings[prev] == null) prev--;
                    
                    int next = i + 1;
                    while (next < embeddings.Count && embeddings[next] == null) next++;

                    if (prev >= 0 && next < embeddings.Count)
                    {
                        embeddings[i] = AverageEmbeddings(embeddings[prev], embeddings[next]);
                    }
                    else if (prev >= 0)
                    {
                        embeddings[i] = embeddings[prev];
                    }
                    else if (next < embeddings.Count)
                    {
                        embeddings[i] = embeddings[next];
                    }
                }
            }

            return embeddings;
        }

        private float[] AverageEmbeddings(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return a ?? b;

            var result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = (a[i] + b[i]) / 2f;
            }
            return result;
        }

        private List<int> FindBreakpoints(List<float[]> embeddings, float percentileThreshold)
        {
            if (embeddings.Count < 2)
                return new List<int>();

            var distances = new List<(int index, float distance)>();

            for (int i = 0; i < embeddings.Count - 1; i++)
            {
                var similarity = VectorClient.CosineSimilarity(embeddings[i], embeddings[i + 1]);
                var distance = 1f - similarity;
                distances.Add((i + 1, distance));
            }

            var sortedDistances = distances.Select(d => d.distance).OrderBy(d => d).ToList();
            int percentileIndex = (int)(percentileThreshold / 100f * sortedDistances.Count);
            percentileIndex = Math.Max(0, Math.Min(percentileIndex, sortedDistances.Count - 1));
            float threshold = sortedDistances[percentileIndex];

            var breakpoints = distances
                .Where(d => d.distance >= threshold)
                .Select(d => d.index)
                .OrderBy(i => i)
                .ToList();

            return breakpoints;
        }

        private List<string> CreateChunksFromBreakpoints(List<string> sentences, List<int> breakpoints)
        {
            var chunks = new List<string>();
            var start = 0;

            foreach (var breakpoint in breakpoints)
            {
                if (breakpoint <= start)
                    continue;

                var chunkSentences = new List<string>();
                int currentLength = 0;

                for (int i = start; i < breakpoint && i < sentences.Count; i++)
                {
                    if (currentLength + sentences[i].Length <= _config.MaxChunkLength)
                    {
                        chunkSentences.Add(sentences[i]);
                        currentLength += sentences[i].Length;
                    }
                    else if (chunkSentences.Count > 0)
                    {
                        chunks.Add(string.Join("", chunkSentences));
                        chunkSentences.Clear();
                        currentLength = 0;
                        
                        chunkSentences.Add(sentences[i]);
                        currentLength = sentences[i].Length;
                    }
                    else
                    {
                        var subChunks = RecursiveSplit(sentences[i], _config.TargetChunkLength, _config.MinChunkLength, _config.Overlap);
                        chunks.AddRange(subChunks);
                    }
                }

                if (chunkSentences.Count > 0)
                {
                    chunks.Add(string.Join("", chunkSentences));
                }

                start = breakpoint;
            }

            if (start < sentences.Count)
            {
                var chunkSentences = new List<string>();
                int currentLength = 0;

                for (int i = start; i < sentences.Count; i++)
                {
                    if (currentLength + sentences[i].Length <= _config.MaxChunkLength)
                    {
                        chunkSentences.Add(sentences[i]);
                        currentLength += sentences[i].Length;
                    }
                    else if (chunkSentences.Count > 0)
                    {
                        chunks.Add(string.Join("", chunkSentences));
                        chunkSentences.Clear();
                        currentLength = 0;
                        
                        chunkSentences.Add(sentences[i]);
                        currentLength = sentences[i].Length;
                    }
                    else
                    {
                        var subChunks = RecursiveSplit(sentences[i], _config.TargetChunkLength, _config.MinChunkLength, _config.Overlap);
                        chunks.AddRange(subChunks);
                    }
                }

                if (chunkSentences.Count > 0)
                {
                    chunks.Add(string.Join("", chunkSentences));
                }
            }

            return chunks;
        }

        private List<string> PostProcessChunks(List<string> chunks)
        {
            chunks = MergeSmallChunks(chunks, _config.MinChunkLength);
            chunks = ApplyOverlap(chunks, _config.Overlap);

            return chunks;
        }

        private List<string> MergeSmallChunks(List<string> chunks, int minSize)
        {
            if (chunks.Count <= 1)
                return chunks;

            var result = new List<string>();
            var i = 0;

            while (i < chunks.Count)
            {
                var current = chunks[i];

                while (i + 1 < chunks.Count && current.Length < minSize)
                {
                    if (current.Length + chunks[i + 1].Length <= _config.MaxChunkLength)
                    {
                        i++;
                        current = current + "\n\n" + chunks[i];
                    }
                    else
                    {
                        break;
                    }
                }

                result.Add(current);
                i++;
            }

            return result;
        }

        private List<string> ApplyOverlap(List<string> chunks, int overlap)
        {
            if (overlap <= 0 || chunks.Count <= 1)
                return chunks;

            var result = new List<string>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];

                if (i > 0 && overlap > 0)
                {
                    var prevChunk = chunks[i - 1];
                    var overlapText = GetLastPart(prevChunk, overlap);
                    if (!string.IsNullOrEmpty(overlapText) && !chunk.StartsWith(overlapText))
                    {
                        chunk = overlapText + "..." + chunk;
                    }
                }

                result.Add(chunk);
            }

            return result;
        }

        private List<string> RecursiveSplit(string text, int targetLength, int minSize, int overlap)
        {
            var chunks = new List<string>();
            var paragraphs = SplitByParagraphs(text);

            var currentChunk = new StringBuilder();

            foreach (var paragraph in paragraphs)
            {
                var trimmedPara = paragraph.Trim();
                if (string.IsNullOrEmpty(trimmedPara)) continue;

                if (currentChunk.Length + trimmedPara.Length + 1 <= targetLength)
                {
                    if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                    currentChunk.Append(trimmedPara);
                }
                else
                {
                    if (currentChunk.Length >= minSize)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        var lastPart = GetLastPart(currentChunk.ToString(), overlap);
                        currentChunk.Clear();
                        if (!string.IsNullOrEmpty(lastPart))
                        {
                            currentChunk.Append(lastPart);
                        }
                    }

                    if (trimmedPara.Length > targetLength)
                    {
                        var subChunks = SplitLargeParagraph(trimmedPara, targetLength, minSize, overlap);
                        foreach (var sub in subChunks)
                        {
                            if (currentChunk.Length + sub.Length + 1 <= targetLength && currentChunk.Length > 0)
                            {
                                currentChunk.Append("\n").Append(sub);
                            }
                            else
                            {
                                if (currentChunk.Length >= minSize)
                                {
                                    chunks.Add(currentChunk.ToString().Trim());
                                }
                                currentChunk.Clear();
                                currentChunk.Append(sub);
                            }
                        }
                    }
                    else
                    {
                        if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                        currentChunk.Append(trimmedPara);
                    }
                }
            }

            if (currentChunk.Length >= minSize / 2)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return MergeSmallChunks(chunks, minSize);
        }

        private List<string> SplitByParagraphs(string text)
        {
            return text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                       .Where(p => !string.IsNullOrWhiteSpace(p))
                       .Select(p => p.Trim())
                       .ToList();
        }

        private List<string> SplitLargeParagraph(string paragraph, int targetLength, int minSize, int overlap)
        {
            var result = new List<string>();
            var sentences = SplitIntoSentences(paragraph);

            var current = new StringBuilder();

            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (current.Length + trimmed.Length + 1 <= targetLength)
                {
                    if (current.Length > 0) current.Append("");
                    current.Append(trimmed);
                }
                else
                {
                    if (current.Length >= minSize)
                    {
                        result.Add(current.ToString().Trim());
                        var lastPart = GetLastPart(current.ToString(), overlap);
                        current.Clear();
                        if (!string.IsNullOrEmpty(lastPart))
                        {
                            current.Append(lastPart).Append("");
                        }
                    }

                    if (trimmed.Length > targetLength)
                    {
                        var subParts = SplitLongSentence(trimmed, targetLength);
                        foreach (var part in subParts)
                        {
                            if (current.Length + part.Length <= targetLength && current.Length > 0)
                            {
                                current.Append(part);
                            }
                            else
                            {
                                if (current.Length >= minSize / 2)
                                {
                                    result.Add(current.ToString().Trim());
                                }
                                current.Clear();
                                current.Append(part);
                            }
                        }
                    }
                    else
                    {
                        if (current.Length >= minSize)
                        {
                            result.Add(current.ToString().Trim());
                            current.Clear();
                        }
                        current.Append(trimmed);
                    }
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString().Trim());
            }

            return result;
        }

        private List<string> SplitLongSentence(string sentence, int maxLength)
        {
            var result = new List<string>();
            var clauses = SplitIntoClauses(sentence);

            var current = new StringBuilder();

            foreach (var clause in clauses)
            {
                var trimmed = clause.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (current.Length + trimmed.Length <= maxLength)
                {
                    current.Append(trimmed);
                }
                else
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Clear();
                    }

                    if (trimmed.Length > maxLength)
                    {
                        for (int i = 0; i < trimmed.Length; i += maxLength)
                        {
                            var len = Math.Min(maxLength, trimmed.Length - i);
                            result.Add(trimmed.Substring(i, len));
                        }
                    }
                    else
                    {
                        current.Append(trimmed);
                    }
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString().Trim());
            }

            return result;
        }

        private List<string> SplitIntoClauses(string text)
        {
            var result = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                current.Append(c);

                if (IsClauseEnd(c) && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    if (!IsClauseEnd(next) && next != '"' && next != '"' && next != '"')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private bool IsClauseEnd(char c)
        {
            return c == '，' || c == '、' || c == '；' || c == '：' ||
                   c == ',' || c == ';' || c == ':';
        }

        private string GetLastPart(string text, int length)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= length) return text;

            int start = text.Length - length;
            int sentenceStart = text.LastIndexOf('。', start + length - 1);
            if (sentenceStart > start - 50 && sentenceStart < start + length)
            {
                start = sentenceStart + 1;
            }

            return text.Substring(start).TrimStart();
        }

        private bool IsOpenQuote(char c)
        {
            return c == '"' || c == '"' || c == '「' || c == '『' || c == '"' || c == '『';
        }

        private bool IsCloseQuote(char c)
        {
            return c == '"' || c == '"' || c == '」' || c == '』' || c == '"' || c == '』';
        }

        private bool LooksLikeAbbreviation(string text)
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
    }
}