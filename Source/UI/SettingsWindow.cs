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
            float contentHeight = 2200f;
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
                list.Label("StyleExpand_PartialProgress".Translate(settings?.SelectedStyleName ?? ""));
                GUI.color = Color.white;
            }
            else if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.color = Color.green;
                list.Label(_statusMessage);
                GUI.color = Color.white;
            }
        }

        private static void DrawWarningMessage(Listing_Standard list)
        {
            if (!string.IsNullOrEmpty(_warningMessage))
            {
                list.Gap();
                GUI.color = Color.yellow;
                list.Label(_warningMessage);
                GUI.color = Color.white;
            }
        }

        private static void DrawSectionHeader(Listing_Standard list, string title, string description)
        {
            Text.Font = GameFont.Medium;
            list.Label(title);
            Text.Font = GameFont.Small;
            
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            list.Label(description);
            GUI.color = Color.white;
            list.Gap();
        }

        private static void DrawEmbeddingApiSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_EmbeddingApiConfig".Translate(), "StyleExpand_EmbeddingApiDesc".Translate());
            
            list.Label("StyleExpand_ApiUrl".Translate());
            settings.VectorApi.Url = list.TextEntry(settings.VectorApi.Url);
            
            list.Label("StyleExpand_ApiKey".Translate());
            settings.VectorApi.ApiKey = list.TextEntry(settings.VectorApi.ApiKey);
            
            list.Label("StyleExpand_ApiModel".Translate());
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
                list.Label(_testResult);
                GUI.color = color;
            }
        }

        private static void DrawLlmApiSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_LlmApiConfig".Translate(), "StyleExpand_LlmApiDesc".Translate());
            
            list.CheckboxLabeled("StyleExpand_UseRimTalkApi".Translate(), ref settings.LlmApi.UseRimTalkApi, "StyleExpand_UseRimTalkApiDesc".Translate());
            
            if (!settings.LlmApi.UseRimTalkApi)
            {
                list.Label("StyleExpand_LlmApiUrl".Translate());
                settings.LlmApi.Url = list.TextEntry(settings.LlmApi.Url);
                
                list.Label("StyleExpand_LlmApiKey".Translate());
                settings.LlmApi.ApiKey = list.TextEntry(settings.LlmApi.ApiKey);
                
                list.Label("StyleExpand_LlmModel".Translate());
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
                list.Label(_llmTestResult);
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
                list.Label("StyleExpand_NoStyles".Translate());
                list.Gap();
            }
            else
            {
                DrawStyleList(list, settings);
            }

            var selectedStyle = settings.GetSelectedStyle();
            if (selectedStyle != null)
            {
                list.Label("StyleExpand_Selected".Translate(selectedStyle.Name));
                
                if (StyleRetriever.CanResumeChunking(selectedStyle.Name))
                {
                    GUI.color = Color.yellow;
                    list.Label("StyleExpand_ResumeHint".Translate());
                    GUI.color = Color.white;
                }
                
                DrawChunkButtons(list, selectedStyle, settings);
                
                list.Gap();
                
                DrawStylePromptEditor(list, selectedStyle, settings);
            }
            else
            {
                list.Label("StyleExpand_NoStyleSelected".Translate());
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
            var stylePromptRow = list.GetRect(30f);
            Widgets.Label(new Rect(stylePromptRow.x, stylePromptRow.y, stylePromptRow.width - 120f, 30f), "StyleExpand_StylePrompt".Translate());
            
            if (Widgets.ButtonText(new Rect(stylePromptRow.xMax - 110f, stylePromptRow.y, 110f, 28f), "StyleExpand_ResetToDefault".Translate()))
            {
                selectedStyle.Prompt = "";
                ShowStatus("StyleExpand_StylePromptCleared".Translate());
            }
            
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

            list.Label("StyleExpand_TopK".Translate(settings.Retrieval.TopK));
            settings.Retrieval.TopK = (int)list.Slider(settings.Retrieval.TopK, 1, 10);
            
            list.Label("StyleExpand_MaxChunk".Translate(settings.Retrieval.MaxChunkLength));
            settings.Retrieval.MaxChunkLength = (int)list.Slider(settings.Retrieval.MaxChunkLength, 50, 500);
            
            list.Label("StyleExpand_Threshold".Translate(settings.Retrieval.SimilarityThreshold.ToString("F2")));
            settings.Retrieval.SimilarityThreshold = list.Slider(settings.Retrieval.SimilarityThreshold, 0f, 1f);
        }

        private static void DrawChunkingSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_ChunkingConfig".Translate(), "StyleExpand_ChunkingConfigDesc".Translate());
            
            list.CheckboxLabeled("StyleExpand_EnableSampling".Translate(), ref settings.Chunking.EnableSampling, "StyleExpand_EnableSamplingDesc".Translate());
            
            if (settings.Chunking.EnableSampling)
            {
                list.Label("StyleExpand_SampleTarget".Translate(settings.Chunking.SampleTargetChunks));
                settings.Chunking.SampleTargetChunks = (int)list.Slider(settings.Chunking.SampleTargetChunks, 100, 1000);
            }
            
            list.Label("StyleExpand_BatchSize".Translate(settings.Chunking.BatchSize));
            settings.Chunking.BatchSize = (int)list.Slider(settings.Chunking.BatchSize, 1, 50);
            
            list.Label("StyleExpand_LargeFileThreshold".Translate(settings.Chunking.LargeFileThreshold));
            settings.Chunking.LargeFileThreshold = (int)list.Slider(settings.Chunking.LargeFileThreshold, 10000, 200000);
        }

        private static void DrawScribanSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_ScribanVariables".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            list.Label("StyleExpand_ScribanDesc".Translate());
            GUI.color = Color.white;
            list.Gap();
            
            list.Label("  " + "StyleExpand_VarStyleName".Translate());
            list.Label("  " + "StyleExpand_VarBasePrompt".Translate());
            list.Label("  " + "StyleExpand_VarStylePrompt".Translate());
            list.Label("  " + "StyleExpand_VarStyleChunks".Translate());
            list.Label("  " + "StyleExpand_VarStyleFull".Translate());
        }

        private static void DrawPromptSection(Listing_Standard list, StyleExpandSettings settings)
        {
            DrawSectionHeader(list, "StyleExpand_PromptTemplate".Translate(), "StyleExpand_PromptTemplateDesc".Translate());
            
            var promptRow = list.GetRect(30f);
            Widgets.Label(new Rect(promptRow.x, promptRow.y, promptRow.width - 120f, 30f), "StyleExpand_BasePrompt".Translate());
            
            if (Widgets.ButtonText(new Rect(promptRow.xMax - 110f, promptRow.y, 110f, 28f), "StyleExpand_ResetToDefault".Translate()))
            {
                settings.Retrieval.BasePromptTemplate = "Please imitate the following writing style ({style_name}) when generating dialogue:";
                ShowStatus("StyleExpand_PromptReset".Translate());
            }
            
            var baseRect = list.GetRect(50f);
            settings.Retrieval.BasePromptTemplate = Widgets.TextArea(baseRect, settings.Retrieval.BasePromptTemplate);
            list.Gap();
        }

        private static void DrawDebugSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_Debug".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.CheckboxLabeled("StyleExpand_ShowQuery".Translate(), ref settings.Debug.ShowQuery, "StyleExpand_ShowQueryDesc".Translate());
            list.CheckboxLabeled("StyleExpand_ShowChunks".Translate(), ref settings.Debug.ShowChunks, "StyleExpand_ShowChunksDesc".Translate());
        }

        private static void DrawResetSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_Reset".Translate());
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
        
        public override Vector2 InitialSize => new Vector2(600f, 550f);

        public HelpWindow()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.doWindowBackground = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            
            float contentHeight = 900f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(inRect, ref _helpScrollPosition, viewRect);
            list.Begin(viewRect);
            
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_HelpTitle".Translate());
            Text.Font = GameFont.Small;
            list.GapLine();
            list.Gap();
            
            list.Label("StyleExpand_HelpQuickStart".Translate());
            list.Label("StyleExpand_HelpStep1".Translate());
            list.Label("StyleExpand_HelpStep2".Translate());
            list.Label("StyleExpand_HelpStep3".Translate());
            list.Label("StyleExpand_HelpStep4".Translate());
            list.Label("StyleExpand_HelpStep5".Translate());
            list.Gap();
            
            list.Label("StyleExpand_HelpApiConfig".Translate());
            list.Label("StyleExpand_HelpApiUrl".Translate());
            list.Label("StyleExpand_HelpApiSupport".Translate());
            list.Label("StyleExpand_HelpApiModel".Translate());
            list.Gap();
            
            list.Label("StyleExpand_HelpStyleFiles".Translate());
            list.Label("StyleExpand_HelpStyleOne".Translate());
            list.Label("StyleExpand_HelpStyleName".Translate());
            list.Label("StyleExpand_HelpStyleContent".Translate());
            list.Label("StyleExpand_HelpStyleSize".Translate());
            list.Label("StyleExpand_HelpStyleSizeNote".Translate());
            list.Gap();
            
            list.Label("StyleExpand_HelpChunking".Translate());
            list.Label("StyleExpand_HelpChunkWhat".Translate());
            list.Label("StyleExpand_HelpChunkEmbed".Translate());
            list.Label("StyleExpand_HelpChunkCache".Translate());
            list.Gap();
            
            list.Label("StyleExpand_HelpAdvanced".Translate());
            list.Label("StyleExpand_HelpScriban".Translate());
            list.Label("  " + "StyleExpand_VarStyleName".Translate());
            list.Label("  " + "StyleExpand_VarBasePrompt".Translate());
            list.Label("  " + "StyleExpand_VarStylePrompt".Translate());
            list.Label("  " + "StyleExpand_VarStyleChunks".Translate());
            list.Label("  " + "StyleExpand_VarStyleFull".Translate());
            
            list.End();
            Widgets.EndScrollView();
        }
    }
}