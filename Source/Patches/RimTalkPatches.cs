using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalkStyleExpand
{
    [HarmonyPatch]
    public static class RimTalkPatches
    {
        private static Type _talkRequestType;
        private static Type _promptManagerType;
        private static PropertyInfo _initiatorProperty;

        static RimTalkPatches()
        {
            _talkRequestType = Type.GetType("RimTalk.Data.TalkRequest, RimTalk");
            _promptManagerType = Type.GetType("RimTalk.Prompt.PromptManager, RimTalk");
            
            if (_talkRequestType != null)
            {
                _initiatorProperty = _talkRequestType.GetProperty("Initiator");
            }
        }

        public static bool IsRimTalkAvailable()
        {
            return _talkRequestType != null && _promptManagerType != null;
        }

        [HarmonyPatch("RimTalk.Prompt.PromptManager", "BuildMessages")]
        [HarmonyPrefix]
        public static void BuildMessagesPrefix(object __instance, object talkRequest, List<Pawn> pawns, string status)
        {
            if (!StyleExpandSettings.Instance?.IsEnabled ?? true) return;
            
            if (talkRequest == null || _initiatorProperty == null) return;
            
            var initiator = _initiatorProperty?.GetValue(talkRequest) as Pawn;
            if (initiator == null) return;

            var stylePrompt = PromptBuilder.BuildStylePrompt(initiator);
            if (string.IsNullOrEmpty(stylePrompt)) return;

            var stylePromptField = _talkRequestType?.GetField("StylePrompt");
            if (stylePromptField != null)
            {
                stylePromptField.SetValue(talkRequest, stylePrompt);
            }
        }

        [HarmonyPatch("RimTalk.Prompt.PromptManager", "BuildMessages")]
        [HarmonyPostfix]
        public static void BuildMessagesPostfix(ref object __result, object talkRequest)
        {
            if (!StyleExpandSettings.Instance?.IsEnabled ?? true) return;
            
            var stylePromptField = _talkRequestType?.GetField("StylePrompt");
            if (stylePromptField == null) return;
            
            var stylePrompt = stylePromptField.GetValue(talkRequest) as string;
            if (string.IsNullOrEmpty(stylePrompt)) return;

            if (__result == null) return;

            try
            {
                var resultType = __result.GetType();
                if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var itemType = resultType.GetGenericArguments()[0];
                    
                    var list = __result;
                    var count = (int)resultType.GetProperty("Count").GetValue(list);
                    
                    for (int i = 0; i < count; i++)
                    {
                        var item = resultType.GetProperty("Item").GetValue(list, new object[] { i });
                        var tupleType = item.GetType();
                        var roleField = tupleType.GetField("Item1");
                        var contentField = tupleType.GetField("Item2");
                        
                        if (roleField != null && contentField != null)
                        {
                            var role = roleField.GetValue(item);
                            var roleValue = Convert.ToInt32(role);
                            
                            if (roleValue == 0) // System role
                            {
                                var content = contentField.GetValue(item) as string;
                                var newContent = PromptBuilder.InjectIntoSystemPrompt(content ?? "", stylePrompt);
                                
                                var newItem = Activator.CreateInstance(tupleType, role, newContent);
                                resultType.GetProperty("Item").SetValue(list, newItem, new object[] { i });
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error injecting style prompt: {ex.Message}");
            }
        }
    }
}