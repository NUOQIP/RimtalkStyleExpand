using System;
using System.IO;
using System.Net;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public static class LLMClient
    {
        private const int MAX_RETRIES = 3;
        private const int BASE_RETRY_DELAY_MS = 1000;

        public static string GenerateStylePrompt(string styleName, string sampleText, LlmApiConfig config)
        {
            if (string.IsNullOrEmpty(sampleText))
            {
                throw new Exception("Sample text is empty");
            }

            var prompt = BuildAnalysisPrompt(styleName, sampleText);
            
            string url;
            string apiKey;
            string model;

            if (config.UseRimTalkApi)
            {
                var rimTalkConfig = RimTalkAPIIntegration.GetRimTalkActiveConfig(null);
                url = rimTalkConfig.url;
                apiKey = rimTalkConfig.apiKey;
                model = rimTalkConfig.model;
            }
            else
            {
                url = config.Url;
                apiKey = config.ApiKey;
                model = config.Model;
            }

            if (!url.Contains("/v1/") && !url.Contains("/api/") && !url.Contains(":11434"))
            {
                url = url.TrimEnd('/') + "/v1/chat/completions";
            }

            return CallLLMApi(prompt, url, apiKey, model);
        }

        public static bool TestConnection(LlmApiConfig config)
        {
            string url;
            string apiKey;
            string model;

            if (config.UseRimTalkApi)
            {
                var rimTalkConfig = RimTalkAPIIntegration.GetRimTalkActiveConfig(null);
                url = rimTalkConfig.url;
                apiKey = rimTalkConfig.apiKey;
                model = rimTalkConfig.model;
            }
            else
            {
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.Model))
                {
                    throw new Exception("URL or Model not configured");
                }
                url = config.Url;
                apiKey = config.ApiKey;
                model = config.Model;
            }

            if (!url.Contains("/v1/") && !url.Contains("/api/") && !url.Contains(":11434"))
            {
                url = url.TrimEnd('/') + "/v1/chat/completions";
            }

            var result = CallLLMApi("Hello", url, apiKey, model);
            return !string.IsNullOrEmpty(result);
        }

private static string BuildAnalysisPrompt(string styleName, string sampleText)
        {
            var sampledText = SampleTextSegments(sampleText, 0.1f);
            
            return $@"You are a writing style guide writer. Your task is to create a practical style guide that instructs others how to write in the style of ""{styleName}"".

【Style Name】
{styleName}

【Requirements】
- Write in imperative, instructional tone (e.g., ""Use short sentences"" not ""The style uses short sentences"")
- Focus only on HOW to write, not WHAT to write about
- Identify the most distinctive style dimensions from the sample
- Provide concrete, actionable instructions

【Forbidden】
Do not quote passages, discuss characters, scenes, plot, or settings

【Output】
- A practical style guide within 500 words
- Use Markdown formatting if helpful
- Write in the same language as the sample text

【Sample Text】
{sampledText}";
        }
        
        private static string SampleTextSegments(string text, float ratio)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            var totalLength = text.Length;
            var sampleLength = (int)(totalLength * ratio);
            
            if (sampleLength >= totalLength) return text;
            if (sampleLength < 1000) return text.Length <= 1500 ? text : text.Substring(0, Math.Min(1500, totalLength));
            
            var segmentLength = sampleLength / 3;
            
            var startSegment = text.Substring(0, segmentLength);
            
            var midStart = totalLength / 2 - segmentLength / 2;
            var midSegment = text.Substring(midStart, segmentLength);
            
            var endStart = Math.Max(0, totalLength - segmentLength);
            var endSegment = text.Substring(endStart, segmentLength);
            
            return $"[开头部分]\n{startSegment}\n\n[中间部分]\n{midSegment}\n\n[结尾部分]\n{endSegment}";
        }

        private static string CallLLMApi(string prompt, string url, string apiKey, string model)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Timeout = 60000;

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.Headers.Add($"Authorization: Bearer {apiKey}");
                    }

                    var isOllama = url.Contains("ollama") || url.Contains(":11434");
                    var requestBody = isOllama
                        ? $"{{\"model\":\"{model}\",\"prompt\":\"{EscapeJsonString(prompt)}\",\"stream\":false}}"
                        : $"{{\"model\":\"{model}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{EscapeJsonString(prompt)}\"}}]}}";

                    var bytes = Encoding.UTF8.GetBytes(requestBody);
                    request.ContentLength = bytes.Length;

                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        var jsonResponse = reader.ReadToEnd();
                        var result = ParseLLMResponse(jsonResponse, isOllama);
                        if (string.IsNullOrEmpty(result))
                        {
                            Logger.Warning($"Failed to parse LLM response. Response: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}");
                        }
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    var shouldRetry = ShouldRetry(ex);
                    
                    if (attempt >= MAX_RETRIES || !shouldRetry)
                    {
                        Logger.Error($"LLM API failed after {attempt} attempts: {ex.Message}");
                        throw;
                    }
                    
                    var delay = BASE_RETRY_DELAY_MS * attempt;
                    Logger.Warning($"LLM API failed (attempt {attempt}), retrying in {delay}ms: {ex.Message}");
                    System.Threading.Thread.Sleep(delay);
                }
            }
            
            return null;
        }
        
        private static bool ShouldRetry(WebException ex)
        {
            if (ex.Response is HttpWebResponse response)
            {
                var code = (int)response.StatusCode;
                return code == 429 || code == 503 || code == 502 || code == 504;
            }
            return ex.Status == WebExceptionStatus.Timeout || 
                   ex.Status == WebExceptionStatus.ConnectionClosed ||
                   ex.Status == WebExceptionStatus.ConnectFailure;
        }

        private static string ParseLLMResponse(string json, bool isOllama)
        {
            try
            {
                if (isOllama)
                {
                    var responseKey = "\"response\":";
                    var responseIndex = json.IndexOf(responseKey);
                    if (responseIndex >= 0)
                    {
                        var start = json.IndexOf('"', responseIndex + responseKey.Length);
                        if (start >= 0)
                        {
                            var end = start + 1;
                            while (end < json.Length)
                            {
                                if (json[end] == '\\' && end + 1 < json.Length)
                                {
                                    end += 2;
                                    continue;
                                }
                                if (json[end] == '"')
                                {
                                    break;
                                }
                                end++;
                            }
                            var result = json.Substring(start + 1, end - start - 1);
                            return UnescapeJsonString(result);
                        }
                    }
                }
                else
                {
                    var contentKey = "\"content\":";
                    var contentIndex = json.IndexOf(contentKey);
                    if (contentIndex >= 0)
                    {
                        var start = json.IndexOf('"', contentIndex + contentKey.Length);
                        if (start >= 0)
                        {
                            var end = start + 1;
                            while (end < json.Length)
                            {
                                if (json[end] == '\\' && end + 1 < json.Length)
                                {
                                    end += 2;
                                    continue;
                                }
                                if (json[end] == '"')
                                {
                                    break;
                                }
                                end++;
                            }
                            var result = json.Substring(start + 1, end - start - 1);
                            return UnescapeJsonString(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to parse LLM response: {ex.Message}");
            }

            return "";
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

        private static string UnescapeJsonString(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    var next = s[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        default: sb.Append(next); i++; break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }
    }
}