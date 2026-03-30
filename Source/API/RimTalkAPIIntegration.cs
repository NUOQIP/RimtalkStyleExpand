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
        private const string ENTRY_NAME = "Style Context";
        
        private static bool _initialized = false;
        private static bool _apiAvailable = false;
        private static bool _configAvailable = false;
        
        private static Assembly _rimTalkAssembly;
        private static Type _promptAPIType;
        private static Type _promptEntryType;
        private static Type _promptRoleType;
        private static Type _promptPositionType;
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
                RegisterPromptEntry();
                
                Log.Message("[StyleExpand] ✓ Integrated via RimTalk API");
                Log.Message("[StyleExpand]   - Registered {{style_name}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_prompt}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_chunks}} variable");
                Log.Message("[StyleExpand]   - Registered {{style_full}} variable");
                Log.Message("[StyleExpand]   - Added PromptEntry: " + ENTRY_NAME);
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
            _promptEntryType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptEntry");
            _promptRoleType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptRole");
            _promptPositionType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptPosition");
            
            if (_promptAPIType == null)
            {
                Log.Message("[StyleExpand] RimTalkPromptAPI not found - old RimTalk version");
                return false;
            }
            
            var registerCtxVar = _promptAPIType.GetMethod("RegisterContextVariable");
            var addEntry = _promptAPIType.GetMethod("AddPromptEntry");
            
            if (registerCtxVar == null || addEntry == null)
            {
                Log.Warning("[StyleExpand] RimTalk API methods not found");
                return false;
            }
            
            Log.Message("[StyleExpand] Detected RimTalk API with PromptEntry support");
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
                    Log.Message($"[StyleExpand] ✓ Registered {{{{{name}}}}} variable");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[StyleExpand] Failed to register {name}: {ex.Message}");
            }
        }
        
        private static void RegisterPromptEntry()
        {
            try
            {
                string entryId = GetDeterministicId(MOD_ID, ENTRY_NAME);
                
                var getPresetMethod = _promptAPIType.GetMethod("GetActivePreset");
                object preset = null;
                if (getPresetMethod != null)
                {
                    preset = getPresetMethod.Invoke(null, null);
                    if (preset != null)
                    {
                        var getEntryMethod = preset.GetType().GetMethod("GetEntry");
                        if (getEntryMethod != null)
                        {
                            var existingEntry = getEntryMethod.Invoke(preset, new object[] { entryId });
                            if (existingEntry != null)
                            {
                                SetProperty(existingEntry, "Content", GetStyleEntryContent());
                                Log.Message($"[StyleExpand] ✓ Updated existing PromptEntry: {ENTRY_NAME}");
                                return;
                            }
                        }
                    }
                }
                
                var entry = Activator.CreateInstance(_promptEntryType);
                if (entry == null)
                {
                    Log.Warning("[StyleExpand] Failed to create PromptEntry instance");
                    return;
                }
                
                SetProperty(entry, "SourceModId", MOD_ID);
                SetProperty(entry, "Name", ENTRY_NAME);
                SetProperty(entry, "Content", GetStyleEntryContent());
                SetProperty(entry, "Enabled", true);
                
                if (_promptRoleType != null)
                {
                    var systemRole = Enum.Parse(_promptRoleType, "System");
                    SetProperty(entry, "Role", systemRole);
                }
                
                if (_promptPositionType != null)
                {
                    var relativePos = Enum.Parse(_promptPositionType, "Relative");
                    SetProperty(entry, "Position", relativePos);
                }
                
                var insertAfterNameMethod = _promptAPIType.GetMethod("InsertPromptEntryAfterName");
                if (insertAfterNameMethod != null)
                {
                    var result = insertAfterNameMethod.Invoke(null, new object[] { entry, "System Prompt" });
                    if (result is bool success)
                    {
                        if (success)
                        {
                            Log.Message($"[StyleExpand] ✓ Inserted PromptEntry after 'System Prompt': {ENTRY_NAME}");
                        }
                        else
                        {
                            Log.Message($"[StyleExpand] ✓ Added PromptEntry at end: {ENTRY_NAME}");
                        }
                    }
                }
                else
                {
                    var addMethod = _promptAPIType.GetMethod("AddPromptEntry");
                    if (addMethod != null)
                    {
                        var result = addMethod.Invoke(null, new[] { entry });
                        if (result is bool success && success)
                        {
                            Log.Message($"[StyleExpand] ✓ Added PromptEntry: {ENTRY_NAME}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[StyleExpand] Failed to register PromptEntry: {ex.Message}");
            }
        }
        
        private static string GetStyleEntryContent()
        {
            return @"---
# Style Instruction

{{style_full}}
---";
        }
        
        private static void SetProperty(object obj, string propertyName, object value)
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
            else
            {
                var field = obj.GetType().GetField(propertyName);
                if (field != null)
                {
                    field.SetValue(obj, value);
                }
            }
        }
        
        private static string GetDeterministicId(string modId, string name)
        {
            if (_promptEntryType != null)
            {
                var generateMethod = _promptEntryType.GetMethod("GenerateDeterministicId",
                    BindingFlags.Public | BindingFlags.Static);
                if (generateMethod != null)
                {
                    try
                    {
                        var result = generateMethod.Invoke(null, new object[] { modId, name });
                        if (result is string id)
                        {
                            return id;
                        }
                    }
                    catch { }
                }
            }
            
            string sanitizedModId = modId.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
            string sanitizedName = name.Replace(" ", "_").Replace("-", "_");
            return $"mod_{sanitizedModId}_{sanitizedName}";
        }
        
        public static void Cleanup()
        {
            if (!_apiAvailable) return;
            
            try
            {
                string entryId = GetDeterministicId(MOD_ID, ENTRY_NAME);
                
                var removeMethod = _promptAPIType?.GetMethod("RemovePromptEntry");
                if (removeMethod != null)
                {
                    removeMethod.Invoke(null, new object[] { entryId });
                    Log.Message($"[StyleExpand] Removed PromptEntry: {entryId}");
                }
                
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