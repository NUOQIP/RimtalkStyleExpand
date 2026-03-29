using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalkStyleExpand
{
    public static class SettingsWindow
    {
        private static Vector2 _scrollPosition;
        private static int _selectedIndex = -1;
        private static string _testResult = "";
        private static string _warningMessage = "";
        private static int _warningTick = 0;
        private static string _statusMessage = "";
        private static int _statusTick = 0;
        private static Vector2 _styleListScrollPosition = Vector2.zero;
        private static Vector2 _stylePromptScrollPosition = Vector2.zero;
        
        private static bool _showAdvanced = false;

        public static void AddLabel(Listing_Standard list, string text)
        {
            var rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth));
            Widgets.Label(rect, text);
        }

        public static void DoSettingsContents(Rect inRect, StyleExpandSettings settings)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            
            try
            {
                if (settings == null)
                {
                    GUI.color = Color.red;
                    Widgets.Label(inRect, "Settings not initialized!");
                    GUI.color = Color.white;
                    return;
                }
                
                DrawSettings(inRect, settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[StyleExpand] Error drawing settings: {ex.Message}\n{ex.StackTrace}");
                GUI.color = Color.red;
                Widgets.Label(inRect, $"Error: {ex.Message}");
                GUI.color = Color.white;
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
            }
        }

        private static void DrawSettings(Rect inRect, StyleExpandSettings settings)
        {
            float contentHeight = 3000f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);
            
            var list = new Listing_Standard();
            list.Begin(viewRect);

            var headerRow = list.GetRect(30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(headerRow.x, headerRow.y, headerRow.width - 100f, 30f), 
                "StyleExpand_ModName".Translate() + " " + "StyleExpand_Version".Translate());
            Text.Font = GameFont.Small;
            
            if (Widgets.ButtonText(new Rect(headerRow.xMax - 90f, headerRow.y, 90f, 28f), "StyleExpand_Help".Translate()))
            {
                Find.WindowStack.Add(new HelpWindow());
            }
            
            list.Gap();
            
            list.CheckboxLabeled("StyleExpand_Enable".Translate(), ref settings.IsEnabled, "StyleExpand_EnableDesc".Translate());
            list.GapLine();

            DrawStatusMessage(list, settings);
            
            DrawStylesSection(list, settings);
            list.GapLine();
            
            DrawLlmApiSection(list, settings);
            list.GapLine();
            
            DrawEmbeddingApiSection(list, settings);
            list.GapLine();
            
            DrawAdvancedSection(list, settings);

            DrawWarningMessage(list);

            list.End();
            Widgets.EndScrollView();
        }

        private static void DrawStatusMessage(Listing_Standard list, StyleExpandSettings settings)
        {
            if (StyleRetriever.CanResumeChunking(settings?.SelectedStyleName ?? ""))
            {
                GUI.color = Color.yellow;
                SettingsWindow.AddLabel(list,"StyleExpand_PartialProgress".Translate(settings?.SelectedStyleName ?? ""));
                GUI.color = Color.white;
            }
            else if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.color = Color.green;
                SettingsWindow.AddLabel(list,_statusMessage);
                GUI.color = Color.white;
            }
        }

        private static void DrawWarningMessage(Listing_Standard list)
        {
            if (!string.IsNullOrEmpty(_warningMessage))
            {
                list.Gap();
                GUI.color = Color.yellow;
                SettingsWindow.AddLabel(list,_warningMessage);
                GUI.color = Color.white;
            }
        }

        private static void DrawSectionHeader(Listing_Standard list, string title, string description)
        {
            Text.Font = GameFont.Medium;
            SettingsWindow.AddLabel(list,title);
            Text.Font = GameFont.Small;
            
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            SettingsWindow.AddLabel(list,description);
            GUI.color = Color.white;
            list.Gap();
        }

        private static void DrawEmbeddingApiSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_EmbeddingApiConfig".Translate(), "StyleExpand_EmbeddingApiDesc".Translate());
            
            SettingsWindow.AddLabel(list,"StyleExpand_ApiUrl".Translate());
            settings.VectorApi.Url = list.TextEntry(settings.VectorApi.Url);
            
            SettingsWindow.AddLabel(list,"StyleExpand_ApiKey".Translate());
            settings.VectorApi.ApiKey = list.TextEntry(settings.VectorApi.ApiKey);
            
            SettingsWindow.AddLabel(list,"StyleExpand_ApiModel".Translate());
            settings.VectorApi.Model = list.TextEntry(settings.VectorApi.Model);
            list.Gap();

            if (list.ButtonText("StyleExpand_TestConnection".Translate()))
            {
                TestConnectionAsync(settings);
            }
            
            if (!string.IsNullOrEmpty(_testResult))
            {
                var color = GUI.color;
                GUI.color = _testResult.Contains("StyleExpand_ConnectionSuccessShort".Translate()) ? Color.green : Color.red;
                SettingsWindow.AddLabel(list,_testResult);
                GUI.color = color;
            }
        }

        private static void DrawLlmApiSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_LlmApiConfig".Translate(), "StyleExpand_LlmApiDesc".Translate());
            
            list.CheckboxLabeled("StyleExpand_UseRimTalkApi".Translate(), ref settings.LlmApi.UseRimTalkApi, "StyleExpand_UseRimTalkApiDesc".Translate());
            
            if (!settings.LlmApi.UseRimTalkApi)
            {
                SettingsWindow.AddLabel(list,"StyleExpand_LlmApiUrl".Translate());
                settings.LlmApi.Url = list.TextEntry(settings.LlmApi.Url);
                
                SettingsWindow.AddLabel(list,"StyleExpand_LlmApiKey".Translate());
                settings.LlmApi.ApiKey = list.TextEntry(settings.LlmApi.ApiKey);
                
                SettingsWindow.AddLabel(list,"StyleExpand_LlmModel".Translate());
                settings.LlmApi.Model = list.TextEntry(settings.LlmApi.Model);
            }
            
            list.Gap();
            
            if (list.ButtonText("StyleExpand_TestLlmConnection".Translate()))
            {
                TestLlmConnectionAsync(settings);
            }
            
            if (!string.IsNullOrEmpty(_llmTestResult))
            {
                var color = GUI.color;
                GUI.color = _llmTestResult.Contains("StyleExpand_ConnectionSuccessShort".Translate()) ? Color.green : Color.red;
                SettingsWindow.AddLabel(list,_llmTestResult);
                GUI.color = color;
            }
        }

        private static void DrawStylesSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_StyleManagement".Translate(), "StyleExpand_StyleManagementDesc".Translate());

            var btnRow = list.GetRect(30f);
            var btnWidth = btnRow.width / 3f - 5f;
            
            if (Widgets.ButtonText(new Rect(btnRow.x, btnRow.y, btnWidth, 30f), "StyleExpand_ScanStyles".Translate()))
            {
                ScanStylesAsync(settings);
            }
            
            if (Widgets.ButtonText(new Rect(btnRow.x + btnWidth + 5f, btnRow.y, btnWidth, 30f), "StyleExpand_OpenFolder".Translate()))
            {
                OpenStylesFolder();
            }
            
            if (Widgets.ButtonText(new Rect(btnRow.x + 2f * (btnWidth + 5f), btnRow.y, btnWidth, 30f), "StyleExpand_ClearCache".Translate()))
            {
                EmbeddingCache.ClearAll();
                StyleRetriever.RefreshStylesCacheStatus();
                ShowStatus("StyleExpand_CacheCleared".Translate());
            }
            
            list.Gap();

            if (settings.Styles.Count == 0)
            {
                SettingsWindow.AddLabel(list,"StyleExpand_NoStyles".Translate());
                list.Gap();
            }
            else
            {
                DrawStyleList(list, settings);
            }

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle != null)
            {
                SettingsWindow.AddLabel(list,"StyleExpand_Selected".Translate(selectedStyle.Name));
                
                if (StyleRetriever.CanResumeChunking(selectedStyle.Name))
                {
                    GUI.color = Color.yellow;
                    SettingsWindow.AddLabel(list,"StyleExpand_ResumeHint".Translate());
                    GUI.color = Color.white;
                }
                
                DrawChunkButtons(list, selectedStyle, settings);
                
                list.Gap();
                
                DrawStylePromptEditor(list, selectedStyle, settings);
            }
            else
            {
                SettingsWindow.AddLabel(list,"StyleExpand_NoStyleSelected".Translate());
            }
        }

        private static void DrawStyleList(Listing_Standard list, StyleExpandSettings settings)
        {
            const float RowHeight = 28f;
            const float RowSpacing = 30f;
            var contentHeight = settings.Styles.Count * RowSpacing;
            var viewHeight = Math.Min(contentHeight + 10f, 180f);
            var outerRect = list.GetRect(viewHeight);
            
            Widgets.DrawBoxSolid(outerRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            var innerRect = new Rect(outerRect.x + 5f, outerRect.y + 5f, outerRect.width - 10f, outerRect.height - 10f);
            var scrollContentHeight = contentHeight;
            
            bool needsScroll = scrollContentHeight > innerRect.height;
            
            if (needsScroll)
            {
                var scrollRect = new Rect(0f, 0f, innerRect.width - 16f, scrollContentHeight);
                Widgets.BeginScrollView(innerRect, ref _styleListScrollPosition, scrollRect);
                
                for (int i = 0; i < settings.Styles.Count; i++)
                {
                    var style = settings.Styles[i];
                    var rowRect = new Rect(0f, i * RowSpacing, scrollRect.width, RowHeight);
                    DrawStyleRow(rowRect, style, settings, i);
                }
                
                Widgets.EndScrollView();
            }
            else
            {
                for (int i = 0; i < settings.Styles.Count; i++)
                {
                    var style = settings.Styles[i];
                    var rowRect = new Rect(innerRect.x, innerRect.y + i * RowSpacing, innerRect.width, RowHeight);
                    DrawStyleRow(rowRect, style, settings, i);
                }
            }
            
            list.Gap();
        }
        
        private static void DrawStyleRow(Rect rowRect, StyleConfig style, StyleExpandSettings settings, int index)
        {
            var isSelected = style.Name == settings.SelectedStyleName;
            
            var bgColor = isSelected ? new Color(0.3f, 0.5f, 0.3f, 0.8f) : 
                          index % 2 == 0 ? new Color(0.15f, 0.15f, 0.15f, 0.5f) : new Color(0.1f, 0.1f, 0.1f, 0.5f);
            Widgets.DrawBoxSolid(rowRect, bgColor);
            
            var radioPos = new Vector2(rowRect.x + 5f, rowRect.y + 4f);
            Widgets.RadioButton(radioPos, isSelected);
            
            var labelRect = new Rect(rowRect.x + 30f, rowRect.y, rowRect.width - 35f, rowRect.height);
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
                _selectedIndex = index;
                
                if (!style.IsChunked && !StyleRetriever.IsStyleChunked(style.Name))
                {
                    ShowWarning("StyleExpand_NotChunked".Translate(style.Name));
                }
            }
        }

        private static void DrawChunkButtons(Listing_Standard list, StyleConfig selectedStyle, StyleExpandSettings settings)
        {
            var chunkBtnRow = list.GetRect(30f);
            var canResume = StyleRetriever.CanResumeChunking(selectedStyle.Name);
            var isChunked = selectedStyle.IsChunked;
            
            if (canResume)
            {
                var btnWidth = chunkBtnRow.width / 2f - 5f;
                
                GUI.color = Color.cyan;
                if (Widgets.ButtonText(new Rect(chunkBtnRow.x, chunkBtnRow.y, btnWidth, 30f), "StyleExpand_Resume".Translate()))
                {
                    ChunkStyleAsync(selectedStyle.Name, true, settings);
                }
                GUI.color = Color.white;
                
                if (Widgets.ButtonText(new Rect(chunkBtnRow.x + btnWidth + 5f, chunkBtnRow.y, btnWidth, 30f), "StyleExpand_Rechunk".Translate()))
                {
                    EmbeddingCache.Clear(selectedStyle.Name);
                    ChunkStyleAsync(selectedStyle.Name, false, settings);
                }
            }
            else if (isChunked)
            {
                if (Widgets.ButtonText(new Rect(chunkBtnRow.x, chunkBtnRow.y, chunkBtnRow.width, 30f), "StyleExpand_Rechunk".Translate()))
                {
                    EmbeddingCache.Clear(selectedStyle.Name);
                    ChunkStyleAsync(selectedStyle.Name, false, settings);
                }
            }
            else
            {
                if (Widgets.ButtonText(new Rect(chunkBtnRow.x, chunkBtnRow.y, chunkBtnRow.width, 30f), "StyleExpand_ChunkStyle".Translate()))
                {
                    ChunkStyleAsync(selectedStyle.Name, false, settings);
                }
            }
            
            GUI.color = Color.white;
        }

        private static void DrawStylePromptEditor(Listing_Standard list, StyleConfig selectedStyle, StyleExpandSettings settings)
        {
            SettingsWindow.AddLabel(list,"StyleExpand_StylePrompt".Translate());
            
            var textRect = list.GetRect(150f);
            Widgets.DrawBoxSolid(textRect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            
            var innerRect = textRect.ContractedBy(5f);
            float textHeight = Math.Max(Text.CalcHeight(selectedStyle.Prompt, innerRect.width - 16f), innerRect.height);
            var viewRect = new Rect(0f, 0f, innerRect.width - 16f, textHeight);
            
            Widgets.BeginScrollView(innerRect, ref _stylePromptScrollPosition, viewRect);
            
            GUI.SetNextControlName("StylePromptTextField");
            selectedStyle.Prompt = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, textHeight), selectedStyle.Prompt);
            
            Widgets.EndScrollView();
            
            list.Gap();
            
            var generateBtnRow = list.GetRect(30f);
            
            if (Widgets.ButtonText(new Rect(generateBtnRow.x, generateBtnRow.y, generateBtnRow.width / 2f - 5f, 30f), "StyleExpand_GeneratePrompt".Translate()))
            {
                GenerateStylePromptAsync(selectedStyle.Name);
            }
            
            Widgets.Label(new Rect(generateBtnRow.x + generateBtnRow.width / 2f + 10f, generateBtnRow.y + 5f, 80f, 20f), "StyleExpand_MaxTokens".Translate());
            var maxTokensStr = settings.LlmApi.MaxTokens.ToString();
            maxTokensStr = Widgets.TextField(new Rect(generateBtnRow.x + generateBtnRow.width / 2f + 95f, generateBtnRow.y, 60f, 30f), maxTokensStr);
            if (int.TryParse(maxTokensStr, out var maxTokens) && maxTokens > 0)
            {
                settings.LlmApi.MaxTokens = maxTokens;
            }
            
            GUI.color = Color.white;
        }

        private static void DrawAdvancedSection(Listing_Standard list, StyleExpandSettings settings)
        {
            var foldoutRect = list.GetRect(30f);
            string foldoutLabel = _showAdvanced ? "▼ " + "StyleExpand_AdvancedSettings".Translate() : "▶ " + "StyleExpand_AdvancedSettings".Translate();
            
            if (Widgets.ButtonInvisible(foldoutRect))
            {
                _showAdvanced = !_showAdvanced;
            }
            
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Text.Font = GameFont.Medium;
            Widgets.Label(foldoutRect, foldoutLabel);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            
            if (_showAdvanced)
            {
                list.Gap();
                
                DrawRetrievalSection(list, settings);
                list.GapLine();
                
                DrawPromptSection(list, settings);
                list.GapLine();
                
                DrawChunkingSection(list, settings);
                list.GapLine();
                
                DrawScribanSection(list, settings);
                list.GapLine();
                
                DrawDebugSection(list, settings);
                list.GapLine();
                
                DrawResetSection(list, settings);
            }
        }

        private static void DrawRetrievalSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_RetrievalConfig".Translate(), "StyleExpand_RetrievalConfigDesc".Translate());

            if (list.ButtonText("StyleExpand_Preview_OpenPreview".Translate()))
            {
                Find.WindowStack.Add(new Dialog_StylePreview());
            }
            list.Gap();

            SettingsWindow.AddLabel(list,"StyleExpand_TopK".Translate(settings.Retrieval.TopK));
            settings.Retrieval.TopK = (int)list.Slider(settings.Retrieval.TopK, 1, 10);
            
            SettingsWindow.AddLabel(list,"StyleExpand_Threshold".Translate(settings.Retrieval.SimilarityThreshold.ToString("F2")));
            settings.Retrieval.SimilarityThreshold = list.Slider(settings.Retrieval.SimilarityThreshold, 0f, 1f);
        }

        private static void DrawChunkingSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_ChunkingConfig".Translate(), "StyleExpand_ChunkingConfigDesc".Translate());
            
            SettingsWindow.AddLabel(list,"StyleExpand_ChunkingStrategy".Translate());
            var strategyLabels = new[] { 
                "StyleExpand_StrategySemantic".Translate(), 
                "StyleExpand_StrategyRecursive".Translate()
            };
            var strategyMapping = new[] { 
                ChunkingStrategy.Semantic, 
                ChunkingStrategy.Recursive
            };
            var currentStrategyIndex = System.Array.IndexOf(strategyMapping, settings.Chunking.Strategy);
            if (currentStrategyIndex < 0) currentStrategyIndex = 0;
            
            var strategyRow = list.GetRect(30f);
            for (int i = 0; i < strategyLabels.Length; i++)
            {
                var btnRect = new Rect(strategyRow.x + i * (strategyRow.width / 2f), strategyRow.y, strategyRow.width / 2f - 2f, 28f);
                var isSelected = i == currentStrategyIndex;
                
                if (isSelected)
                {
                    GUI.color = new Color(0.3f, 0.5f, 0.3f);
                    Widgets.DrawBoxSolid(btnRect, GUI.color);
                    GUI.color = Color.white;
                }
                
                if (Widgets.ButtonText(btnRect, strategyLabels[i]))
                {
                    settings.Chunking.Strategy = strategyMapping[i];
                }
            }
            
            list.Gap();
            
            GUI.color = new Color(0.7f, 0.9f, 0.7f);
            switch (settings.Chunking.Strategy)
            {
                case ChunkingStrategy.Recursive:
                    SettingsWindow.AddLabel(list,"StyleExpand_RecursiveChunkingInfo".Translate());
                    break;
                case ChunkingStrategy.Semantic:
                    SettingsWindow.AddLabel(list,"StyleExpand_SemanticChunkingInfo".Translate());
                    break;
            }
            GUI.color = Color.white;
            list.Gap();
            
            if (settings.Chunking.Strategy == ChunkingStrategy.Semantic)
            {
                SettingsWindow.AddLabel(list,"StyleExpand_BreakpointThreshold".Translate(settings.Chunking.BreakpointPercentileThreshold.ToString("F0")));
                settings.Chunking.BreakpointPercentileThreshold = list.Slider(settings.Chunking.BreakpointPercentileThreshold, 50f, 95f);
            }
            
            list.Gap();
            
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            SettingsWindow.AddLabel(list,"StyleExpand_ChunkLengthParams".Translate());
            GUI.color = Color.white;
            
            SettingsWindow.AddLabel(list,"StyleExpand_MinChunkLength".Translate(settings.Chunking.MinChunkLength));
            settings.Chunking.MinChunkLength = (int)list.Slider(settings.Chunking.MinChunkLength, 50, 300);
            
            SettingsWindow.AddLabel(list,"StyleExpand_TargetChunkLength".Translate(settings.Chunking.TargetChunkLength));
            settings.Chunking.TargetChunkLength = (int)list.Slider(settings.Chunking.TargetChunkLength, 200, 1000);
            
            SettingsWindow.AddLabel(list,"StyleExpand_MaxChunkLength".Translate(settings.Chunking.MaxChunkLength));
            settings.Chunking.MaxChunkLength = (int)list.Slider(settings.Chunking.MaxChunkLength, 500, 2000);
            
            SettingsWindow.AddLabel(list,"StyleExpand_Overlap".Translate(settings.Chunking.Overlap));
            settings.Chunking.Overlap = (int)list.Slider(settings.Chunking.Overlap, 0, 200);
            
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_BatchSize".Translate(settings.Chunking.BatchSize));
            settings.Chunking.BatchSize = (int)list.Slider(settings.Chunking.BatchSize, 1, 50);
            
            list.Gap();
            list.CheckboxLabeled("StyleExpand_EnableSampling".Translate(), ref settings.Chunking.EnableSampling, "StyleExpand_EnableSamplingDesc".Translate());
            
            if (settings.Chunking.EnableSampling)
            {
                SettingsWindow.AddLabel(list,"StyleExpand_SampleTarget".Translate(settings.Chunking.SampleTargetChunks));
                settings.Chunking.SampleTargetChunks = (int)list.Slider(settings.Chunking.SampleTargetChunks, 100, 1000);
                
                SettingsWindow.AddLabel(list,"StyleExpand_LargeFileThreshold".Translate(settings.Chunking.LargeFileThreshold));
                settings.Chunking.LargeFileThreshold = (int)list.Slider(settings.Chunking.LargeFileThreshold, 10000, 100000);
            }
        }

        private static void DrawScribanSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            SettingsWindow.AddLabel(list,"StyleExpand_ScribanVariables".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            SettingsWindow.AddLabel(list,"StyleExpand_ScribanDesc".Translate());
            GUI.color = Color.white;
            list.Gap();
            
            SettingsWindow.AddLabel(list,"  " + "StyleExpand_VarStyleName".Translate());
            SettingsWindow.AddLabel(list,"  " + "StyleExpand_VarStylePrompt".Translate());
            SettingsWindow.AddLabel(list,"  " + "StyleExpand_VarStyleChunks".Translate());
            SettingsWindow.AddLabel(list,"  " + "StyleExpand_VarStyleFull".Translate());
        }

        private static void DrawPromptSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_PromptTemplate".Translate(), "StyleExpand_PromptTemplateDesc".Translate());
            
            var promptRow = list.GetRect(30f);
            Widgets.Label(new Rect(promptRow.x, promptRow.y, promptRow.width - 120f, 30f), "StyleExpand_BasePrompt".Translate());
            
            if (Widgets.ButtonText(new Rect(promptRow.xMax - 110f, promptRow.y, 110f, 28f), "StyleExpand_ResetBasePrompt".Translate()))
            {
                settings.Retrieval.BasePromptTemplate = @"Write in the **{{style_name}}** style. Refer to the style guide below to grasp its tone, rhythm, and atmosphere. Let it permeate your entire output—not surface imitation, but deep embodiment.";
                ShowStatus("StyleExpand_PromptReset".Translate());
            }
            
            var baseRect = list.GetRect(50f);
            settings.Retrieval.BasePromptTemplate = Widgets.TextArea(baseRect, settings.Retrieval.BasePromptTemplate);
            list.Gap();
            
            DrawSectionHeader(list, "StyleExpand_StylePromptTemplate".Translate(), "StyleExpand_StylePromptTemplateDesc".Translate());
            
            var stylePromptRect = list.GetRect(200f);
            settings.LlmApi.StylePromptTemplate = Widgets.TextArea(stylePromptRect, settings.LlmApi.StylePromptTemplate);
            
            var stylePromptFooter = list.GetRect(30f);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(new Rect(stylePromptFooter.x, stylePromptFooter.y, stylePromptFooter.width - 130f, 30f), "StyleExpand_StylePromptVars".Translate());
            GUI.color = Color.white;
            
            if (Widgets.ButtonText(new Rect(stylePromptFooter.xMax - 120f, stylePromptFooter.y, 120f, 28f), "StyleExpand_ResetStylePrompt".Translate()))
            {
                settings.LlmApi.StylePromptTemplate = @"You are a writing style guide writer. Your task is to analyze the provided text sample, extract the distinctive stylistic patterns that define how the author writes independent of what they write about, and create a practical style guide to instruct other LLMs to replicate the ""{{style_name}}"" style.

【Requirements】
- Examine the text holistically and determine which dimensions of style are most distinctive and defining for this particular writing.
- Focus exclusively on HOW the writing works, not WHAT it contains. Extract only transferable stylistic elements that could be applied to any content.
- Let the text itself reveal what matters—different styles emphasize different elements, so adapt your analysis accordingly rather than forcing a predetermined framework.

【Forbidden】
Do not analyze content-specific elements that only exist in this particular text (characters, settings, plot events, unique terminology, etc.).
Do not include perspective or formatting rules—these are context-dependent, not style-inherent.

【Output】
- Produce a style guide within {{max_tokens}} tokens that captures the essence of this writing approach. Your guide should enable LLMs to replicate the style of the sample.
- Write in the same language as the input text.
- Use imperative tone.

【Sample Text】
{{sample_text}}";
                ShowStatus("StyleExpand_PromptReset".Translate());
            }
            
            list.Gap();
        }

        private static void DrawDebugSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            SettingsWindow.AddLabel(list,"StyleExpand_Debug".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.CheckboxLabeled("StyleExpand_ShowQuery".Translate(), ref settings.Debug.ShowQuery, "StyleExpand_ShowQueryDesc".Translate());
            list.CheckboxLabeled("StyleExpand_ShowChunks".Translate(), ref settings.Debug.ShowChunks, "StyleExpand_ShowChunksDesc".Translate());
        }

        private static void DrawResetSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            SettingsWindow.AddLabel(list,"StyleExpand_Reset".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            var resetRow = list.GetRect(30f);
            var resetWidth = resetRow.width / 2f - 5f;
            
            if (Widgets.ButtonText(new Rect(resetRow.x, resetRow.y, resetWidth, 30f), "StyleExpand_ResetAllSettings".Translate()))
            {
                ResetAllSettings(settings);
                ShowStatus("StyleExpand_SettingsReset".Translate());
            }
            
            if (Widgets.ButtonText(new Rect(resetRow.x + resetWidth + 5f, resetRow.y, resetWidth, 30f), "StyleExpand_ClearAllCache".Translate()))
            {
                EmbeddingCache.ClearAll();
                foreach (var style in settings.Styles)
                {
                    style.IsChunked = false;
                    style.ChunkCount = 0;
                }
                ShowStatus("StyleExpand_AllCacheCleared".Translate());
            }
        }

        private static void ResetAllSettings(StyleExpandSettings settings)
        {
            settings.IsEnabled = true;
            settings.SelectedStyleName = "";
            settings.VectorApi = new VectorApiConfig();
            settings.Retrieval = new RetrievalConfig();
            settings.Debug = new DebugConfig();
            settings.Styles.Clear();
            EmbeddingCache.ClearAll();
            StyleRetriever.ScanStyleFiles();
        }

        private static void ShowWarning(string message)
        {
            _warningMessage = message;
            _warningTick = GetCurrentTick();
        }

        private static void ShowStatus(string message)
        {
            _statusMessage = message;
            _statusTick = GetCurrentTick();
        }

        private static void ShowError(string message)
        {
            _warningMessage = "StyleExpand_ErrorPrefix".Translate() + message;
            _warningTick = GetCurrentTick();
            Log.Error($"[StyleExpand] {message}");
        }

        private static int GetCurrentTick()
        {
            try
            {
                return Find.TickManager?.TicksGame ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void TestConnectionAsync(StyleExpandSettings settings)
        {
            _testResult = "StyleExpand_Testing".Translate();
            
            try
            {
                var embedding = VectorClient.GetEmbeddingSync("test", settings?.VectorApi);
                _testResult = embedding != null ? "StyleExpand_ConnectionSuccessShort".Translate() : "StyleExpand_ConnectionFailedShort".Translate();
                if (embedding == null)
                {
                    ShowError("StyleExpand_ConnectionFailed".Translate("no embedding returned"));
                }
                else
                {
                    ShowStatus("StyleExpand_ConnectionSuccess".Translate());
                }
            }
            catch (Exception ex)
            {
                _testResult = "StyleExpand_ConnectionFailed".Translate(ex.Message);
                ShowError("StyleExpand_ConnectionFailed".Translate(ex.Message));
            }
        }

        private static string _llmTestResult = "";
        
        private static void TestLlmConnectionAsync(StyleExpandSettings settings)
        {
            _llmTestResult = "StyleExpand_Testing".Translate();
            
            try
            {
                var success = LLMClient.TestConnection(settings.LlmApi);
                _llmTestResult = success 
                    ? "StyleExpand_ConnectionSuccessShort".Translate() 
                    : "StyleExpand_ConnectionFailedShort".Translate();
                
                if (!success)
                {
                    ShowError("StyleExpand_LlmConnectionFailed".Translate("no response"));
                }
                else
                {
                    ShowStatus("StyleExpand_LlmConnectionSuccess".Translate());
                }
            }
            catch (Exception ex)
            {
                _llmTestResult = "StyleExpand_LlmConnectionFailed".Translate(ex.Message);
                ShowError("StyleExpand_LlmConnectionFailed".Translate(ex.Message));
            }
        }

        private static void ScanStylesAsync(StyleExpandSettings settings)
        {
            try
            {
                StyleRetriever.ScanStyleFiles();
                _selectedIndex = -1;
                ShowStatus("StyleExpand_ScanComplete".Translate(settings?.Styles?.Count ?? 0));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private static void ChunkStyleAsync(string styleName, bool resume, StyleExpandSettings settings)
        {
            if (string.IsNullOrEmpty(settings?.VectorApi?.Url) || string.IsNullOrEmpty(settings?.VectorApi?.Model))
            {
                ShowWarning("StyleExpand_ApiNotConfigured".Translate());
                return;
            }
            
            var charCount = StyleRetriever.GetFileCharCount(styleName);
            
            if (!resume && charCount > (settings?.Chunking?.LargeFileThreshold ?? 50000))
            {
                ShowWarning("StyleExpand_FileTooLarge".Translate(styleName, charCount.ToString()));
            }
            
            LongEventHandler.QueueLongEvent(() =>
            {
                try
                {
                    StyleRetriever.ChunkStyle(styleName, resume);
                }
                catch (Exception ex)
                {
                    Log.Error($"[StyleExpand] Chunk failed: {ex.Message}");
                    throw;
                }
            }, "StyleExpand_Chunking".Translate(styleName), true, (Exception ex) =>
            {
                if (ex != null)
                {
                    ShowError(ex.Message);
                }
                else if (StyleRetriever.WasCancelled)
                {
                    ShowWarning("StyleExpand_ChunkingCancelled".Translate(styleName));
                }
                else
                {
                    ShowStatus("StyleExpand_ChunkComplete".Translate(styleName, StyleRetriever.ChunkTotal.ToString()));
                }
            });
        }

        private static void GenerateStylePromptAsync(string styleName)
        {
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return;
            
            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return;
            
            var filePath = System.IO.Path.Combine(mod.Content.RootDir, "Styles", styleName + ".txt");
            if (!System.IO.File.Exists(filePath))
            {
                ShowError("StyleExpand_FileNotFound".Translate(styleName));
                return;
            }
            
            LongEventHandler.QueueLongEvent(() =>
            {
                try
                {
                    var sampleText = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    var prompt = LLMClient.GenerateStylePrompt(styleName, sampleText, settings.LlmApi);
                    
                    var style = settings.Styles.FirstOrDefault(s => s.Name == styleName);
                    if (style != null)
                    {
                        style.Prompt = prompt;
                        settings.Write();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[StyleExpand] Generate prompt failed: {ex.Message}");
                    throw;
                }
            }, "StyleExpand_GeneratingPrompt".Translate(styleName), true, (Exception ex) =>
            {
                if (ex != null)
                {
                    ShowError("StyleExpand_GenerateFailed".Translate(ex.Message));
                }
                else
                {
                    ShowStatus("StyleExpand_PromptGenerated".Translate(styleName));
                }
            });
        }

        private static void OpenStylesFolder()
        {
            var path = StyleRetriever.GetStylesPath();
            if (path == null)
            {
                ShowError("StyleExpand_FolderNotFound".Translate());
                return;
            }
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
                ShowStatus("StyleExpand_FolderOpened".Translate());
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
    }

    public class HelpWindow : Window
    {
        private Vector2 _helpScrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(650f, 600f);

        public HelpWindow()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.doWindowBackground = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            
            float contentHeight = 2000f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(inRect, ref _helpScrollPosition, viewRect);
            list.Begin(viewRect);
            
            Text.Font = GameFont.Medium;
            SettingsWindow.AddLabel(list,"StyleExpand_HelpTitle".Translate());
            Text.Font = GameFont.Small;
            list.GapLine();
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpIntro".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpQuickStart".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStep1".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStep2".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStep3".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStep4".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStep5".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpApiSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpApiEmbedding".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpApiOllama".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpApiOther".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStyleSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStyleOne".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStyleSize".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpStyleTips".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpChunkSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpChunkStrategy".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpChunkSemantic".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpChunkRecursive".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpChunkParams".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRetrievalSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRetrievalTopK".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRetrievalThreshold".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRecommendSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRecommendModels".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpRecommendParams".Translate());
            list.Gap();
            
            SettingsWindow.AddLabel(list,"StyleExpand_HelpFaqSection".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpFaq1".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpFaq2".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpFaq3".Translate());
            SettingsWindow.AddLabel(list,"StyleExpand_HelpFaq4".Translate());
            
            list.End();
            Widgets.EndScrollView();
        }
    }
}