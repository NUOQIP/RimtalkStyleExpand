using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimTalkStyleExpand
{
    [StaticConstructorOnStartup]
    public static class RimTalkAPIIntegration
    {
        private const string MOD_ID = "RimTalk.StyleExpand";
        
        private static bool _initialized = false;
        private static bool _apiAvailable = false;
        private static bool _configAvailable = false;
        
        private static Assembly _rimTalkAssembly;
        private static Type _promptAPIType;
        private static Type _settingsType;
        private static MethodInfo _getSettingsMethod;
        
        public static bool IsApiAvailable => _apiAvailable;
        public static bool IsConfigAvailable => _configAvailable;
        
        static RimTalkAPIIntegration()
        {
            LongEventHandler.ExecuteWhenFinished(Initialize);
        }
        
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            try
            {
                if (!DetectAPI())
                {
                    Log.Message("[StyleExpand] RimTalk API not detected, using manual integration");
                    return;
                }
                
                _apiAvailable = true;
                RegisterVariables();
                
                Log.Message("[StyleExpand] Integrated via RimTalk API");
                Log.Message("[StyleExpand]   - Registered {{style_base_prompt}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_name}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_prompt}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_chunks}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_full}} variable");
            }
            catch (Exception ex)
            {
                Log.Error($"[StyleExpand] API integration failed: {ex.Message}");
                _apiAvailable = false;
            }
        }
        
        private static bool DetectAPI()
        {
            _rimTalkAssembly = GetRimTalkAssembly();
            if (_rimTalkAssembly == null)
            {
                Log.Warning("[StyleExpand] RimTalk assembly not found");
                return false;
            }
            
            _promptAPIType = _rimTalkAssembly.GetType("RimTalk.API.RimTalkPromptAPI");
            if (_promptAPIType == null)
            {
                Log.Message("[StyleExpand] RimTalkPromptAPI not found - old RimTalk version");
                return false;
            }
            
            var registerCtxVar = _promptAPIType.GetMethod("RegisterContextVariable");
            if (registerCtxVar == null)
            {
                Log.Warning("[StyleExpand] RegisterContextVariable method not found");
                return false;
            }
            
            Log.Message("[StyleExpand] Detected RimTalk API");
            return true;
        }
        
        private static Assembly GetRimTalkAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "RimTalk")
                {
                    return assembly;
                }
            }
            return null;
        }
        
        private static void RegisterVariables()
        {
            var registerCtxVar = _promptAPIType.GetMethod("RegisterContextVariable");
            if (registerCtxVar == null) return;
            
            RegisterContextVariable(registerCtxVar, "style_base_prompt",
                new Func<object, string>(StyleVariableProvider.GetBasePrompt),
                "Base style instruction prompt", 99);
            
            RegisterContextVariable(registerCtxVar, "style_name",
                new Func<object, string>(StyleVariableProvider.GetStyleName),
                "Current style name", 100);
            
            RegisterContextVariable(registerCtxVar, "style_prompt",
                new Func<object, string>(StyleVariableProvider.GetStylePrompt),
                "Style description/prompt", 101);
            
            RegisterContextVariable(registerCtxVar, "style_chunks",
                new Func<object, string>(StyleVariableProvider.GetStyleChunks),
                "Retrieved style example chunks", 102);
            
            RegisterContextVariable(registerCtxVar, "style_full",
                new Func<object, string>(StyleVariableProvider.GetStyleFull),
                "Complete style prompt with all components", 103);
        }
        
        private static void RegisterContextVariable(MethodInfo registerMethod, string name, 
            Func<object, string> provider, string description, int priority)
        {
            try
            {
                registerMethod.Invoke(null, new object[] { MOD_ID, name, provider, description, priority });
                if (Prefs.DevMode)
                {
                    Log.Message($"[StyleExpand] Registered {{{{{name}}}}} variable");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[StyleExpand] Failed to register {name}: {ex.Message}");
            }
        }
        
        public static void Cleanup()
        {
            if (!_apiAvailable) return;
            
            try
            {
                var unregisterMethod = _promptAPIType?.GetMethod("UnregisterAllHooks");
                if (unregisterMethod != null)
                {
                    unregisterMethod.Invoke(null, new object[] { MOD_ID });
                }
                Log.Message("[StyleExpand] Cleaned up RimTalk API registrations");
            }
            catch (Exception ex)
            {
                Log.Warning($"[StyleExpand] Cleanup failed: {ex.Message}");
            }
        }
        
        #region RimTalk Config Access
        
        public static (string url, string apiKey, string model) GetRimTalkActiveConfig(string overrideModel = null)
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
                baseUrl = GetDefaultUrl(provider);
            }
            
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new Exception("RimTalk API URL is not configured");
            }
            
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
        
        private static string GetDefaultUrl(string provider)
        {
            switch (provider)
            {
                case "OpenAI": return "https://api.openai.com/v1/chat/completions";
                case "DeepSeek": return "https://api.deepseek.com/v1/chat/completions";
                case "Google": return "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=API_KEY_PLACEHOLDER";
                case "Player2": return "https://api.player2.live/v1/chat/completions";
                default: return "";
            }
        }
        
        private static void ResolveRimTalkTypes()
        {
            if (_configAvailable) return;
            
            try
            {
                _rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (_rimTalkAssembly == null) return;
                
                _settingsType = _rimTalkAssembly.GetType("RimTalk.Settings");
                if (_settingsType == null) return;
                
                _getSettingsMethod = _settingsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                _configAvailable = _getSettingsMethod != null;
            }
            catch
            {
            }
        }
        
        #endregion
    }
}