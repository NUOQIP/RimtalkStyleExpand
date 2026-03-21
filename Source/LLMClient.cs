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
        private static Type _settingsType;
        private static MethodInfo _getSettingsMethod;
        private static PropertyInfo _localConfigProperty;
        private static PropertyInfo _useCloudProvidersProperty;
        private static PropertyInfo _useSimpleConfigProperty;
        private static PropertyInfo _simpleApiKeyProperty;
        private static Type _apiConfigType;
        private static PropertyInfo _baseUrlProperty;
        private static PropertyInfo _apiKeyProperty;
        private static PropertyInfo _selectedModelProperty;
        private static PropertyInfo _customModelNameProperty;
        private static bool _rimTalkResolved = false;

        public static string GenerateStylePrompt(string styleName, string sampleText, LlmApiConfig config)
        {
            if (string.IsNullOrEmpty(sampleText))
            {
                throw new Exception("Sample text is empty");
            }

            var prompt = BuildAnalysisPrompt(styleName, sampleText);
            
            if (config.UseRimTalkApi)
            {
                var (url, apiKey, model) = GetRimTalkLlmConfig(config.Model);
                return CallLLMApi(prompt, url, apiKey, model);
            }
            else
            {
                return CallLLMApi(prompt, config.Url, config.ApiKey, config.Model);
            }
        }

        public static bool TestConnection(LlmApiConfig config)
        {
            if (config.UseRimTalkApi)
            {
                var (url, apiKey, model) = GetRimTalkLlmConfig(config.Model);
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
                {
                    throw new Exception("RimTalk local provider URL or Model not configured");
                }
                var result = CallLLMApi("Hello", url, apiKey, model);
                return !string.IsNullOrEmpty(result);
            }
            else
            {
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.Model))
                {
                    throw new Exception("URL or Model not configured");
                }
                var result = CallLLMApi("Hello", config.Url, config.ApiKey, config.Model);
                return !string.IsNullOrEmpty(result);
            }
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

                _settingsType = _rimTalkAssembly.GetType("RimTalk.Settings");
                if (_settingsType == null) return;

                _getSettingsMethod = _settingsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (_getSettingsMethod == null) return;

                var rimTalkSettingsType = _rimTalkAssembly.GetType("RimTalk.RimTalkSettings");
                if (rimTalkSettingsType == null) return;

                _localConfigProperty = rimTalkSettingsType.GetProperty("LocalConfig");
                _useCloudProvidersProperty = rimTalkSettingsType.GetProperty("UseCloudProviders");
                _useSimpleConfigProperty = rimTalkSettingsType.GetProperty("UseSimpleConfig");
                _simpleApiKeyProperty = rimTalkSettingsType.GetProperty("SimpleApiKey");

                _apiConfigType = _rimTalkAssembly.GetType("RimTalk.ApiConfig");
                if (_apiConfigType == null) return;

                _baseUrlProperty = _apiConfigType.GetProperty("BaseUrl");
                _apiKeyProperty = _apiConfigType.GetProperty("ApiKey");
                _selectedModelProperty = _apiConfigType.GetProperty("SelectedModel");
                _customModelNameProperty = _apiConfigType.GetProperty("CustomModelName");
            }
            catch
            {
            }
        }

        private static (string url, string apiKey, string model) GetRimTalkLlmConfig(string overrideModel)
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

            bool useSimpleConfig = (bool)(_useSimpleConfigProperty?.GetValue(settingsInstance) ?? true);
            bool useCloudProviders = (bool)(_useCloudProvidersProperty?.GetValue(settingsInstance) ?? true);

            if (useSimpleConfig)
            {
                throw new Exception("RimTalk is using Simple Mode (Google Gemini). Please switch to Advanced Mode with Local provider for LLM style generation.");
            }

            if (useCloudProviders)
            {
                throw new Exception("RimTalk is using Cloud providers. Please switch to Local provider (Ollama) for LLM style generation.");
            }

            var localConfig = _localConfigProperty?.GetValue(settingsInstance);
            if (localConfig == null)
            {
                throw new Exception("RimTalk local config is not set");
            }

            string url = _baseUrlProperty?.GetValue(localConfig) as string ?? "";
            string apiKey = _apiKeyProperty?.GetValue(localConfig) as string ?? "";
            
            string model = overrideModel;
            if (string.IsNullOrEmpty(model))
            {
                model = _selectedModelProperty?.GetValue(localConfig) as string ?? "";
                if (model == "Custom" || string.IsNullOrEmpty(model))
                {
                    model = _customModelNameProperty?.GetValue(localConfig) as string ?? "";
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                throw new Exception("RimTalk local provider BaseUrl is not configured");
            }

            return (url, apiKey, model);
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