using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimTalkStyleExpand
{
    /// <summary>
    /// 设置 UI 绘制辅助类
    /// 拆分 UI 代码以减少主文件大小
    /// </summary>
    public static class SettingsUIDrawers
    {
        private static Vector2 _styleListScrollPosition;
        private static Dictionary<string, List<(string name, string description, string category)>> _cachedVariables;
        
        #region 文风选择 UI
        
        public static void DrawStyleSelectionSection(Listing_Standard list, StyleExpandSettings settings, 
            ref int selectedIndex, Action<string> onWarning)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_StyleSelection".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            DrawStyleList(list, settings, ref selectedIndex, onWarning);
        }
        
        private static void DrawStyleList(Listing_Standard list, StyleExpandSettings settings, 
            ref int selectedIndex, Action<string> onWarning)
        {
            if (settings.Styles.Count == 0)
            {
                list.Label("StyleExpand_NoStyles".Translate());
                list.Gap();
                return;
            }
            
            var contentHeight = settings.Styles.Count * 32f;
            var viewHeight = Math.Min(contentHeight + 10f, 150f);
            var styleRect = list.GetRect(viewHeight);
            
            Widgets.DrawBoxSolid(styleRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            var innerStyleRect = new Rect(styleRect.x + 5f, styleRect.y + 5f, styleRect.width - 10f, styleRect.height - 10f);
            var scrollRect = new Rect(0f, 0f, innerStyleRect.width - 16f, contentHeight);
            
            bool needsScroll = contentHeight > innerStyleRect.height;
            if (needsScroll)
            {
                Widgets.BeginScrollView(innerStyleRect, ref _styleListScrollPosition, scrollRect);
            }
            
            for (int i = 0; i < settings.Styles.Count; i++)
            {
                var style = settings.Styles[i];
                var rowRect = new Rect(0f, i * 32f, needsScroll ? scrollRect.width : innerStyleRect.width, 28f);
                var isSelected = style.Name == settings.SelectedStyleName;
                
                var bgColor = isSelected ? new Color(0.3f, 0.5f, 0.3f, 0.8f) : 
                              i % 2 == 0 ? new Color(0.15f, 0.15f, 0.15f, 0.5f) : new Color(0.1f, 0.1f, 0.1f, 0.5f);
                Widgets.DrawBoxSolid(rowRect, bgColor);
                
                var radioPos = new Vector2(rowRect.x + 5f, rowRect.y + 4f);
                Widgets.RadioButton(radioPos, isSelected);
                
                var labelRect = new Rect(rowRect.x + 30f, rowRect.y, rowRect.width - 80f, rowRect.height);
                var label = style.Name;
                if (style.IsChunked)
                {
                    label += " [" + "StyleExpand_ChunksCount".Translate(style.ChunkCount) + "]";
                }
                else
                {
                    label += " [" + "StyleExpand_NotChunkedLabel".Translate() + "]";
                }
                Widgets.Label(labelRect, label);
                
                if (Widgets.ButtonInvisible(rowRect))
                {
                    settings.SelectStyle(style.Name);
                    selectedIndex = i;
                    
                    if (!style.IsChunked && !StyleRetriever.IsStyleChunked(style.Name))
                    {
                        onWarning?.Invoke("StyleExpand_NotChunked".Translate(style.Name));
                    }
                }
            }
            
            if (needsScroll)
            {
                Widgets.EndScrollView();
            }
            
            list.Gap();
        }
        
        #endregion
        
        #region API 配置 UI
        
        public static void DrawApiConfigSection(Listing_Standard list, StyleExpandSettings settings,
            Action testConnection, string testResult)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_ApiConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.Label("StyleExpand_ApiUrl".Translate());
            settings.VectorApi.Url = list.TextEntry(settings.VectorApi.Url);
            
            list.Label("StyleExpand_ApiKey".Translate());
            settings.VectorApi.ApiKey = list.TextEntry(settings.VectorApi.ApiKey);
            
            list.Label("StyleExpand_ApiModel".Translate());
            settings.VectorApi.Model = list.TextEntry(settings.VectorApi.Model);
            
            if (list.ButtonText("StyleExpand_TestConnection".Translate()))
            {
                testConnection?.Invoke();
            }
            
            if (!string.IsNullOrEmpty(testResult))
            {
                GUI.color = testResult.Contains("Success") || testResult.Contains("成功") ? Color.green : Color.red;
                list.Label(testResult);
                GUI.color = Color.white;
            }
        }
        
        #endregion
        
        #region 变量选择 UI
        
        public static void DrawVariableSelector(Rect rect, Action<string> onVariableSelected)
        {
            var variables = VariableHelper.GetFlattenedVariables()
                .Where(v => !v.name.StartsWith("#") && v.name != "json.format" && v.name != "chat.history")
                .ToList();
            
            if (variables.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "StyleExpand_NoVariablesFound".Translate());
                GUI.color = Color.white;
                return;
            }
            
            var options = new List<FloatMenuOption>();
            
            foreach (var v in variables)
            {
                string varName = v.name;
                string desc = v.description;
                
                options.Add(new FloatMenuOption($"{varName} - {desc}", () =>
                {
                    onVariableSelected?.Invoke(varName);
                }));
            }
            
            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        
        #endregion
        
        #region 检索配置 UI
        
        public static void DrawRetrievalConfigSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_RetrievalConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.Label("StyleExpand_TopK".Translate(settings.Retrieval.TopK));
            settings.Retrieval.TopK = (int)list.Slider(settings.Retrieval.TopK, 1, 10);
            
            list.Label("StyleExpand_MaxChunk".Translate(settings.Retrieval.MaxChunkLength));
            settings.Retrieval.MaxChunkLength = (int)list.Slider(settings.Retrieval.MaxChunkLength, 50, 500);
            
            list.Label("StyleExpand_Threshold".Translate(settings.Retrieval.SimilarityThreshold));
            settings.Retrieval.SimilarityThreshold = list.Slider(settings.Retrieval.SimilarityThreshold, 0f, 1f);
        }
        
        #endregion
        
        #region 切分配置 UI
        
        public static void DrawChunkingConfigSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_ChunkingConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.CheckboxLabeled("StyleExpand_EnableSampling".Translate(), ref settings.Chunking.EnableSampling, 
                "StyleExpand_EnableSamplingDesc".Translate());
            
            if (settings.Chunking.EnableSampling)
            {
                list.Label("StyleExpand_SampleTarget".Translate(settings.Chunking.SampleTargetChunks));
                settings.Chunking.SampleTargetChunks = (int)list.Slider(settings.Chunking.SampleTargetChunks, 100, 1000);
            }
            
            list.Label("StyleExpand_BatchSize".Translate(settings.Chunking.BatchSize));
            settings.Chunking.BatchSize = (int)list.Slider(settings.Chunking.BatchSize, 1, 50);
            
            list.Label("StyleExpand_LargeFileThreshold".Translate(settings.Chunking.LargeFileThreshold));
            settings.Chunking.LargeFileThreshold = (int)list.Slider(settings.Chunking.LargeFileThreshold, 10000, 200000);
            
            list.CheckboxLabeled("StyleExpand_AutoResume".Translate(), ref settings.Chunking.AutoResume, 
                "StyleExpand_AutoResumeDesc".Translate());
        }
        
        #endregion
    }
}