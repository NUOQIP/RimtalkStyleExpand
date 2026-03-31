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
        
        private static List<string> _availableModels = new List<string>();
        private static int _selectedModelIndex = -1;
        private static bool _isLoadingModels = false;
        
        private static float _stylePromptHeight = 200f;
        private static float _fullPromptHeight = 200f;
        private static float _stylePromptTemplateHeight = 200f;
        private static Vector2 _fullPromptScrollPos = Vector2.zero;
        private static Vector2 _stylePromptTemplateScrollPos = Vector2.zero;

        public static void AddLabel(Listing_Standard list, string text)
        {
            var rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth));
            Widgets.Label(rect, text);
        }
        
        private static string DrawResizableTextArea(Listing_Standard list, string label, string content, ref float height, ref Vector2 scrollPos, float minHeight = 50f, float maxHeight = 500f)
        {
            var labelRect = list.GetRect(25f);
            Widgets.Label(labelRect, label);
            
            var textRect = list.GetRect(height);
            Widgets.DrawBoxSolid(textRect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            Widgets.DrawBox(textRect);
            
            var innerRect = textRect.ContractedBy(5f);
            float textHeight = Math.Max(Text.CalcHeight(content, innerRect.width - 16f), innerRect.height);
            var viewRect = new Rect(0f, 0f, innerRect.width - 16f, textHeight);
            
            Widgets.BeginScrollView(innerRect, ref scrollPos, viewRect);
            content = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, textHeight), content);
            Widgets.EndScrollView();
            
            var handleRect = new Rect(textRect.x, textRect.yMax - 6f, textRect.width, 12f);
            
            if (Mouse.IsOver(handleRect))
            {
                Widgets.DrawHighlight(handleRect);
                if (Input.GetMouseButton(0))
                {
                    height += Event.current.delta.y;
                    height = Mathf.Clamp(height, minHeight, maxHeight);
                }
            }
            
            Widgets.DrawLineHorizontal(textRect.x, textRect.yMax, textRect.width, new Color(0.4f, 0.4f, 0.4f));
            
            list.Gap(6f);
            return content;
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
            
            DrawApiStatusWarning(list, settings);
            
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

        private static bool _apiChecked = false;
        private static bool _apiAvailable = false;
        private static int _lastApiCheckTick = 0;
        
        private static void DrawApiStatusWarning(Listing_Standard list, StyleExpandSettings settings)
        {
            if (settings == null || !settings.IsEnabled) return;
            
            int currentTick = GetCurrentTick();
            if (!_apiChecked || currentTick - _lastApiCheckTick > 600)
            {
                _apiChecked = true;
                _lastApiCheckTick = currentTick;
                _apiAvailable = CheckApiQuickly(settings);
            }
            
            if (!_apiAvailable)
            {
                var rect = list.GetRect(50f);
                GUI.color = new Color(1f, 0.7f, 0.2f);
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.15f, 0.05f, 0.8f));
                
                var innerRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, rect.height - 10f);
                
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(innerRect.x, innerRect.y, innerRect.width, 20f), 
                    "StyleExpand_ApiWarningTitle".Translate());
                GUI.color = Color.white;
                Widgets.Label(new Rect(innerRect.x, innerRect.y + 22f, innerRect.width, 20f), 
                    "StyleExpand_ApiWarningDesc".Translate());
                
                Text.Anchor = TextAnchor.UpperLeft;
                list.Gap();
            }
        }
        
        private static bool CheckApiQuickly(StyleExpandSettings settings)
        {
            if (settings?.VectorApi == null) return false;
            if (string.IsNullOrEmpty(settings.VectorApi.Url)) return false;
            if (string.IsNullOrEmpty(settings.VectorApi.Model)) return false;
            
            try
            {
                var embedding = VectorClient.GetEmbeddingSync("test", settings.VectorApi);
                return embedding != null && embedding.Length > 0;
            }
            catch
            {
                return false;
            }
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
            
            var modelRow = list.GetRect(30f);
            var inputWidth = modelRow.width - 130f;
            
            settings.VectorApi.Model = Widgets.TextField(
                new Rect(modelRow.x, modelRow.y, inputWidth, 30f), 
                settings.VectorApi.Model);
            
            if (Widgets.ButtonText(new Rect(modelRow.xMax - 120f, modelRow.y, 120f, 30f), 
                _isLoadingModels ? "StyleExpand_LoadingModels".Translate() : "StyleExpand_GetModels".Translate()))
            {
                FetchModelsAsync(settings);
            }
            
            if (_availableModels.Count > 0)
            {
                var dropdownRect = list.GetRect(30f);
                
                var options = new List<string> { "StyleExpand_SelectModel".Translate() };
                options.AddRange(_availableModels);
                
                var selectedIndex = _selectedModelIndex + 1;
                if (Widgets.ButtonText(dropdownRect, options[selectedIndex]))
                {
                    var floatMenuOptions = new List<FloatMenuOption>();
                    for (int i = 0; i < _availableModels.Count; i++)
                    {
                        int idx = i;
                        floatMenuOptions.Add(new FloatMenuOption(_availableModels[i], () =>
                        {
                            _selectedModelIndex = idx;
                            settings.VectorApi.Model = _availableModels[idx];
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
                }
            }
            
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
        
        private static void FetchModelsAsync(StyleExpandSettings settings)
        {
            if (_isLoadingModels) return;
            _isLoadingModels = true;
            
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    _availableModels = VectorClient.GetAvailableModels(settings.VectorApi);
                    _selectedModelIndex = -1;
                    
                    if (_availableModels.Count == 0)
                    {
                        ShowWarning("StyleExpand_NoModelsFound".Translate());
                    }
                    else
                    {
                        ShowStatus("StyleExpand_ModelsLoaded".Translate(_availableModels.Count));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to fetch models: {ex.Message}");
                    ShowWarning("StyleExpand_FailedToGetModels".Translate());
                }
                finally
                {
                    _isLoadingModels = false;
                }
            });
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
            selectedStyle.Prompt = DrawResizableTextArea(list, "StyleExpand_StylePrompt".Translate(), selectedStyle.Prompt, ref _stylePromptHeight, ref _stylePromptScrollPosition);
            
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
            Widgets.Label(new Rect(promptRow.x, promptRow.y, promptRow.width - 120f, 30f), "StyleExpand_FullPrompt".Translate());
            
            if (Widgets.ButtonText(new Rect(promptRow.xMax - 110f, promptRow.y, 110f, 28f), "StyleExpand_ResetFullPrompt".Translate()))
            {
                settings.Retrieval.FullPromptTemplate = @"Write in the **{{style_name}}** style.

Read the style guide below. You must apply these style characteristics in all your outputs.

[Style Guide]
{{style_prompt}}

[Style Examples]
{{style_chunks}}

For reference only on language form; apply flexibly based on the current scene.";
                ShowStatus("StyleExpand_PromptReset".Translate());
            }
            
            settings.Retrieval.FullPromptTemplate = DrawResizableTextArea(list, "", settings.Retrieval.FullPromptTemplate, ref _fullPromptHeight, ref _fullPromptScrollPos);
            
            DrawSectionHeader(list, "StyleExpand_StylePromptTemplate".Translate(), "StyleExpand_StylePromptTemplateDesc".Translate());
            
            settings.LlmApi.StylePromptTemplate = DrawResizableTextArea(list, "", settings.LlmApi.StylePromptTemplate, ref _stylePromptTemplateHeight, ref _stylePromptTemplateScrollPos);
            
            var stylePromptFooter = list.GetRect(30f);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(new Rect(stylePromptFooter.x, stylePromptFooter.y, stylePromptFooter.width - 130f, 30f), "StyleExpand_StylePromptVars".Translate());
            GUI.color = Color.white;
            
            if (Widgets.ButtonText(new Rect(stylePromptFooter.xMax - 120f, stylePromptFooter.y, 120f, 28f), "StyleExpand_ResetStylePrompt".Translate()))
            {
                settings.LlmApi.StylePromptTemplate = @"You are a writing style guide writer. Your task is to analyze the provided text sample, extract its distinctive style patterns, and create a practical style guide to instruct other LLMs to replicate the ""{{style_name}}"" writing style for text output.

【Important Note】
This style guide will be used for RP dialogue generation in RimTalk, a RimWorld mod.

【Core Requirements】
- Examine the text holistically and determine which style dimensions are most defining and distinctive for this writing, rather than applying a predetermined analytical framework.
- Focus only on ""how it is written,"" not ""what is written."" Extract style elements that can be transferred to any content.
- Focus on: macro style, writing perspective, writing structure, writing philosophy/principles, word choice and sentence construction, rhetorical devices, description techniques (action, appearance, expression descriptions, etc.), and writing skills.

【Prohibitions】
- Ignore content-specific elements that only exist in this text (e.g., character settings).
- Do not include formatting rules.
- Avoid abstract theories unless they can be directly applied to dialogue generation.

【Output Requirements】
- Strictly limit to {{max_tokens}} tokens.
- Write in the same language as the input text.
- Use imperative tone to guide practice, not analytical reports.
- Output should enable other LLMs to directly execute style replication, not merely understand style characteristics.

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
                    var sampleText = FileEncodingHelper.ReadAllTextWithAutoDetect(filePath);
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
        
        private void AddSectionTitle(Listing_Standard list, string text)
        {
            list.Gap(8f);
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.9f, 0.85f, 0.6f);
            var rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth));
            Widgets.Label(rect, text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            list.Gap(4f);
        }
        
        private void AddBodyText(Listing_Standard list, string text)
        {
            var rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth));
            Widgets.Label(rect, text);
        }
        
        private void AddIndentedText(Listing_Standard list, string text, float indent = 20f)
        {
            var rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth - indent));
            rect.x += indent;
            rect.width -= indent;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(rect, text);
            GUI.color = Color.white;
            list.Gap(2f);
        }
        
        private void AddFaqItem(Listing_Standard list, string question, string answer, float indent = 20f)
        {
            var qRect = list.GetRect(Text.CalcHeight(question, list.ColumnWidth - indent));
            qRect.x += indent;
            qRect.width -= indent;
            GUI.color = new Color(0.9f, 0.85f, 0.7f);
            Widgets.Label(qRect, question);
            GUI.color = Color.white;
            
            var aRect = list.GetRect(Text.CalcHeight(answer, list.ColumnWidth - indent));
            aRect.x += indent;
            aRect.width -= indent;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(aRect, answer);
            GUI.color = Color.white;
            list.Gap(6f);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            
            float contentHeight = 2500f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(inRect, ref _helpScrollPosition, viewRect);
            list.Begin(viewRect);
            
            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            var titleRect = list.GetRect(35f);
            Widgets.Label(titleRect, "StyleExpand_HelpTitle".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            list.GapLine();
            
            AddBodyText(list, "StyleExpand_HelpIntro".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpQuickStart".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep1".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep2".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep3".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep4".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep5".Translate());
            AddIndentedText(list, "StyleExpand_HelpStep6".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpApiSection".Translate());
            AddBodyText(list, "StyleExpand_HelpApiEmbedding".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOllama".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOllamaStep1".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOllamaStep2".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOllamaStep3".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOllamaStep4".Translate());
            AddIndentedText(list, "StyleExpand_HelpApiOther".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpStyleSection".Translate());
            AddIndentedText(list, "StyleExpand_HelpStyleOne".Translate());
            AddIndentedText(list, "StyleExpand_HelpStyleSize".Translate());
            AddIndentedText(list, "StyleExpand_HelpStyleTips".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpChunkSection".Translate());
            AddBodyText(list, "StyleExpand_HelpChunkStrategy".Translate());
            AddIndentedText(list, "StyleExpand_HelpChunkSemantic".Translate());
            AddIndentedText(list, "StyleExpand_HelpChunkRecursive".Translate());
            AddIndentedText(list, "StyleExpand_HelpChunkParams".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpRetrievalSection".Translate());
            AddIndentedText(list, "StyleExpand_HelpRetrievalTopK".Translate());
            AddIndentedText(list, "StyleExpand_HelpRetrievalThreshold".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpRecommendSection".Translate());
            AddIndentedText(list, "StyleExpand_HelpRecommendModels".Translate());
            AddIndentedText(list, "StyleExpand_HelpRecommendParams".Translate());
            
            AddSectionTitle(list, "StyleExpand_HelpFaqSection".Translate());
            AddFaqItem(list, "StyleExpand_HelpFaq1".Translate(), "StyleExpand_HelpFaq1A".Translate());
            AddFaqItem(list, "StyleExpand_HelpFaq2".Translate(), "StyleExpand_HelpFaq2A".Translate());
            AddFaqItem(list, "StyleExpand_HelpFaq3".Translate(), "StyleExpand_HelpFaq3A".Translate());
            AddFaqItem(list, "StyleExpand_HelpFaq4".Translate(), "StyleExpand_HelpFaq4A".Translate());
            
            list.End();
            Widgets.EndScrollView();
        }
    }
}