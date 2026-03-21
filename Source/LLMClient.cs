using System;
using System.IO;
using System.Net;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public static class LLMClient
    {
        public static string GenerateStylePrompt(string styleName, string sampleText, LlmApiConfig config)
        {
            if (string.IsNullOrEmpty(sampleText))
            {
                throw new Exception("Sample text is empty");
            }

            var prompt = BuildAnalysisPrompt(styleName, sampleText);
            return CallLLMApi(prompt, config);
        }

        private static string BuildAnalysisPrompt(string styleName, string sampleText)
        {
            var truncatedSample = sampleText.Length > 3000 ? sampleText.Substring(0, 3000) + "..." : sampleText;
            
            return $@"Analyze the writing style of this text and create a style guide for '{styleName}'.

IMPORTANT: Focus ONLY on macro-level writing style strategies. Ignore specific characters, scenes, plot elements, and settings mentioned in the text. Extract the generalizable style patterns that can be applied to ANY content.

Analyze these aspects:
- Tone and emotional expression (formal/casual, warm/cold, serious/humorous, etc.)
- Vocabulary patterns (archaic/modern, simple/ornate, specific word choices)
- Sentence structure (short/long, simple/complex, rhetorical devices used)
- Characteristic expressions or catchphrases

Output format:
1. Style summary (1-2 sentences describing the overall style)
2. Key characteristics (2-4 bullet points with specific traits)
3. Example phrases (1-2 typical expressions if identifiable)

Write in the same language as the sample text.

Sample text:
{truncatedSample}

Style guide:";
        }

        private static string CallLLMApi(string prompt, LlmApiConfig config)
        {
            var request = (HttpWebRequest)WebRequest.Create(config.Url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 60000;

            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            }

            var isOllama = config.Url.Contains("ollama") || config.Url.Contains(":11434");
            var requestBody = isOllama
                ? $"{{\"model\":\"{config.Model}\",\"prompt\":\"{EscapeJsonString(prompt)}\",\"stream\":false}}"
                : $"{{\"model\":\"{config.Model}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{EscapeJsonString(prompt)}\"}}]}}";

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
                return ParseLLMResponse(jsonResponse, isOllama);
            }
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