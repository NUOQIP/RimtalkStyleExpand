using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Verse;

namespace RimTalkStyleExpand
{
    public static class LLMClient
    {
        private static Assembly _rimTalkAssembly;
        private static MethodInfo _getSettingsMethod;
        private static bool _rimTalkResolved = false;

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
                var rimTalkConfig = GetRimTalkActiveConfig(null);
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

            // 自动补充 API 端点路径
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
                var rimTalkConfig = GetRimTalkActiveConfig(null);
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

            // 自动补充 API 端点路径
            if (!url.Contains("/v1/") && !url.Contains("/api/") && !url.Contains(":11434"))
            {
                url = url.TrimEnd('/') + "/v1/chat/completions";
            }

            var result = CallLLMApi("Hello", url, apiKey, model);
            return !string.IsNullOrEmpty(result);
        }

        private static void ResolveRimTalkTypes()
        {
            if (_rimTalkResolved) return;
            _rimTalkResolved = true;

            try
            {
                _rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (_rimTalkAssembly == null) return;

                var settingsType = _rimTalkAssembly.GetType("RimTalk.Settings");
                if (settingsType == null) return;

                _getSettingsMethod = settingsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            }
            catch
            {
            }
        }

        private static (string url, string apiKey, string model) GetRimTalkActiveConfig(string overrideModel)
        {
            ResolveRimTalkTypes();

            if (_getSettingsMethod == null)
            {
                throw new Exception("RimTalk not found");
            }

            var settingsInstance = _getSettingsMethod.Invoke(null, null);
            if (settingsInstance == null)
            {
                throw new Exception("RimTalk settings not available");
            }

            var getActiveConfigMethod = settingsInstance.GetType().GetMethod("GetActiveConfig");
            if (getActiveConfigMethod == null)
            {
                throw new Exception("RimTalk GetActiveConfig method not found");
            }

            var activeConfig = getActiveConfigMethod.Invoke(settingsInstance, null);
            if (activeConfig == null)
            {
                throw new Exception("RimTalk has no active API configuration. Please configure RimTalk first.");
            }

            var configType = activeConfig.GetType();
            
            var apiKeyField = configType.GetField("ApiKey");
            var baseUrlField = configType.GetField("BaseUrl");
            var providerField = configType.GetField("Provider");
            var selectedModelField = configType.GetField("SelectedModel");
            var customModelNameField = configType.GetField("CustomModelName");

            string apiKey = apiKeyField?.GetValue(activeConfig) as string ?? "";
            string baseUrl = baseUrlField?.GetValue(activeConfig) as string ?? "";
            string provider = providerField?.GetValue(activeConfig)?.ToString() ?? "";
            
            string model = overrideModel;
            if (string.IsNullOrEmpty(model))
            {
                model = selectedModelField?.GetValue(activeConfig) as string ?? "";
                if (model == "Custom" || string.IsNullOrEmpty(model))
                {
                    model = customModelNameField?.GetValue(activeConfig) as string ?? "";
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                if (provider == "OpenAI")
                {
                    baseUrl = "https://api.openai.com/v1/chat/completions";
                }
                else if (provider == "DeepSeek")
                {
                    baseUrl = "https://api.deepseek.com/v1/chat/completions";
                }
                else if (provider == "Google")
                {
                    baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=API_KEY_PLACEHOLDER";
                }
                else if (provider == "Player2")
                {
                    baseUrl = "https://api.player2.live/v1/chat/completions";
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new Exception("RimTalk API URL is not configured");
            }

            // 自动补充 API 端点路径
            if (!baseUrl.Contains("/v1/") && !baseUrl.Contains("/api/") && !baseUrl.Contains(":11434"))
            {
                baseUrl = baseUrl.TrimEnd('/') + "/v1/chat/completions";
            }

            if (string.IsNullOrEmpty(model))
            {
                model = "gpt-3.5-turbo";
            }

            Logger.Message($"Using RimTalk config - Provider: {provider}, Model: {model}, URL: {baseUrl}");

            return (baseUrl, apiKey, model);
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

Write in the same language as the sample text.

Sample text:
{truncatedSample}

Style guide:";
        }

        private static string CallLLMApi(string prompt, string url, string apiKey, string model)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 60000;

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
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