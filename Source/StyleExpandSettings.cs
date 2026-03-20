using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimTalkStyleExpand
{
    public class StyleExpandSettings : ModSettings
    {
        public bool IsEnabled = true;
        public string SelectedStyleName = "";
        
        public VectorApiConfig VectorApi = new VectorApiConfig();
        public RetrievalConfig Retrieval = new RetrievalConfig();
        public DebugConfig Debug = new DebugConfig();
        public ChunkingConfig Chunking = new ChunkingConfig();
        public LlmApiConfig LlmApi = new LlmApiConfig();
        
        public List<StyleConfig> Styles = new List<StyleConfig>();
        
        public static StyleExpandSettings Instance { get; private set; }

        public StyleExpandSettings()
        {
            Instance = this;
        }

        public StyleConfig GetSelectedStyle()
        {
            if (string.IsNullOrEmpty(SelectedStyleName)) return null;
            return Styles.FirstOrDefault(s => s.Name == SelectedStyleName);
        }

        public void SelectStyle(string name)
        {
            SelectedStyleName = name;
        }

        public bool HasStyle(string name)
        {
            return Styles.Any(s => s.Name == name);
        }

        public void AddStyle(StyleConfig style)
        {
            if (!HasStyle(style.Name))
            {
                Styles.Add(style);
            }
        }

        public void RemoveStyle(string name)
        {
            Styles.RemoveAll(s => s.Name == name);
            if (SelectedStyleName == name)
            {
                SelectedStyleName = Styles.FirstOrDefault()?.Name ?? "";
            }
        }

        public void ClearStyles()
        {
            Styles.Clear();
            SelectedStyleName = "";
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref IsEnabled, "isEnabled", true);
            Scribe_Values.Look(ref SelectedStyleName, "selectedStyleName", "");
            
            Scribe_Deep.Look(ref VectorApi, "vectorApi");
            Scribe_Deep.Look(ref Retrieval, "retrieval");
            Scribe_Deep.Look(ref Debug, "debug");
            Scribe_Deep.Look(ref Chunking, "chunking");
            Scribe_Deep.Look(ref LlmApi, "llmApi");
            
            Scribe_Collections.Look(ref Styles, "styles", LookMode.Deep);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (VectorApi == null) VectorApi = new VectorApiConfig();
                if (Retrieval == null) Retrieval = new RetrievalConfig();
                if (Debug == null) Debug = new DebugConfig();
                if (Chunking == null) Chunking = new ChunkingConfig();
                if (LlmApi == null) LlmApi = new LlmApiConfig();
                if (Styles == null) Styles = new List<StyleConfig>();
            }
        }
    }
}