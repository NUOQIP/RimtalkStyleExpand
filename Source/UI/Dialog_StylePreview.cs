using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace RimTalkStyleExpand
{
    public class Dialog_StylePreview : Window
    {
        private Vector2 _variableScrollPosition;
        private Vector2 _resultScrollPosition;
        private Vector2 _parsedScrollPosition;
        
        private List<(string name, string description, string category)> _availableVariables;
        private HashSet<string> _selectedVariables = new HashSet<string>();
        
        private string _queryTemplate = "";
        private string _manualInput = "";
        private string _parsedQuery = "";
        
        private List<(StyleRetriever.StyleChunk chunk, float similarity)> _retrievedChunks = new List<(StyleRetriever.StyleChunk, float)>();
        
        private bool _showVariablesPanel = true;
        private bool _isLoading = false;
        
        private static Type _scribanParserType;
        private static MethodInfo _renderMethod;
        private static bool _rimTalkResolved = false;
        
        public override Vector2 InitialSize => new Vector2(900f, 650f);

        public Dialog_StylePreview()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            
            LoadVariables();
            ResolveRimTalkTypes();
            
            var settings = StyleExpandSettings.Instance;
            if (settings != null)
            {
                _queryTemplate = settings.Retrieval.QueryTemplate;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float yPos = 0f;

            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            Widgets.Label(new Rect(0f, yPos, 600f, 35f), "StyleExpand_Preview_Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            yPos += 40f;

            float contentHeight = inRect.height - yPos - 50f;
            float leftWidth = 280f;
            float rightWidth = inRect.width - leftWidth - 20f;

            Rect leftRect = new Rect(0f, yPos, leftWidth, contentHeight);
            DrawVariablesPanel(leftRect);

            Rect rightRect = new Rect(leftWidth + 20f, yPos, rightWidth, contentHeight);
            DrawPreviewPanel(rightRect);
        }

        #region Variables Panel

        private void DrawVariablesPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 0.9f));
            
            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            
            string title = _showVariablesPanel ? "▼ " : "▶ ";
            title += "StyleExpand_Preview_Variables".Translate();
            
            if (Widgets.ButtonText(titleRect, title, false))
            {
                _showVariablesPanel = !_showVariablesPanel;
            }
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            
            if (!_showVariablesPanel) return;
            
            float yPos = rect.y + 35f;
            
            Rect helpRect = new Rect(rect.x + 5f, yPos, rect.width - 10f, 20f);
            GUI.color = Color.gray;
            Widgets.Label(helpRect, "StyleExpand_Preview_VariablesHint".Translate());
            GUI.color = Color.white;
            yPos += 25f;
            
            Rect listRect = new Rect(rect.x + 5f, yPos, rect.width - 10f, rect.yMax - yPos - 10f);
            DrawVariablesList(listRect);
        }

        private void DrawVariablesList(Rect rect)
        {
            if (_availableVariables == null || _availableVariables.Count == 0)
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "StyleExpand_Preview_NoVariables".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            var innerRect = rect.ContractedBy(2f);
            float lineHeight = 24f;
            float totalHeight = _availableVariables.Count * lineHeight;
            var viewRect = new Rect(0f, 0f, innerRect.width - 16f, totalHeight);

            Widgets.BeginScrollView(innerRect, ref _variableScrollPosition, viewRect);

            string currentCategory = null;
            float y = 0f;

            for (int i = 0; i < _availableVariables.Count; i++)
            {
                var v = _availableVariables[i];
                
                if (v.category != currentCategory)
                {
                    currentCategory = v.category;
                    GUI.color = new Color(0.6f, 0.7f, 0.9f);
                    Widgets.Label(new Rect(0f, y, viewRect.width, lineHeight), $"[{currentCategory}]");
                    GUI.color = Color.white;
                    y += lineHeight;
                }

                var rowRect = new Rect(0f, y, viewRect.width, lineHeight);
                
                bool isSelected = _selectedVariables.Contains(v.name);
                bool newSelected = isSelected;
                
                string displayName = v.name;
                if (displayName.StartsWith("pawn."))
                {
                    displayName = displayName.Substring(5);
                }
                
                Widgets.CheckboxLabeled(rowRect, displayName, ref newSelected);
                
                if (!string.IsNullOrEmpty(v.description))
                {
                    TooltipHandler.TipRegion(rowRect, v.description);
                }
                
                if (newSelected != isSelected)
                {
                    if (newSelected)
                    {
                        _selectedVariables.Add(v.name);
                        InsertVariableToTemplate(v.name);
                    }
                    else
                    {
                        _selectedVariables.Remove(v.name);
                    }
                }

                y += lineHeight;
            }

            Widgets.EndScrollView();
        }

        private void InsertVariableToTemplate(string varName)
        {
            if (string.IsNullOrEmpty(_queryTemplate))
            {
                _queryTemplate = "{{ " + varName + " }}";
            }
            else
            {
                _queryTemplate += " {{ " + varName + " }}";
            }
        }

        #endregion

        #region Preview Panel

        private void DrawPreviewPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            
            float yPos = rect.y + 10f;
            float contentWidth = rect.width - 20f;

            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(new Rect(rect.x + 10f, yPos, contentWidth, 25f), "StyleExpand_Preview_QueryTemplate".Translate());
            GUI.color = Color.white;
            yPos += 28f;

            Rect templateRect = new Rect(rect.x + 10f, yPos, contentWidth, 60f);
            _queryTemplate = Widgets.TextArea(templateRect, _queryTemplate);
            yPos += 65f;

            Rect btnRow = new Rect(rect.x + 10f, yPos, contentWidth, 30f);
            float btnWidth = btnRow.width / 4f - 5f;

            if (Widgets.ButtonText(new Rect(btnRow.x, btnRow.y, btnWidth, 30f), "StyleExpand_Preview_Parse".Translate()))
            {
                ParseQuery();
            }

            if (Widgets.ButtonText(new Rect(btnRow.x + btnWidth + 5f, btnRow.y, btnWidth, 30f), "StyleExpand_Preview_Retrieve".Translate()))
            {
                RetrieveChunks();
            }

            if (Widgets.ButtonText(new Rect(btnRow.x + 2f * (btnWidth + 5f), btnRow.y, btnWidth, 30f), "StyleExpand_Preview_Clear".Translate()))
            {
                ClearAll();
            }

            bool canRetrieve = !_isLoading && !string.IsNullOrEmpty(_parsedQuery) && HasSelectedStyle();
            if (!canRetrieve) GUI.color = Color.gray;
            if (Widgets.ButtonText(new Rect(btnRow.x + 3f * (btnWidth + 5f), btnRow.y, btnWidth, 30f), "StyleExpand_Preview_ManualRetrieve".Translate()) && canRetrieve)
            {
                RetrieveFromManual();
            }
            GUI.color = Color.white;

            yPos += 35f;

            if (!HasSelectedStyle())
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(rect.x + 10f, yPos, contentWidth, 25f), "StyleExpand_Preview_NoStyleSelected".Translate());
                GUI.color = Color.white;
                yPos += 30f;
            }

            GUI.color = new Color(0.7f, 0.9f, 0.7f);
            Widgets.Label(new Rect(rect.x + 10f, yPos, contentWidth, 25f), "StyleExpand_Preview_ParsedQuery".Translate());
            GUI.color = Color.white;
            yPos += 28f;

            Rect parsedRect = new Rect(rect.x + 10f, yPos, contentWidth, 80f);
            Widgets.DrawBoxSolid(parsedRect, new Color(0.08f, 0.08f, 0.08f, 0.8f));
            
            if (string.IsNullOrEmpty(_parsedQuery))
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(parsedRect, "StyleExpand_Preview_ClickParse".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            else
            {
                var innerParsedRect = parsedRect.ContractedBy(5f);
                float textHeight = Text.CalcHeight(_parsedQuery, innerParsedRect.width - 16f);
                var parsedViewRect = new Rect(0f, 0f, innerParsedRect.width - 16f, Mathf.Max(textHeight, innerParsedRect.height));
                
                Widgets.BeginScrollView(innerParsedRect, ref _parsedScrollPosition, parsedViewRect);
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(0f, 0f, parsedViewRect.width, textHeight), _parsedQuery);
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }
            yPos += 85f;

            GUI.color = new Color(1f, 0.9f, 0.7f);
            string resultTitle = "StyleExpand_Preview_RetrievedChunks".Translate(_retrievedChunks.Count);
            Widgets.Label(new Rect(rect.x + 10f, yPos, contentWidth, 25f), resultTitle);
            GUI.color = Color.white;
            yPos += 28f;

            Rect resultRect = new Rect(rect.x + 10f, yPos, contentWidth, rect.yMax - yPos - 10f);
            DrawRetrievedChunks(resultRect);
        }

        private void DrawRetrievedChunks(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.8f));
            
            if (_retrievedChunks.Count == 0)
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "StyleExpand_Preview_NoChunks".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            var innerRect = rect.ContractedBy(5f);
            float entryHeight = 70f;
            float totalHeight = _retrievedChunks.Count * entryHeight;
            var viewRect = new Rect(0f, 0f, innerRect.width - 16f, totalHeight);

            Widgets.BeginScrollView(innerRect, ref _resultScrollPosition, viewRect);

            for (int i = 0; i < _retrievedChunks.Count; i++)
            {
                var item = _retrievedChunks[i];
                var entryRect = new Rect(0f, i * entryHeight, viewRect.width, entryHeight - 5f);
                
                Widgets.DrawBoxSolid(entryRect, new Color(0.15f, 0.18f, 0.15f, 0.6f));
                
                GUI.color = new Color(0.9f, 0.9f, 0.6f);
                string header = $"[{i + 1}] " + "StyleExpand_Preview_Score".Translate(item.similarity.ToString("F3"));
                Widgets.Label(new Rect(entryRect.x + 5f, entryRect.y + 2f, entryRect.width - 10f, 20f), header);
                GUI.color = Color.white;
                
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                string preview = item.chunk.Text.Length > 150 
                    ? item.chunk.Text.Substring(0, 147) + "..." 
                    : item.chunk.Text;
                Widgets.Label(new Rect(entryRect.x + 5f, entryRect.y + 22f, entryRect.width - 10f, 45f), preview);
                GUI.color = Color.white;
            }

            Widgets.EndScrollView();
        }

        #endregion

        #region Actions

        private void LoadVariables()
        {
            _availableVariables = VariableHelper.GetFlattenedVariables()
                .Where(v => !v.name.StartsWith("#") && v.name != "json.format" && v.name != "chat.history")
                .Where(v => !v.name.StartsWith("knowledge"))
                .ToList();
        }

        private void ResolveRimTalkTypes()
        {
            if (_rimTalkResolved) return;
            _rimTalkResolved = true;
            
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (assembly == null) return;
                
                _scribanParserType = assembly.GetType("RimTalk.Prompt.ScribanParser");
                if (_scribanParserType != null)
                {
                    _renderMethod = _scribanParserType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                }
            }
            catch
            {
            }
        }

        private void ParseQuery()
        {
            if (string.IsNullOrEmpty(_queryTemplate))
            {
                _parsedQuery = "";
                return;
            }

            try
            {
                if (_renderMethod != null)
                {
                    var promptManagerType = _scribanParserType?.Assembly.GetType("RimTalk.Prompt.PromptManager");
                    var lastContextProperty = promptManagerType?.GetProperty("LastContext", BindingFlags.Public | BindingFlags.Static);
                    
                    if (lastContextProperty != null)
                    {
                        var ctx = lastContextProperty.GetValue(null);
                        if (ctx != null)
                        {
                            var result = _renderMethod.Invoke(null, new object[] { _queryTemplate, ctx, false });
                            _parsedQuery = result as string ?? _queryTemplate;
                            return;
                        }
                    }
                }
                
                _parsedQuery = _queryTemplate;
            }
            catch
            {
                _parsedQuery = _queryTemplate;
            }
        }

        private void RetrieveChunks()
        {
            if (string.IsNullOrEmpty(_parsedQuery))
            {
                ParseQuery();
            }
            
            if (string.IsNullOrEmpty(_parsedQuery)) return;
            
            _isLoading = true;
            _retrievedChunks.Clear();
            
            try
            {
                var settings = StyleExpandSettings.Instance;
                if (settings == null) return;
                
                var resultsWithScores = StyleRetriever.RetrieveWithScores(_parsedQuery, settings.Retrieval.TopK, settings.Retrieval.SimilarityThreshold);
                
                foreach (var result in resultsWithScores)
                {
                    _retrievedChunks.Add((result.chunk, result.similarity));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve chunks: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void RetrieveFromManual()
        {
            if (string.IsNullOrEmpty(_manualInput))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("StyleExpand_Preview_EnterManualInput".Translate()));
                return;
            }
            
            _parsedQuery = _manualInput;
            RetrieveChunks();
        }

        private void ClearAll()
        {
            _queryTemplate = "";
            _parsedQuery = "";
            _manualInput = "";
            _selectedVariables.Clear();
            _retrievedChunks.Clear();
        }

        private bool HasSelectedStyle()
        {
            var settings = StyleExpandSettings.Instance;
            var style = settings?.GetSelectedStyle();
            return style != null && style.IsChunked;
        }

        #endregion
    }
}