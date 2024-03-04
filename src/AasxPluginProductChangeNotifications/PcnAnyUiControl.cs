﻿/*
Copyright (c) 2018-2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AasxIntegrationBase;
using Aas = AasCore.Aas3_0;
using AdminShellNS;
using Extensions;
using AnyUi;
using Newtonsoft.Json;
using AasxPredefinedConcepts;
using System.Reflection.PortableExecutable;

using PDPCN = AasxPredefinedConcepts.ProductChangeNotifications;
using System.Windows.Documents;
using System.Linq;
using System.Globalization;
using System.Windows.Controls;
using AasxIntegrationBaseGdi;
using System.Windows;

namespace AasxPluginProductChangeNotifications
{
    public class PcnAnyUiControl
    {
        #region Members
        //=============

        private LogInstance _log = new LogInstance();
        private AdminShellPackageEnv _package = null;
        private Aas.Submodel _submodel = null;
        private PcnOptions _options = null;
        private PluginEventStack _eventStack = null;
        private PluginSessionBase _session = null;
        private AnyUiStackPanel _panel = null;
        private AasxPluginBase _plugin = null;

        private PDPCN.CD_ProductChangeNotifications _pcnData = new PDPCN.CD_ProductChangeNotifications();

        protected AnyUiSmallWidgetToolkit _uitk = new AnyUiSmallWidgetToolkit();

        protected static Dictionary<string, PDPCN.PcnReasonDescription> _dictIdToReason =
            PDPCN.PcnReasonDescription.BuildDict();

        protected static Dictionary<string, PDPCN.PcnItemDescription> _dictIdToItem =
            PDPCN.PcnItemDescription.BuildDict();

        #endregion

        #region Members to be kept for state/ update
        //=============

        protected double _lastScrollPosition = 0.0;

        protected int _selectedLangIndex = 0;
        protected string _selectedLangStr = null;

        protected int _pcnIndex = 0;

        #endregion

        #region Constructors
        //=============

        public PcnAnyUiControl()
        {
        }

        public void Dispose()
        {
        }

        public void Start(
            LogInstance log,
            AdminShellPackageEnv thePackage,
            Aas.Submodel theSubmodel,
            PcnOptions theOptions,
            PluginEventStack eventStack,
            PluginSessionBase session,
            AnyUiStackPanel panel,
            AasxPluginBase plugin)
        {
            // internal members
            _log = log;
            _package = thePackage;
            _submodel = theSubmodel;
            _options = theOptions;
            _eventStack = eventStack;
            _session = session;
            _panel = panel;
            _plugin = plugin;

            // fill given panel
            RenderFullView(_panel, _uitk, _package, _submodel);
        }

        public static PcnAnyUiControl FillWithAnyUiControls(
            LogInstance log,
            object opackage, object osm,
            PcnOptions options,
            PluginEventStack eventStack,
            PluginSessionBase session,
            object opanel,
            AasxPluginBase plugin)
        {
            // access
            var package = opackage as AdminShellPackageEnv;
            var sm = osm as Aas.Submodel;
            var panel = opanel as AnyUiStackPanel;
            if (package == null || sm == null || panel == null)
                return null;

            // the Submodel elements need to have parents
            sm.SetAllParents();

            // factory this object
            var aidCntl = new PcnAnyUiControl();
            aidCntl.Start(log, package, sm, options, eventStack, session, panel, plugin);

            // return shelf
            return aidCntl;
        }

        #endregion

        #region Display Submodel
        //=============

        private void RenderFullView(
            AnyUiStackPanel view, AnyUiSmallWidgetToolkit uitk,
            AdminShellPackageEnv package,
            Aas.Submodel sm)
        {
            // test trivial access
            if (_options == null || _submodel?.SemanticId == null)
                return;

            // make sure for the right Submodel
            var foundRecs = new List<PcnOptionsRecord>();
            foreach (var rec in _options.LookupAllIndexKey<PcnOptionsRecord>(
                _submodel?.SemanticId?.GetAsExactlyOneKey()))
                foundRecs.Add(rec);

            // try decode
            _pcnData = new PDPCN.CD_ProductChangeNotifications();
            PredefinedConceptsClassMapper.ParseAasElemsToObject(
                sm, _pcnData, 
                lambdaLookupReference: (rf) => package?.AasEnv?.FindReferableByReference(rf));

            // render
            RenderPanelOutside(view, uitk, foundRecs, package, sm, _pcnData);
        }

        protected void RenderPanelOutside(
            AnyUiStackPanel view, AnyUiSmallWidgetToolkit uitk,
            IEnumerable<PcnOptionsRecord> foundRecs,
            AdminShellPackageEnv package,
            Aas.Submodel sm,
            PDPCN.CD_ProductChangeNotifications data)
        {
            // make an outer grid, very simple grid of three rows: header, list, details
            var outer = view.Add(uitk.AddSmallGrid(rows: 7, cols: 1, colWidths: new[] { "*" }));

            //
            // Bluebar
            //

            var bluebar = uitk.AddSmallGridTo(outer, 0, 0, 1, cols: 5, colWidths: new[] { "*", "#", "#", "#", "#" });

            bluebar.Margin = new AnyUiThickness(0);
            bluebar.Background = AnyUiBrushes.LightBlue;

            uitk.AddSmallBasicLabelTo(bluebar, 0, 0, margin: new AnyUiThickness(8, 6, 0, 6),
                foreground: AnyUiBrushes.DarkBlue,
                fontSize: 1.5f,
                setBold: true,
                content: $"PCN #{_pcnIndex:D3}");

            Func<int, AnyUiLambdaActionBase> lambdaButtonClick = (i) => {
                // mode change
                if (data?.Records?.Record != null)
                    switch (i)
                    {
                        case 0:
                            _pcnIndex = 0;
                            break;
                        case 1:
                            _pcnIndex = Math.Max(0, _pcnIndex - 1);
                            break;
                        case 2:
                            _pcnIndex = Math.Min(data.Records.Record.Count - 1, _pcnIndex + 1);
                            break;
                        case 3:
                            _pcnIndex = data.Records.Record.Count - 1;
                            break;
                    }

                //redisplay
                PushUpdateEvent();
                return new AnyUiLambdaActionNone();
            };

            for (int i = 0; i < 4; i++)
            {
                var thisI = i;
                AnyUiUIElement.RegisterControl(
                    uitk.AddSmallButtonTo(bluebar, 0, 1 + i,
                        margin: new AnyUiThickness(2), setHeight: 21,
                        padding: new AnyUiThickness(2, 0, 2, 0),
                        content: (new[] { "\u2759\u25c0", "\u25c0", "\u25b6", "\u25b6\u2759" })[i]),
                        (o) => lambdaButtonClick(thisI));
            }

            //
            // Scroll area (list)
            //

            // small spacer
            outer.RowDefinitions[1] = new AnyUiRowDefinition(2.0, AnyUiGridUnitType.Pixel);
            uitk.AddSmallBasicLabelTo(outer, 1, 0,
                fontSize: 0.3f,
                verticalAlignment: AnyUiVerticalAlignment.Top,
                content: "", background: AnyUiBrushes.White);

            // add the body, a scroll viewer
            outer.RowDefinitions[2] = new AnyUiRowDefinition(1.0, AnyUiGridUnitType.Star);
            var scroll = AnyUiUIElement.RegisterControl(
                uitk.AddSmallScrollViewerTo(outer, 2, 0,
                    horizontalScrollBarVisibility: AnyUiScrollBarVisibility.Disabled,
                    verticalScrollBarVisibility: AnyUiScrollBarVisibility.Visible,
                    flattenForTarget: AnyUiTargetPlatform.Browser, initialScrollPosition: _lastScrollPosition),
                (o) =>
                {
                    if (o is Tuple<double, double> positions)
                    {
                        _lastScrollPosition = positions.Item2;
                    }
                    return new AnyUiLambdaActionNone();
                });

            // content of the scroll viewer
            // need a stack panel to add inside
            var inner = new AnyUiStackPanel()
            {
                Orientation = AnyUiOrientation.Vertical,
                Margin = new AnyUiThickness(2, 2, 8, 2)
            };
            scroll.Content = inner;

            if (foundRecs != null)
                foreach (var rec in foundRecs)
                    if (data?.Records?.Record != null
                        && _pcnIndex >= 0 && _pcnIndex < data.Records.Record.Count)
                        RenderPanelInner(inner, uitk, rec, package, sm, data.Records.Record[_pcnIndex]);
        }

        #endregion

        #region Inner
        //=============

        protected AnyUiLambdaActionBase TriggerUpdate(bool full = true)
        {
            // trigger a complete redraw, as the regions might emit 
            // events or not, depending on this flag
            return new AnyUiLambdaActionPluginUpdateAnyUi()
            {
                PluginName = _plugin?.GetPluginName(),
                UpdateMode = AnyUiRenderMode.All,
                UseInnerGrid = true
            };
        }

        protected int _innerDocumentCols = 6;

        protected int InnerDocGetNewRow(AnyUiGrid grid)
        {
            if (grid == null)
                return 0;
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new AnyUiRowDefinition());
            return row;
        }

        protected void InnerDocAddHeadline(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col,
            string heading, int level,
            int assignRow = -1)
        {
            // access and add row
            if (grid == null)
                return;
            int row = (assignRow >= 0) ? assignRow : InnerDocGetNewRow(grid);

            var bg = AnyUi.AnyUiBrushes.DarkBlue;
            var fg = AnyUi.AnyUiBrushes.White;
            var bold = true;

            if (level == 2)
            {
                bg = AnyUi.AnyUiBrushes.LightBlue;
                fg = AnyUi.AnyUiBrushes.Black;
                bold = true;
            }

            uitk.Set(
                uitk.AddSmallBasicLabelTo(grid, row, col,
                    colSpan: _innerDocumentCols - col,
                    background: bg,
                    foreground: fg,
                    content: heading,
                    setBold: bold,
                    padding: new AnyUiThickness(2,0,0,0)),
                margin: new AnyUiThickness(0, 14, 0, 6));
        }

        protected void InnerDocAddText(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col, int keyCols,
            string[] texts,            
            int[] bold = null,
            int[] wrap = null)
        {
            // access and add row
            if (grid == null || texts == null)
                return;
            int row = InnerDocGetNewRow(grid);

            // key
            if (texts.Length > 0)
            {
                uitk.AddSmallBasicLabelTo(grid, row, col,
                    colSpan: _innerDocumentCols - col,
                    content: texts[0],
                    setBold: bold != null && bold.Contains(0),
                    textWrapping: (wrap != null && wrap.Contains(0)) 
                        ? AnyUiTextWrapping.Wrap : null);
            }

            // text(s)
            for (int i = 1; i < texts.Length; i++)
            {
                uitk.AddSmallBasicLabelTo(grid, row, col + keyCols + i - 1,
                    colSpan: (i != texts.Length - 1) 
                        ? 1 
                        : _innerDocumentCols - col - keyCols - (texts.Length - 2),
                    content: texts[i],
                    setBold: bold != null && bold.Contains(i),
                    textWrapping: (wrap != null && wrap.Contains(i))
                        ? AnyUiTextWrapping.Wrap : null);
            }
        }

        protected void InnerDocAddText(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col, int keyCols,
            string key,
            string text,
            AnyUiTextWrapping? wrapText = null)
        {
            InnerDocAddText(
                uitk, grid, col, keyCols,
                new[] { key, text },
                bold: new[] { 0 },
                wrap: (wrapText != AnyUiTextWrapping.Wrap) ? null : (new[] { 1 }));
        }

        protected enum IDCellFormat { 
            None = 0, 
            Bold = 1,
            Wrap = 2,
            Centered = 4,
            Button = 8,
            Url = 16
         }

        protected void InnerDocAddGridCells(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid,
            string[] cell,
            IDCellFormat[] cellFormat = null,
            Func<int, AnyUiLambdaActionBase> lambdaClick = null,
            int minRowHeight = -1)
        {
            // access and add row
            if (grid == null || cell == null)
                return;
            int row = InnerDocGetNewRow(grid);

            // adjust row height?
            if (minRowHeight >= 0 && row < grid.RowDefinitions.Count)
                grid.RowDefinitions[row].MinHeight = minRowHeight;

            // text(s)
            for (int i = 0; i < cell.Length; i++)
            {
                // lambda
                var thisI = i;
                // format?
                var cf = (cellFormat == null || i >= cellFormat.Length) 
                            ? IDCellFormat.None : cellFormat[i];

                // make the border
                var brd = uitk.AddSmallBorderTo(grid, row, i,
                    margin: (i == 0) ? new AnyUiThickness(0, -1, 0, 0)
                                     : new AnyUiThickness(-1, -1, 0, 0),
                    borderThickness: new AnyUiThickness(1.0),
                    borderBrush: AnyUiBrushes.Black);

                // make the inner
                if ((cf & IDCellFormat.Button) > 0)
                {
                    if (cell[i]?.HasContent() == true)
                        brd.Child =
                            AnyUiUIElement.RegisterControl(
                                new AnyUiButton()
                                {
                                    Content = "" + cell[i],
                                    Padding = new AnyUiThickness(2, -1, 2, -1),
                                    Margin = new AnyUiThickness(2),
                                    VerticalAlignment = AnyUiVerticalAlignment.Center,
                                    VerticalContentAlignment = AnyUiVerticalAlignment.Center
                                },
                                setValue: (o) => lambdaClick(thisI));
                }
                else
                {
                    var stb = new AnyUiSelectableTextBlock()
                    {
                        Text = "" + cell[i],
                        Padding = new AnyUiThickness(2, 0, 2, 0),
                        FontWeight = ((cf & IDCellFormat.Bold) > 0)
                            ? AnyUiFontWeight.Bold : null,
                        TextWrapping = ((cf & IDCellFormat.Wrap) > 0)
                            ? AnyUiTextWrapping.WrapWithOverflow : null,
                        VerticalAlignment = AnyUiVerticalAlignment.Center,
                        VerticalContentAlignment = AnyUiVerticalAlignment.Center
                    };

                    brd.Child = stb;
               
                    if ((cf & IDCellFormat.Centered) > 0)
                    {
                        stb.HorizontalAlignment = AnyUiHorizontalAlignment.Center;
                        stb.HorizontalContentAlignment = AnyUiHorizontalAlignment.Center;
                    }
                }
            }
        }

        protected void InnerDocAddLifeCycleMilestones(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col, 
            IList<PDPCN.CD_LifeCycleMilestone> milestones)
        {
            // access and add row
            if (grid == null || milestones == null || milestones.Count < 1)
                return;
            int row = InnerDocGetNewRow(grid);

            // make inner grid
            var inner = uitk.Set(
                uitk.AddSmallGridTo(grid, row, col, 
                    rows: 1, cols: milestones.Count + 1,
                    colWidths: "#".Times(milestones.Count).Add("*").ToArray()),
                colSpan: _innerDocumentCols - col);

            // have milestone name to color mapping
            var x = new Dictionary<string, AnyUiBrush>();
            x.Add("SOP",  new AnyUiBrush("#0128CB"));
            x.Add("NRND", new AnyUiBrush("#F9F871"));
            x.Add("PCN",  new AnyUiBrush("#00B9CD"));
            x.Add("PDN",  new AnyUiBrush("#FFBA44"));
            x.Add("EOS",  new AnyUiBrush("#FF724F"));
            x.Add("EOP",  new AnyUiBrush("#FF0076"));
            x.Add("LTD",  new AnyUiBrush("#B10000"));
            x.Add("EOSR", new AnyUiBrush("#C400A6"));

            // single "boxes"
            for (int ci=0; ci<milestones.Count; ci++) 
            {
                // make the border
                var brd = uitk.AddSmallBorderTo(inner, 0, ci,
                                margin: (ci == 0) ? new AnyUiThickness(0, -1, 0, 0)
                                                  : new AnyUiThickness(-1, -1, 0, 0),
                                borderThickness: new AnyUiThickness(1.0), 
                                borderBrush: AnyUiBrushes.Black);

                // find a nice color coding
                var bg = AnyUiBrushes.Transparent;
                var lookup = ("" + milestones[ci].MilestoneClassification).ToUpper().Trim();
                if (x.ContainsKey(lookup))
                    bg = x[lookup];
                var fg = AnyUiBrushes.Black;
                if (bg.Color.Blackness() > 0.5)
                    fg = AnyUiBrushes.White;

                // provide a nice date
                var vd = "" + milestones[ci].DateOfValidity;
                if (DateTime.TryParseExact(vd,
                    "yyyy-MM-dd'T'HH:mm:ssZ", CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out DateTime dt))
                {
                    var delta = dt - DateTime.UtcNow;
                    var sign = (delta > TimeSpan.Zero) ? "in" : "ago";
                    var days = Convert.ToInt32(Math.Abs(delta.TotalDays));
                    var years = 0;
                    if (days > 365)
                    {
                        years = days / 365;
                        days = days % 365;
                    }
                    vd = $"{sign} {(years > 0 ? $"{years} yrs " : "")} {days} days";
                }

                // make the 2 row content grid
                var mg = uitk.AddSmallGrid(2, 1, new[] { "110:" }, new[] { "40:", "#" },
                background: bg,
                margin: new AnyUiThickness(2));
                brd.Child = mg;

                uitk.AddSmallBasicLabelTo(mg, 0, 0,   
                    foreground: fg,
                    fontSize: 1.5f,
                    setBold: true,
                    horizontalAlignment: AnyUiHorizontalAlignment.Center,
                    horizontalContentAlignment: AnyUiHorizontalAlignment.Center,
                    verticalAlignment: AnyUiVerticalAlignment.Center,
                    verticalContentAlignment: AnyUiVerticalAlignment.Center,
                    content: "" + milestones[ci].MilestoneClassification);

                uitk.AddSmallBasicLabelTo(mg, 1, 0,
                    foreground: fg,
                    fontSize: 0.8f,
                    setBold: false,
                    horizontalAlignment: AnyUiHorizontalAlignment.Center,
                    horizontalContentAlignment: AnyUiHorizontalAlignment.Center,
                    content: vd,
                    margin: new AnyUiThickness(0, 0, 0, 4));
            }
        }

        protected void InnerDocAddAffectedPartNumbers(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col,
            IList<string> partNumbers)
        {
            // access and add row
            if (grid == null || partNumbers == null || partNumbers.Count < 1)
                return;
            int row = InnerDocGetNewRow(grid);

            // set layout
            var numRows = 1 + partNumbers.Count / 3;

            // make inner grid
            var inner = uitk.Set(
                uitk.AddSmallGridTo(grid, row, col,
                    rows: numRows, cols: 3,
                    colWidths: "*".Times(3).ToArray()),
                colSpan: _innerDocumentCols - col);

            // single "boxes"
            for (int i = 0; i < partNumbers.Count; i++)
            {
                // row, col
                var ri = i % numRows;
                var ci = i / numRows;

                // make the border
                var brd = uitk.AddSmallBorderTo(inner, ri, ci,
                    margin: (ci == 0) ? new AnyUiThickness(0, -1, 0, 0)
                                        : new AnyUiThickness(-1, -1, 0, 0),
                    borderThickness: new AnyUiThickness(1.0),
                    borderBrush: AnyUiBrushes.Black);

                brd.Child = new AnyUiSelectableTextBlock()
                {
                    Text = "" + partNumbers[i],
                    Padding = new AnyUiThickness(1),
                    FontSize = 0.9f,
                    TextWrapping = AnyUiTextWrapping.Wrap,
                    HorizontalAlignment = AnyUiHorizontalAlignment.Center,
                    HorizontalContentAlignment = AnyUiHorizontalAlignment.Center,
                    VerticalAlignment = AnyUiVerticalAlignment.Center,
                    VerticalContentAlignment = AnyUiVerticalAlignment.Center
                };
            }
        }

        protected void InnerDocDisplaySaveFile(
            string uri, string contentType, bool display, bool save)
        {
            // first check
            if (uri == null || uri.Trim().Length < 1
                || _eventStack == null)
                return;

            try
            {
                // temp input
                var inputFn = uri;
                try
                {
                    if (!inputFn.ToLower().Trim().StartsWith("http://")
                            && !inputFn.ToLower().Trim().StartsWith("https://"))
                        inputFn = _package?.MakePackageFileAvailableAsTempFile(inputFn);
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, "Making local file available");
                }

                // give over to event stack
                _eventStack?.PushEvent(new AasxPluginResultEventDisplayContentFile()
                {
                    SaveInsteadDisplay = save,
                    ProposeFn = System.IO.Path.GetFileName(uri),
                    Session = _session,
                    fn = inputFn,
                    mimeType = contentType
                });
            }
            catch (Exception ex)
            {
                _log?.Error(ex, "when executing file action");
            }
        }

        protected void InnerDocAddAdditionalInfo(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col,
            IList<AasClassMapperFile> addInfo)
        {
            // access and add row
            if (grid == null || addInfo == null || addInfo.Count < 1)
                return;
            int row = InnerDocGetNewRow(grid);

            // make inner grid
            var inner = uitk.Set(
                uitk.AddSmallGridTo(grid, row, col,
                    rows: addInfo.Count, cols: 3,
                    colWidths: new[] { "#", "*", "#" }),
                colSpan: _innerDocumentCols - col);

            // single "boxes"
            for (int i = 0; i < addInfo.Count; i++)
            {
                // handle good & bad path values as uri
                var uriStr = addInfo[i].Value;
                var contentType = addInfo[i].ContentType;
                var uriValid = AdminShellUtil.TryReFormatAsValidUri(ref uriStr);

                // border: symbol
                var brdSymbol = uitk.AddSmallBorderTo(inner, i, 0,
                    margin: (true) ? new AnyUiThickness(0, -1, 0, 0)
                                        : new AnyUiThickness(-1, -1, 0, 0),
                    borderThickness: new AnyUiThickness(1.0),
                    background: AnyUiBrushes.DarkGray,
                    borderBrush: AnyUiBrushes.DarkGray);

                // child: symbol
                brdSymbol.Child = new AnyUiSelectableTextBlock()
                {
                    Text = "\U0001F5CE",
                    Background = AnyUiBrushes.DarkGray,
                    Foreground = AnyUiBrushes.White,
                    Padding = new AnyUiThickness(4, 2, 4, 2),
                    FontSize = 1.0f,
                    FontWeight = AnyUiFontWeight.Bold,
                    HorizontalAlignment = AnyUiHorizontalAlignment.Center,
                    HorizontalContentAlignment = AnyUiHorizontalAlignment.Center,
                    VerticalAlignment = AnyUiVerticalAlignment.Center,
                    VerticalContentAlignment = AnyUiVerticalAlignment.Center
                };

                // border: inner link
                var brdLink = uitk.AddSmallBorderTo(inner, i, 1,
                    margin: (false) ? new AnyUiThickness(0, -1, 0, 0)
                                        : new AnyUiThickness(-1, -1, 0, 0),
                    borderThickness: new AnyUiThickness(1.0),
                    borderBrush: AnyUiBrushes.DarkGray);

                // child: inner link
                brdLink.Child =
                    AnyUiUIElement.RegisterControl(
                        new AnyUiSelectableTextBlock()
                        {
                            Text = "" + uriStr,
                            Padding = new AnyUiThickness(4, 2, 4, 2),
                            FontSize = 1.0f,
                            TextWrapping = AnyUiTextWrapping.Wrap,
                            TextAsHyperlink = uriValid,
                            VerticalAlignment = AnyUiVerticalAlignment.Center,
                            VerticalContentAlignment = AnyUiVerticalAlignment.Center
                        },
                        setValue: (o) =>
                        {
                            InnerDocDisplaySaveFile(uriStr, contentType,
                                display: true, save: false);
                            return new AnyUiLambdaActionNone();
                        });

                // border: button
                var brdBtn = uitk.AddSmallBorderTo(inner, i, 2,
                    margin: (false) ? new AnyUiThickness(0, -1, 0, 0)
                                        : new AnyUiThickness(-1, -1, 0, 0),
                    borderThickness: new AnyUiThickness(1.0),
                    borderBrush: AnyUiBrushes.DarkGray);

                // child: button
                brdBtn.Child =
                    AnyUiUIElement.RegisterControl(
                        new AnyUiButton()
                        {
                            Content = "\U0001f80b",
                            Padding = new AnyUiThickness(2, -3, 2, -3),
                            Margin = new AnyUiThickness(2),
                            FontSize = 0.8f,
                            HorizontalAlignment = AnyUiHorizontalAlignment.Center,
                            HorizontalContentAlignment = AnyUiHorizontalAlignment.Center
                        },
                        setValue: (o) =>
                        {
                            InnerDocDisplaySaveFile(uriStr, contentType,
                                display: false, save: true);
                            return new AnyUiLambdaActionNone();
                        });
            }
        }

        protected void InnerDocAddTechnicalDataChanges(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col,
            AasClassMapperInfo info)
        {
            // access and add row
            if (grid == null 
                || !(info?.Referable is Aas.ISubmodelElementCollection smc)
                || smc.Value == null)
                return;
            int row = InnerDocGetNewRow(grid);

            // access definitions
            var theDefs = AasxPredefinedConcepts.IdtaProductChangeNotificationsV10.Static;
            var mm = MatchMode.Relaxed;

            // make inner grid
            var inner = uitk.Set(
                uitk.AddSmallGridTo(grid, row, col,
                    rows: 0, cols: 5,
                    colWidths: new[] { "#", "*", "#", "#", "#" }),
                colSpan: _innerDocumentCols - col);

            // add head line
            InnerDocAddGridCells(uitk, inner,
                cell: new[] { 
                    "IdShort", "SemanticId", "New value", "Reason", "Origin" 
                },
                cellFormat: new[] { 
                    IDCellFormat.Bold, IDCellFormat.Bold, IDCellFormat.Bold,
                    IDCellFormat.Bold, IDCellFormat.Bold 
                });

            // lambda for adding things
            Action<string, Aas.IReference, Aas.IReferenceElement, string, Aas.IProperty> lambdaAddLine 
                = (ids, semId, origin, newVal, reason) =>
            {
                InnerDocAddGridCells(uitk, inner,
                    cell: new[] { 
                        "" + ids, 
                        "" + semId?.ToStringExtended(2), 
                        "" + newVal, 
                        "" + reason?.ValueAsText(), 
                        (origin?.Value?.IsValid() == true) ? "\U0001F81E" : null 
                    },
                    cellFormat: new[] {
                        IDCellFormat.Wrap, IDCellFormat.Wrap, IDCellFormat.Wrap,
                        IDCellFormat.Wrap, IDCellFormat.Button
                    },
                    minRowHeight: 20,
                    lambdaClick: (i) =>
                    {
                        if (i == 4 && origin?.Value?.IsValid() == true)
                        {
                            // send event to main application
                            var evt = new AasxPluginResultEventNavigateToReference();
                            evt.targetReference = origin.Value;
                            this._eventStack.PushEvent(evt);
                        }
                        return new AnyUiLambdaActionNone();
                    });
            };

            //
            // Approach (1)
            // search the top SMC for CD single change
            //
            foreach (var smcSingleChange in smc.Value
                .FindAllSemanticIdAs<Aas.SubmodelElementCollection>(
                    theDefs.CD_SingleChange?.GetReference(), mm))
            {
                // detect information by excluding known ..
                Aas.ISubmodelElement changedSme = null;
                Aas.IReferenceElement originOfChange = null;
                Aas.IProperty reasonId = null;

                // look all elements
                foreach (var sme in smcSingleChange?.Value.AsNotNull())
                {
                    if (true == sme.SemanticId?.Matches(
                        theDefs.CD_Origin_of_change?.GetReference(), mm)
                        && sme is Aas.IReferenceElement rfe)
                        originOfChange = rfe;
                    else
                    if (true == sme.SemanticId?.Matches(
                        theDefs.CD_ReasonId?.GetReference(), mm)
                        && sme is Aas.IProperty prop)
                        reasonId = prop;
                    else
                        changedSme = sme;
                }

                // sufficient?
                if (changedSme != null)
                    lambdaAddLine(
                        changedSme.IdShort,
                        changedSme.SemanticId,
                        originOfChange,
                        changedSme.ValueAsText(),
                        reasonId);
            }

            //
            // Approach (2) 
            // if it is not a SingleChange, it has to be a change with
            // arbitrary semanticId
            //
            foreach (var sme2 in smc.Value)
                if (sme2 is Aas.ISubmodelElementCollection smc2
                    && smc2.Value != null
                    && true != sme2.SemanticId?.Matches(theDefs.CD_SingleChange?.GetReference(), mm))
                {
                    // find some members
                    var newValue = smc2.Value.FindFirstSemanticIdAs<Aas.ISubmodelElement>(
                        theDefs.CD_NewValueOfChange?.GetReference(), matchMode: mm);
                    var originOfChange = smc2.Value.FindFirstSemanticIdAs<Aas.IReferenceElement>(
                        theDefs.CD_Origin_of_change?.GetReference(), matchMode: mm);
                    var reasonId = smc2.Value.FindFirstSemanticIdAs<Aas.IProperty>(
                        theDefs.CD_ReasonId?.GetReference(), matchMode: mm);

                    // new value has to be given
                    if (smc2.IdShort?.HasContent() == true
                        && smc2.SemanticId?.IsValid() == true
                        && newValue != null)
                        lambdaAddLine(
                            smc2.IdShort,
                            smc2.SemanticId,
                            originOfChange,
                            newValue.ValueAsText(),
                            reasonId);
                }
        }

        protected class IDTDCEntry
        {
            public string IdShort;
            public string SemanticId;
            public Dictionary<int, string> Values;
        }

        protected void InnerDocAddTechnicalDataCompare(
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid, int col,
            AasClassMapperInfo currentState,
            AasClassMapperInfo[] compareToState = null,
            string[] compareToName = null,
            bool filterForMin2Values = false,
            bool indicateCurrentStatePresent = false)
        {
            // access and add row
            if (grid == null
                || !(currentState?.Referable is Aas.ISubmodelElementCollection smc)
                || smc.Value == null)
                return;
            int row = InnerDocGetNewRow(grid);

            // access definitions
            var theDefs = AasxPredefinedConcepts.IdtaProductChangeNotificationsV10.Static;
            var mm = MatchMode.Relaxed;

            // figure out, how many compareTo items
            var compareToNum = 0;
            if (compareToState != null && compareToName != null)
                compareToNum = Math.Min(compareToState.Length, compareToName.Length);

            // make inner grid
            var inner = uitk.Set(
                uitk.AddSmallGridTo(grid, row, col,
                    rows: 0, cols: 3 + compareToNum,
                    colWidths: new[] { "#", "*", "#" }.Add("#".Times(compareToNum)).ToArray() ),
                colSpan: _innerDocumentCols - col);

            // add head line
            InnerDocAddGridCells(uitk, inner,
                cell: (new[] { "IdShort", "SemanticId", "CurrentState" }
                        .Add(compareToName))
                        .ToArray(),
                cellFormat: (new[] { IDCellFormat.Bold, IDCellFormat.Bold, IDCellFormat.Bold })
                        .Add(IDCellFormat.Bold.Times(compareToNum))
                        .ToArray()
                );

            // target estimates
            if (compareToNum > 0)
            {
                var cells = new[] { "Target estimate", "-", "-" }.ToList();
                for (int i=0; i< compareToNum; i++)
                    if (compareToState[i]?.Referable is Aas.ISubmodelElementCollection smc2)
                    {
                        var te = "" + smc2.Value?.FindFirstSemanticIdAs<Aas.IProperty>(
                            theDefs.CD_TargetEstimate, mm)?.ValueAsText();
                        cells.Add(te);
                    }
                InnerDocAddGridCells(uitk, inner, cell: cells.ToArray(),
                    cellFormat: new[] { IDCellFormat.Bold } );
            }

            // now build a dictionary of all cited semanticIds by providing a
            // lambda to index multiple containers
            var dict = new Dictionary<string, IDTDCEntry>();
            
            Action<AasClassMapperInfo, int> lambdaIndex = (cmi, index) =>
            {
                if (!(cmi?.Referable is Aas.ISubmodelElementCollection smc3))
                    return;
                foreach (var sme in smc3.Value.AsNotNull())
                {
                    // require a valid semantic id
                    if (sme?.SemanticId?.IsValid() != true)
                        continue;
                    // and this should not be target estite
                    if (sme.SemanticId.Matches(theDefs.CD_TargetEstimate?.GetReference(), mm) == true)
                        continue;
                    // check if present
                    var semid = sme.SemanticId.ToStringExtended(format: 2).Trim().ToLower();
                    if (!dict.ContainsKey(semid))
                    {
                        // create new
                        var ne = new IDTDCEntry()
                        {
                            IdShort = "" + sme.IdShort,
                            SemanticId = sme.SemanticId.ToStringExtended(format: 2),
                            Values = new Dictionary<int, string>()
                        };
                        ne.Values[index] = sme.ValueAsText();

                        if (indicateCurrentStatePresent && index == 0
                            && sme.ValueAsText()?.HasContent() != true)
                            ne.Values[index] = "<present>";

                        dict.Add(semid, ne);
                    }
                    else
                    {
                        // update carefully (first for each index wins!)
                        var ee = dict[semid]; 
                        if (ee.Values != null && !ee.Values.ContainsKey(index))
                            ee.Values[index] = sme.ValueAsText();
                    }
                }
            };

            lambdaIndex(currentState, 0);
            for (int i = 0; i < compareToNum; i++)
                lambdaIndex(compareToState[i], 1 + i);

            // simply dump the entries out
            foreach (var ek in dict.Keys)
            {
                var entry = dict[ek];

                if (filterForMin2Values
                    && (entry.Values == null || entry.Values.Count < 2))
                    continue;

                var cells = new[] { 
                    "" + entry.IdShort, 
                    "" + entry.SemanticId
                }.ToList();
                
                for (int i = 0; i <= compareToNum; i++)
                    if (entry.Values?.ContainsKey(i) == true)
                        cells.Add("" + entry.Values[i]);
                    else
                        cells.Add("");
                
                InnerDocAddGridCells(uitk, inner, 
                    cell: cells.ToArray(),
                    cellFormat: IDCellFormat.Wrap.Times(3 + compareToNum).ToArray());
            }
        }

        protected void InnerDocIdentificationData(
            AdminShellPackageEnv package, 
            AnyUiSmallWidgetToolkit uitk,
            AnyUiGrid grid,
            string header,
            AasClassMapperHintedReference manufacturerAssetID,
            string manufacturerProductFamily,
            string manufacturerProductDesignation,
            string orderCodeOfManufacturer,
            string hardwareVersion)
        {
            // make a grid for two columns
            int row = InnerDocGetNewRow(grid);
            var twoColGrid =
                uitk.Set(
                    uitk.AddSmallGridTo(grid, row, 0, rows: 1, cols: 2, new[] { "2*", "1*" }),
                    colSpan: _innerDocumentCols);

            // access asset information
            if (manufacturerAssetID?.ValueHint is Aas.IAssetAdministrationShell aas
                && aas.AssetInformation is Aas.IAssetInformation ai
                && ai.DefaultThumbnail?.Path != null)
            {
                var img = AnyUiGdiHelper.LoadBitmapInfoFromPackage(package, ai.DefaultThumbnail.Path);

                uitk.Set(
                    uitk.AddSmallImageTo(twoColGrid, 0, 1,
                        margin: new AnyUiThickness(2, 8, 2, 2),
                        stretch: AnyUiStretch.Uniform,
                        bitmap: img),
                maxHeight: 300, maxWidth: 300,
                horizontalAlignment: AnyUiHorizontalAlignment.Stretch,
                verticalAlignment: AnyUiVerticalAlignment.Stretch);
            }

            // identification info
            if (true)
            {
                var colTwoGrid =
                uitk.Set(
                    uitk.AddSmallGridTo(twoColGrid, 0, 0,
                        rows: 1, cols: 3, new[] { "70:", "70:", "*" }));

                InnerDocAddHeadline(uitk, colTwoGrid, 0, "" + header, 1,
                    assignRow: 0);

                InnerDocAddText(uitk, colTwoGrid, 0, 2, "ManufacturerAssetID:",
                    "" + manufacturerAssetID?.Value?.ToStringExtended(1),
                    wrapText: AnyUiTextWrapping.Wrap);

                InnerDocAddText(uitk, colTwoGrid, 0, 2, "Mfg.Prod.Family:",
                    "" + manufacturerProductFamily);

                InnerDocAddText(uitk, colTwoGrid, 0, 2, "Mfg.Prod.Deign.:",
                    "" + manufacturerProductDesignation);

                InnerDocAddText(uitk, colTwoGrid, 0, 2, "Order Code Mfg.:",
                    "" + orderCodeOfManufacturer);

                if (hardwareVersion != null)
                InnerDocAddText(uitk, grid, 0, 2, "HardwareVersion:",
                    hardwareVersion);
            }
        }

        protected void InnerDocAddProductClassifications(
            AnyUiSmallWidgetToolkit uitk, AnyUiGrid grid,
            IList<PDPCN.CD_ProductClassification> pds)
        {
            if (pds != null && pds.Count > 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Product classification(s)", 2);

                foreach (var pc in pds)
                {
                    InnerDocAddText(uitk, grid, 0, 2, "" + pc.ProductClassId,
                        ("" + pc.ClassificationSystem)
                        .AddWithDelimiter("" + pc.VersionOfClassificationSystem, delimter: ":"));
                }
            }
        }

        protected void RenderPanelInner(
            AnyUiStackPanel view, AnyUiSmallWidgetToolkit uitk,
            PcnOptionsRecord rec,
            AdminShellPackageEnv package,
            Aas.Submodel sm,
            PDPCN.CD_Record data)
        {
            // access
            if (view == null || uitk == null || sm == null || rec == null)
                return;

            // 
            // Document approach via Grid.
            // Basically one large grid, which helps synchronizing multiple identation levels.
            //

            var grid = view.Add(uitk.AddSmallGrid(rows: 5, cols: _innerDocumentCols, 
                colWidths: new[] { "70:", "70:", "70:", "70:", "70:", "*" }));

            // Start
            InnerDocAddHeadline(uitk, grid, 0, "Record", 1);

            InnerDocAddText(uitk, grid, 0, 2, "DateOfRecord:",
                    "" + data.DateOfRecord);

            // Manufacturer
            if (data.Manufacturer != null)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Manufacturer", 2);
                
                InnerDocAddText(uitk, grid, 0, 2, "ManufacturerName:", 
                    data.Manufacturer.ManufacturerName?.GetDefaultString(_selectedLangStr));

                InnerDocAddText(uitk, grid, 0, 2, "AdressInformation:",
                    "<TBD>");

                InnerDocAddText(uitk, grid, 0, 2, "ManufacturerChangeID:",
                    "" + data.ManufacturerChangeID);
            }

            // Life cycle mile stones
            if (data.LifeCycleData?.Milestone != null && data.LifeCycleData.Milestone.Count > 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Life cycle mile stones changed by this notification", 2);

                InnerDocAddLifeCycleMilestones(uitk, grid, 0, data.LifeCycleData.Milestone);
            }

            // Reasons of change
            if (data.ReasonsOfChange?.ReasonOfChange != null && data.ReasonsOfChange.ReasonOfChange.Count > 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Reasons of change", 2);

                foreach (var roc in data.ReasonsOfChange.ReasonOfChange)
                {
                    var rid = ("" + roc.ReasonId).ToUpper().Trim();
                    if (_dictIdToReason.ContainsKey(rid))
                    {
                        var rd = _dictIdToReason[rid];
                        InnerDocAddText(uitk, grid, 0, 2, rd.Id,
                            rd.Description,
                            wrapText: AnyUiTextWrapping.Wrap);
                    }
                    else
                    {
                        InnerDocAddText(uitk, grid, 0, 2, "" + roc.ReasonId,
                            ("" + roc.ReasonClassificationSystem)
                            .AddWithDelimiter("" + roc.VersionOfClassificationSystem, delimter: ":"));
                    }
                }
            }

            // asset, partnumbers, items
            if (data.ItemOfChange != null)
            {
                InnerDocIdentificationData(
                    package, uitk, grid, 
                    header: "Item of change (1)",
                    manufacturerAssetID: data.ItemOfChange.ManufacturerAssetID,
                    manufacturerProductFamily: "" + data.ItemOfChange
                        .ManufacturerProductFamily?.GetDefaultString(_selectedLangStr),
                    manufacturerProductDesignation: "" + data.ItemOfChange
                        .ManufacturerProductDesignation?.GetDefaultString(_selectedLangStr),
                    orderCodeOfManufacturer: "" + data.ItemOfChange
                        .OrderCodeOfManufacturer?.GetDefaultString(_selectedLangStr),
                    hardwareVersion: "" + data.ItemOfChange.HardwareVersion);
            }

            // part numbers
            if (data.AffectedPartNumbers?.AffectedPartNumber != null 
                && data.AffectedPartNumbers.AffectedPartNumber.Count >= 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Affected part numbers", 2);

                InnerDocAddAffectedPartNumbers(uitk, grid, 0,
                    data.AffectedPartNumbers.AffectedPartNumber);
            }

            // Item categories
            if (data.ItemCategories?.ItemCategory != null && data.ItemCategories.ItemCategory.Count > 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Item categories", 2);

                foreach (var ic in data.ItemCategories.ItemCategory)
                {
                    var cid = ("" + ic.ItemCategory).ToUpper().Trim();
                    if (_dictIdToItem.ContainsKey(cid))
                    {
                        var cd = _dictIdToItem[cid];
                        InnerDocAddText(uitk, grid, 0, 2, cd.Id,
                            cd.Description,
                            wrapText: AnyUiTextWrapping.Wrap);
                    }
                    else
                    {
                        InnerDocAddText(uitk, grid, 0, 2, "" + ic.ItemCategory,
                            ("" + ic.ItemClassificationSystem)
                            .AddWithDelimiter("" + ic.VersionOfClassificationSystem, delimter: ":"));
                    }
                }
            }

            // human readable infos
            if (true)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Human readable change information", 2);

                InnerDocAddText(uitk, grid, 0, 2, "PcnChg.Info/Title:",
                    "" + data.PcnChangeInformation?.ChangeTitle?.GetDefaultString(_selectedLangStr));

                InnerDocAddText(uitk, grid, 0, 2, "PcnChg.Info/Details:",
                    "" + data.PcnChangeInformation?.ChangeDetail?.GetDefaultString(_selectedLangStr),
                    wrapText: AnyUiTextWrapping.Wrap);

                InnerDocAddText(uitk, grid, 0, 2, "PcnReasonComment:",
                    "" + data.PcnReasonComment?.GetDefaultString(_selectedLangStr),
                    wrapText: AnyUiTextWrapping.Wrap);

            }

            // additional infos
            if (data.AdditionalInformations?.AdditionalInformation != null
                && data.AdditionalInformations.AdditionalInformation.Count >= 0)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Additional information provided by documents", 2);

                InnerDocAddAdditionalInfo(uitk, grid, 0,
                    data.AdditionalInformations.AdditionalInformation);
            }

            // further of item of change
            if (data.ItemOfChange != null)
            {
                InnerDocAddHeadline(uitk, grid, 0, "ItemOfChange (2)", 1);

                InnerDocAddHeadline(uitk, grid, 0, "Remaining stock information", 2);

                InnerDocAddText(uitk, grid, 0, 2, "Rem.AmountAvail.:",
                    "" + data.ItemOfChange.RemainingAmountAvailable);

                // product classifications
                InnerDocAddProductClassifications(
                    uitk, grid, 
                    data.ItemOfChange.ProductClassifications?.ProductClassification);
            }

            // technical data - changes
            if (data.ItemOfChange?.TechnicalData_Changes != null)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Given changes of technical data for the item of change", 2);

                InnerDocAddTechnicalDataChanges(uitk, grid, 0,
                    data.ItemOfChange.TechnicalData_Changes.__Info__);
            }

            // technical data - current state (no compare items)
            if (data.ItemOfChange.TechnicalData_CurrentState?.__Info__?.Referable != null)
            {
                InnerDocAddHeadline(uitk, grid, 0, "Technical data provided for comparison with recommendations", 2);

                InnerDocAddTechnicalDataCompare(
                    uitk, grid, 0,
                    currentState: data.ItemOfChange.TechnicalData_CurrentState.__Info__);
            }

            // now, for all recommended items
            int rin = 0;
            foreach (var ri in data.RecommendedItems?.RecommendedItem.AsNotNull()) 
            {
                // weird numeric loop
                var riIndex = rin++;
                if (ri == null)
                    continue;
                
                // header + manuf. identification
                InnerDocIdentificationData(
                    package, uitk, grid,
                    header: $"Recommended item #{riIndex:D3}",
                    manufacturerAssetID: ri.ManufacturerAssetID,
                    manufacturerProductFamily: "" + ri
                        .ManufacturerProductFamily?.GetDefaultString(_selectedLangStr),
                    manufacturerProductDesignation: "" + ri
                        .ManufacturerProductDesignation?.GetDefaultString(_selectedLangStr),
                    orderCodeOfManufacturer: "" + ri
                        .OrderCodeOfManufacturer?.GetDefaultString(_selectedLangStr),
                    hardwareVersion: null);

                // product classifications
                InnerDocAddProductClassifications(
                    uitk, grid,
                    ri.ProductClassifications?.ProductClassification);

                // comparison of technical data
                var compareToState = new List<AasClassMapperInfo>();
                var compareToName = new List<string>();
                Action<AasClassMapperInfo, string> lambdaCheckCompTo = (state, name) =>
                {
                    if (state != null)
                    {
                        compareToState.Add(state);
                        compareToName.Add(name);
                    }
                };
                lambdaCheckCompTo(ri.TechnicalData_Fit?.__Info__, "Fit");
                lambdaCheckCompTo(ri.TechnicalData_Form?.__Info__, "Form");
                lambdaCheckCompTo(ri.TechnicalData_Function?.__Info__, "Function");
                lambdaCheckCompTo(ri.TechnicalData_Other?.__Info__, "Other");

                if (data.ItemOfChange?.TechnicalData_CurrentState != null
                    && compareToState.Count > 0)
                {
                    InnerDocAddHeadline(uitk, grid, 0, "Technical data comparison of fit/ form/ function with current state", 2);

                    InnerDocAddTechnicalDataCompare(
                        uitk, grid, 0,
                        currentState: data.ItemOfChange.TechnicalData_CurrentState.__Info__,
                        compareToState: compareToState.ToArray(),
                        compareToName: compareToName.ToArray(),
                        filterForMin2Values: true);
                }

                // further logistics information
                if (true)
                {
                    InnerDocAddHeadline(uitk, grid, 0, "Remaining stock information", 2);

                    InnerDocAddText(uitk, grid, 0, 3, "Incotermcode:",
                        "" + ri.Incotermcode);

                    InnerDocAddText(uitk, grid, 0, 3, "Delivery time other region [days]:",
                        "" + ri.DeliveryTimeClassOtherRegion);

                    InnerDocAddText(uitk, grid, 0, 3, "Delivery time same region [days]:",
                        "" + ri.DeliveryTimeClassSameRegion);
                }

                // conformity declarations
                if (true)
                {
                    InnerDocAddHeadline(uitk, grid, 0, "Conformity declarations", 2);

                    InnerDocAddTechnicalDataCompare(
                        uitk, grid, 0,
                        currentState: ri.ConformityDeclarations.__Info__,
                        indicateCurrentStatePresent: true);
                }
            }
        }

        #endregion

        #region Event handling
        //=============

        private Action<AasxPluginEventReturnBase> _menuSubscribeForNextEventReturn = null;

        protected void PushUpdateEvent(AnyUiRenderMode mode = AnyUiRenderMode.All)
        {
            // bring it to the panel by redrawing the plugin
            _eventStack?.PushEvent(new AasxPluginEventReturnUpdateAnyUi()
            {
                // get the always currentplugin name
                PluginName = _plugin?.GetPluginName(),
                Session = _session,
                Mode = mode,
                UseInnerGrid = true
            });
        }

        public void HandleEventReturn(AasxPluginEventReturnBase evtReturn)
        {
            // demands from shelf
            if (_menuSubscribeForNextEventReturn != null)
            {
                // delete first
                var tempLambda = _menuSubscribeForNextEventReturn;
                _menuSubscribeForNextEventReturn = null;

                // execute
                tempLambda(evtReturn);

                // finish
                return;
            }
        }

        #endregion

        #region Update
        //=============

        public void Update(params object[] args)
        {
            // check args
            if (args == null || args.Length < 1
                || !(args[0] is AnyUiStackPanel newPanel))
                return;

            // ok, re-assign panel and re-display
            _panel = newPanel;
            _panel.Children.Clear();

            // the default: the full shelf
            RenderFullView(_panel, _uitk, _package, _submodel);
        }

        #endregion

        #region STUFF
        //=====================
        
        #endregion

        #region Callbacks
        //===============


        #endregion

        #region Utilities
        //===============


        #endregion
    }
}
