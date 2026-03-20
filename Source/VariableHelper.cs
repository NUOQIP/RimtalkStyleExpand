using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;

namespace RimTalkStyleExpand
{
    public static class VariableHelper
    {
        private static Dictionary<string, List<(string name, string description)>> _cachedVariables;
        private static Assembly _rimTalkAssembly;

        private static Assembly GetRimTalkAssembly()
        {
            if (_rimTalkAssembly == null)
            {
                _rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
            }
            return _rimTalkAssembly;
        }

        public static Dictionary<string, List<(string name, string description)>> GetBuiltinVariables()
        {
            if (_cachedVariables != null) return _cachedVariables;
            
            var assembly = GetRimTalkAssembly();
            if (assembly == null)
            {
                _cachedVariables = GetFallbackVariables();
                return _cachedVariables;
            }
            
            try
            {
                var variableDefsType = assembly.GetType("RimTalk.Prompt.VariableDefinitions");
                var getMethod = variableDefsType?.GetMethod("GetScribanVariables", BindingFlags.Public | BindingFlags.Static);
                
                if (getMethod != null)
                {
                    var result = getMethod.Invoke(null, null);
                    _cachedVariables = ConvertDictionaryResult(result);
                    if (_cachedVariables.Count > 0) return _cachedVariables;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to get builtin variables: {ex.Message}");
            }
            
            _cachedVariables = GetFallbackVariables();
            return _cachedVariables;
        }

        public static List<(string name, string description, string category)> GetFlattenedVariables()
        {
            var result = new List<(string, string, string)>();
            foreach (var category in GetBuiltinVariables())
            {
                foreach (var v in category.Value)
                {
                    result.Add((v.name, v.description, category.Key));
                }
            }
            return result;
        }

        public static List<(string name, string description)> GetPawnVariables()
        {
            var result = new List<(string, string)>();
            foreach (var category in GetBuiltinVariables())
            {
                foreach (var v in category.Value)
                {
                    if (v.name.StartsWith("pawn.") && !v.name.StartsWith("pawn.memory"))
                    {
                        result.Add((v.name, v.description));
                    }
                }
            }
            return result;
        }

        public static bool TryGetPawnPropertyValue(string propertyName, Pawn pawn, out string value)
        {
            value = null;
            if (pawn == null || string.IsNullOrEmpty(propertyName)) return false;
            
            var assembly = GetRimTalkAssembly();
            if (assembly == null) return false;
            
            try
            {
                var hookType = assembly.GetType("RimTalk.API.ContextHookRegistry");
                if (hookType != null)
                {
                    var tryGetMethod = hookType.GetMethod("TryGetPawnVariable", BindingFlags.Public | BindingFlags.Static);
                    if (tryGetMethod != null)
                    {
                        var parameters = new object[] { propertyName, pawn, null };
                        if ((bool)tryGetMethod.Invoke(null, parameters))
                        {
                            value = parameters[2] as string;
                            if (!string.IsNullOrEmpty(value)) return true;
                        }
                    }
                }
                
                var parserType = assembly.GetType("RimTalk.Prompt.ScribanParser");
                var contextType = assembly.GetType("RimTalk.Prompt.PromptContext");
                
                if (parserType != null && contextType != null)
                {
                    var ctx = Activator.CreateInstance(contextType, new object[] { pawn, null });
                    string template = "{{" + propertyName + "}}";
                    
                    var renderMethod = parserType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                    if (renderMethod != null)
                    {
                        var renderResult = renderMethod.Invoke(null, new object[] { template, ctx, false });
                        string parsed = renderResult as string;
                        if (!string.IsNullOrEmpty(parsed) && parsed != template)
                        {
                            value = parsed;
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearCache()
        {
            _cachedVariables = null;
            _rimTalkAssembly = null;
        }

        private static Dictionary<string, List<(string, string)>> ConvertDictionaryResult(object result)
        {
            var converted = new Dictionary<string, List<(string, string)>>();
            if (result == null) return converted;
            
            try
            {
                if (result is Dictionary<string, List<(string, string)>> typedResult)
                {
                    return typedResult;
                }
                
                if (result is System.Collections.IDictionary dict)
                {
                    foreach (var key in dict.Keys)
                    {
                        string keyStr = key?.ToString() ?? "";
                        if (string.IsNullOrEmpty(keyStr)) continue;
                        
                        var dictValue = dict[key];
                        if (dictValue is System.Collections.IEnumerable list)
                        {
                            var tuples = new List<(string, string)>();
                            foreach (var item in list)
                            {
                                var itemType = item.GetType();
                                string item1 = itemType.GetField("Item1")?.GetValue(item)?.ToString() ?? "";
                                string item2 = itemType.GetField("Item2")?.GetValue(item)?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(item1))
                                {
                                    tuples.Add((item1, item2));
                                }
                            }
                            if (tuples.Count > 0)
                            {
                                converted[keyStr] = tuples;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to convert result: {ex.Message}");
            }
            
            return converted;
        }

        private static Dictionary<string, List<(string, string)>> GetFallbackVariables()
        {
            return new Dictionary<string, List<(string, string)>>
            {
                ["Pawn"] = new List<(string, string)>
                {
                    ("pawn.name", "Name"),
                    ("pawn.backstory", "Backstory"),
                    ("pawn.traits", "Traits"),
                    ("pawn.mood", "Mood"),
                    ("pawn.job", "Current job"),
                    ("pawn.health", "Health"),
                    ("pawn.skills", "Skills"),
                    ("pawn.personality", "Personality")
                },
                ["Context"] = new List<(string, string)>
                {
                    ("hour", "Current hour"),
                    ("season", "Current season"),
                    ("weather", "Current weather")
                }
            };
        }
    }
}