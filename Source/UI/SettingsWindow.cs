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
        private static bool _isProcessing = false;
        private static string _processingStatus = "";
        private static Vector2 _styleListScrollPosition = Vector2.zero;

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
            float contentHeight = 2500f;
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
            
            DrawApiSection(list, settings);
            list.GapLine();
            
            DrawPromptSection(list, settings);
            list.GapLine();
            
            DrawRetrievalSection(list, settings);
            list.GapLine();
            
            DrawChunkingSection(list, settings);
            list.GapLine();
            
            DrawLlmApiSection(list, settings);
            list.GapLine();
            
            DrawStylesSection(list, settings);
            list.GapLine();
            
            DrawAdvancedUsageSection(list, settings);
            list.GapLine();
            
            DrawDebugSection(list, settings);
            list.GapLine();
            
            DrawResetSection(list, settings);

            DrawWarningMessage(list);

            list.End();
            Widgets.EndScrollView();
        }

        private static void DrawStatusMessage(Listing_Standard list, StyleExpandSettings settings)
        {
            if (_isProcessing)
            {
                GUI.color = Color.cyan;
                if (StyleRetriever.ChunkTotal > 0)
                {
                    var progress = (float)StyleRetriever.ChunkProgress / StyleRetriever.ChunkTotal * 100;
                    list.Label("StyleExpand_ChunkingProgress".Translate(
                        StyleRetriever.ChunkStyleName,
                        StyleRetriever.ChunkProgress.ToString(),
                        StyleRetriever.ChunkTotal.ToString()) + $" ({progress:F1}%)");
                    
                    var progressRect = list.GetRect(20f);
                    var progressWidth = progressRect.width * (StyleRetriever.ChunkProgress / (float)StyleRetriever.ChunkTotal);
                    Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.2f, 0.2f));
                    Widgets.DrawBoxSolid(new Rect(progressRect.x, progressRect.y, progressWidth, progressRect.height), new Color(0.2f, 0.6f, 0.8f));
                    
                    if (Widgets.ButtonText(new Rect(progressRect.xMax - 80f, progressRect.y + 25f, 80f, 28f), "StyleExpand_Cancel".Translate()))
                    {
                        StyleRetriever.CancelChunking();
                    }
                }
                else
                {
                    list.Label("StyleExpand_Processing".Translate(_processingStatus));
                }
                GUI.color = Color.white;
            }
            else if (StyleRetriever.CanResumeChunking(settings?.SelectedStyleName ?? ""))
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

        private static void DrawApiSection(Listing_Standard list, StyleExpandSettings settings)
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

        private static void DrawPromptSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_PromptTemplate".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
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

        private static void DrawRetrievalSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_RetrievalConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            var templateRow = list.GetRect(30f);
            Widgets.Label(new Rect(templateRow.x, templateRow.y, templateRow.width - 120f, 30f), "StyleExpand_QueryTemplate".Translate());
            
            if (Widgets.ButtonText(new Rect(templateRow.xMax - 110f, templateRow.y, 110f, 28f), "StyleExpand_InsertVariable".Translate()))
            {
                ShowVariableSelector(settings);
            }
            
            settings.Retrieval.QueryTemplate = list.TextEntry(settings.Retrieval.QueryTemplate, 2);
            list.Gap();

            if (list.ButtonText("StyleExpand_Preview_OpenPreview".Translate()))
            {
                Find.WindowStack.Add(new Dialog_StylePreview());
            }
            list.Gap();

            list.Label("StyleExpand_TopK".Translate(settings.Retrieval.TopK));
            settings.Retrieval.TopK = (int)list.Slider(settings.Retrieval.TopK, 1, 10);
            
            list.Label("StyleExpand_MaxChunk".Translate(settings.Retrieval.MaxChunkLength));
            settings.Retrieval.MaxChunkLength = (int)list.Slider(settings.Retrieval.MaxChunkLength, 50, 500);
            
            list.Label("StyleExpand_Threshold".Translate(settings.Retrieval.SimilarityThreshold));
            settings.Retrieval.SimilarityThreshold = list.Slider(settings.Retrieval.SimilarityThreshold, 0f, 1f);
        }

        private static void ShowVariableSelector(StyleExpandSettings settings)
        {
            var variables = VariableHelper.GetPawnVariables();
            var options = new List<FloatMenuOption>();
            
            foreach (var v in variables)
            {
                string varName = v.name;
                string desc = v.description;
                
                options.Add(new FloatMenuOption($"{varName} - {desc}", delegate
                {
                    settings.Retrieval.QueryTemplate += " {{" + varName + "}}";
                }));
            }
            
            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                ShowWarning("StyleExpand_NoVariablesFound".Translate());
            }
        }

        private static void DrawChunkingSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_ChunkingConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
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
            
            list.CheckboxLabeled("StyleExpand_AutoResume".Translate(), ref settings.Chunking.AutoResume, "StyleExpand_AutoResumeDesc".Translate());
        }

        private static void DrawLlmApiSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_LlmApiConfig".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.CheckboxLabeled("StyleExpand_UseRimTalkApi".Translate(), ref settings.LlmApi.UseRimTalkApi, "StyleExpand_UseRimTalkApiDesc".Translate());
            
            if (!settings.LlmApi.UseRimTalkApi)
            {
                list.Label("StyleExpand_LlmApiUrl".Translate());
                settings.LlmApi.Url = list.TextEntry(settings.LlmApi.Url);
                
                list.Label("StyleExpand_LlmApiKey".Translate());
                settings.LlmApi.ApiKey = list.TextEntry(settings.LlmApi.ApiKey);
            }
            
            list.Label("StyleExpand_LlmModel".Translate());
            settings.LlmApi.Model = list.TextEntry(settings.LlmApi.Model);
        }

        private static void DrawStylesSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_StyleSelection".Translate());
            Text.Font = GameFont.Small;
            list.Gap();

            var btnRow = list.GetRect(30f);
            var btnWidth = btnRow.width / 4f - 5f;
            
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
                ShowStatus("StyleExpand_CacheCleared".Translate());
            }
            
            var chunkAllEnabled = !_isProcessing && StyleRetriever.GetUnchunkedCount() > 0;
            if (!chunkAllEnabled) GUI.color = Color.grey;
            if (Widgets.ButtonText(new Rect(btnRow.x + 3f * (btnWidth + 5f), btnRow.y, btnWidth, 30f), "StyleExpand_ChunkAll".Translate()) && chunkAllEnabled)
            {
                ChunkAllStylesAsync();
            }
            GUI.color = Color.white;
            
            list.Gap();

            if (settings.Styles.Count == 0)
            {
                list.Label("StyleExpand_NoStyles".Translate());
                list.Gap();
            }
            else
            {
                var contentHeight = settings.Styles.Count * 32f;
                var viewHeight = Math.Min(contentHeight + 10f, 150f);
                var styleRect = list.GetRect(viewHeight);
                
                Widgets.DrawBoxSolid(styleRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                
                var innerStyleRect = new Rect(styleRect.x + 5f, styleRect.y + 5f, styleRect.width - 10f, styleRect.height - 10f);
                var scrollRect = new Rect(0f, 0f, innerStyleRect.width - 16f, contentHeight);
                
                var y = 0f;
                
                bool needsScroll = contentHeight > innerStyleRect.height;
                if (needsScroll)
                {
                    Widgets.BeginScrollView(innerStyleRect, ref _styleListScrollPosition, scrollRect);
                }
                
                for (int i = 0; i < settings.Styles.Count; i++)
                {
                    var style = settings.Styles[i];
                    var rowRect = new Rect(0f, y, needsScroll ? scrollRect.width : innerStyleRect.width, 28f);
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
                    
                    var chunkedIndicator = new Rect(rowRect.xMax - 25f, rowRect.y + 4f, 20f, 20f);
                    if (style.IsChunked)
                    {
                        GUI.color = Color.green;
                        Widgets.Label(chunkedIndicator, "*");
                        GUI.color = Color.white;
                    }
                    
                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        settings.SelectStyle(style.Name);
                        _selectedIndex = i;
                        
                        if (!style.IsChunked && !StyleRetriever.IsStyleChunked(style.Name))
                        {
                            ShowWarning("StyleExpand_NotChunked".Translate(style.Name));
                        }
                    }
                    
                    y += 32f;
                }
                
                if (needsScroll)
                {
                    Widgets.EndScrollView();
                }
                
                list.Gap();
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
                
                var chunkBtnRow = list.GetRect(30f);
                var canResume = StyleRetriever.CanResumeChunking(selectedStyle.Name);
                var btnCount = canResume ? 3 : 2;
                var chunkBtnWidth = chunkBtnRow.width / btnCount - 5f;
                
                var chunkBtnEnabled = !_isProcessing;
                if (!chunkBtnEnabled)
                {
                    GUI.color = Color.grey;
                }
                
                if (Widgets.ButtonText(new Rect(chunkBtnRow.x, chunkBtnRow.y, chunkBtnWidth, 30f), "StyleExpand_ChunkStyle".Translate()) && chunkBtnEnabled)
                {
                    ChunkStyleAsync(selectedStyle.Name, false, settings);
                }
                
                if (canResume)
                {
                    GUI.color = chunkBtnEnabled ? Color.cyan : Color.grey;
                    if (Widgets.ButtonText(new Rect(chunkBtnRow.x + chunkBtnWidth + 5f, chunkBtnRow.y, chunkBtnWidth, 30f), "StyleExpand_Resume".Translate()) && chunkBtnEnabled)
                    {
                        ChunkStyleAsync(selectedStyle.Name, true, settings);
                    }
                    GUI.color = Color.white;
                    
                    if (!chunkBtnEnabled) GUI.color = Color.grey;
                    if (Widgets.ButtonText(new Rect(chunkBtnRow.x + 2f * (chunkBtnWidth + 5f), chunkBtnRow.y, chunkBtnWidth, 30f), "StyleExpand_Rechunk".Translate()) && chunkBtnEnabled)
                    {
                        EmbeddingCache.Clear(selectedStyle.Name);
                        ChunkStyleAsync(selectedStyle.Name, false, settings);
                    }
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(chunkBtnRow.x + chunkBtnWidth + 5f, chunkBtnRow.y, chunkBtnWidth, 30f), "StyleExpand_Rechunk".Translate()) && chunkBtnEnabled)
                    {
                        EmbeddingCache.Clear(selectedStyle.Name);
                        ChunkStyleAsync(selectedStyle.Name, false, settings);
                    }
                }
                
                GUI.color = Color.white;
                
                list.Gap();
                
                var stylePromptRow = list.GetRect(30f);
                Widgets.Label(new Rect(stylePromptRow.x, stylePromptRow.y, stylePromptRow.width - 120f, 30f), "StyleExpand_StylePrompt".Translate());
                
                if (Widgets.ButtonText(new Rect(stylePromptRow.xMax - 110f, stylePromptRow.y, 110f, 28f), "StyleExpand_ResetToDefault".Translate()))
                {
                    selectedStyle.Prompt = "";
                    ShowStatus("StyleExpand_StylePromptCleared".Translate());
                }
                
                var textRect = list.GetRect(60f);
                selectedStyle.Prompt = Widgets.TextArea(textRect, selectedStyle.Prompt);
                
                list.Gap();
                
                var generateBtnRow = list.GetRect(30f);
                var generateBtnEnabled = !_isProcessing && !string.IsNullOrEmpty(selectedStyle.Name);
                if (!generateBtnEnabled) GUI.color = Color.grey;
                
                if (Widgets.ButtonText(new Rect(generateBtnRow.x, generateBtnRow.y, generateBtnRow.width / 2f - 5f, 30f), "StyleExpand_GeneratePrompt".Translate()) && generateBtnEnabled)
                {
                    GenerateStylePromptAsync(selectedStyle.Name);
                }
                
                GUI.color = Color.white;
            }
            else
            {
                list.Label("StyleExpand_NoStyleSelected".Translate());
            }
        }

        private static void DrawAdvancedUsageSection(Listing_Standard list, StyleExpandSettings settings)
        {
            Text.Font = GameFont.Medium;
            list.Label("StyleExpand_AdvancedMode".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.Label("StyleExpand_UseVariables".Translate());
            list.Gap();
            
            GUI.color = new Color(0.7f, 0.9f, 0.7f);
            list.Label("StyleExpand_VarStyleName".Translate());
            list.Label("StyleExpand_VarBasePrompt".Translate());
            list.Label("StyleExpand_VarStylePrompt".Translate());
            list.Label("StyleExpand_VarStyleChunks".Translate());
            list.Label("StyleExpand_VarStyleFull".Translate());
            GUI.color = Color.white;
            
            list.Gap();
            list.Label("StyleExpand_ExampleUsage".Translate());
            
            var exampleRect = list.GetRect(80f);
            var exampleText = @"[Style Instruction]
{{style_base_prompt}}

{{style_prompt}}

{{style_chunks}}";
            Widgets.TextArea(exampleRect, exampleText, true);
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

        private static void SetProcessing(bool processing, string status = "")
        {
            _isProcessing = processing;
            _processingStatus = status;
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

        private static void ScanStylesAsync(StyleExpandSettings settings)
        {
            SetProcessing(true, "StyleExpand_Scanning".Translate());
            
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
            finally
            {
                SetProcessing(false);
            }
        }

        private static void ChunkStyleAsync(string styleName, bool resume, StyleExpandSettings settings)
        {
            var charCount = StyleRetriever.GetFileCharCount(styleName);
            
            if (!resume && charCount > (settings?.Chunking?.LargeFileThreshold ?? 50000))
            {
                ShowWarning("StyleExpand_FileTooLarge".Translate(styleName, charCount.ToString()));
            }
            
            SetProcessing(true, "StyleExpand_Chunking".Translate(styleName));
            
            try
            {
                StyleRetriever.ChunkStyle(styleName, resume);
                
                if (StyleRetriever.WasCancelled)
                {
                    ShowWarning("StyleExpand_ChunkingCancelled".Translate(styleName));
                }
                else
                {
                    ShowStatus("StyleExpand_ChunkComplete".Translate(styleName, StyleRetriever.ChunkTotal.ToString()));
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SetProcessing(false);
            }
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
            
            SetProcessing(true, "StyleExpand_GeneratingPrompt".Translate(styleName));
            
            try
            {
                var sampleText = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var prompt = LLMClient.GenerateStylePrompt(styleName, sampleText, settings.LlmApi);
                
                var style = settings.Styles.FirstOrDefault(s => s.Name == styleName);
                if (style != null)
                {
                    style.Prompt = prompt;
                    settings.Write();
                    ShowStatus("StyleExpand_PromptGenerated".Translate(styleName));
                }
            }
            catch (Exception ex)
            {
                ShowError("StyleExpand_GenerateFailed".Translate(ex.Message));
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private static void ChunkAllStylesAsync()
        {
            SetProcessing(true, "StyleExpand_ChunkingAll".Translate());
            
            try
            {
                var processed = StyleRetriever.ChunkAllStyles();
                
                if (StyleRetriever.WasCancelled)
                {
                    ShowWarning("StyleExpand_ChunkAllCancelled".Translate());
                }
                else
                {
                    ShowStatus("StyleExpand_ChunkAllComplete".Translate(processed.ToString()));
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SetProcessing(false);
            }
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
        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public HelpWindow()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.doWindowBackground = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            
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
        }
    }
}