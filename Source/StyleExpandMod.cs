using System;
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
            StyleWatcher.Start();
            Logger.Message("Mod loaded successfully");
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