using System;
using System.Reflection;
using Verse;

namespace RimTalkStyleExpand
{
    /// <summary>
    /// RimTalk API 集成入口
    /// 自动注册文风相关变量到 RimTalk
    ///
    /// 注册变量:
    /// - {{style_name}} - 当前文风名称
    /// - {{style_prompt}} - 文风提示词
    /// - {{style_chunks}} - 检索到的示例片段
    /// - {{style_full}} - 完整文风提示词
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkAPIIntegration
    {
        private const string MOD_ID = "RimTalk.StyleExpand";
        
        private static bool _initialized = false;
        private static bool _apiAvailable = false;
        
        private static Assembly _rimTalkAssembly;
        private static Type _promptAPIType;
        
        public static bool IsApiAvailable => _apiAvailable;
        
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
    }
}