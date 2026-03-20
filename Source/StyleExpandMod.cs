using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimTalkStyleExpand
{
    public class StyleExpandMod : Mod
    {
        public static StyleExpandMod Instance { get; private set; }
        public static StyleExpandSettings Settings { get; private set; }

        public StyleExpandMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<StyleExpandSettings>();
            
            var harmony = new Harmony("RimTalk.StyleExpand");
            harmony.PatchAll();
            
            LongEventHandler.ExecuteWhenFinished(OnGameLoaded);
        }

        private void OnGameLoaded()
        {
            StyleRetriever.Initialize();
            RegisterStyleVariables();
            StyleWatcher.Start();
            Logger.Message("Mod loaded successfully");
        }

        private void RegisterStyleVariables()
        {
            try
            {
                var apiType = Type.GetType("RimTalk.API.RimTalkPromptAPI, RimTalk");
                if (apiType == null)
                {
                    Logger.Warning("RimTalkPromptAPI not found, skipping variable registration");
                    return;
                }

                var registerMethod = apiType.GetMethod("RegisterContextVariable");
                if (registerMethod != null)
                {
                    RegisterVariable(registerMethod, "style_base_prompt", ctx => PromptBuilder.GetBasePrompt(), "Base style instruction prompt");
                    RegisterVariable(registerMethod, "style_prompt", ctx => PromptBuilder.GetStylePromptSection(), "Style description prompt");
                    RegisterVariable(registerMethod, "style_chunks", ctx => PromptBuilder.GetRetrievedChunksSection(ctx), "Retrieved style example chunks");
                    RegisterVariable(registerMethod, "style_name", ctx => GetActiveStyleName(), "Active style name");
                    RegisterVariable(registerMethod, "style_full", ctx => PromptBuilder.GetFullStylePrompt(ctx), "Complete style prompt (base + description + chunks)");
                    
                    Logger.Message("Style variables registered with RimTalk");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to register style variables: {ex.Message}");
            }
        }

        private void RegisterVariable(MethodInfo registerMethod, string varName, Func<object, string> provider, string description)
        {
            registerMethod.Invoke(null, new object[] { 
                "RimTalk.StyleExpand", 
                varName, 
                provider, 
                description, 
                100 
            });
        }

        private static string GetActiveStyleName()
        {
            var selectedStyle = Settings.GetSelectedStyle();
            return selectedStyle?.Name ?? "";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }

        public override string SettingsCategory()
        {
            return "StyleExpand_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            try
            {
                if (Settings == null)
                {
                    Settings = GetSettings<StyleExpandSettings>();
                }
                
                if (Settings == null)
                {
                    GUI.color = Color.red;
                    Widgets.Label(inRect, "Failed to load settings!");
                    GUI.color = Color.white;
                    return;
                }
                
                SettingsWindow.DoSettingsContents(inRect, Settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[StyleExpand] Error in DoSettingsWindowContents: {ex.Message}\n{ex.StackTrace}");
                GUI.color = Color.red;
                Widgets.Label(inRect, $"Error: {ex.Message}");
                GUI.color = Color.white;
            }
        }
    }
}