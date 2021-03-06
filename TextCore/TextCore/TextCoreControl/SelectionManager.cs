﻿using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    public delegate void SelectionChangeEventHandler();

    internal class SelectionManager
    {
        internal SelectionManager(HwndRenderTarget renderTarget, D2DFactory d2dFactory)
        {
            defaultBackgroundBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultBackgroundColor);
            defaultSelectionBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultSelectionColor);
            defaultSelectionOutlineBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultSelectionOutlineColor);
            highlightSelectionBrush = renderTarget.CreateSolidColorBrush(new ColorF(60 / 255f, 179 / 255f, 113 / 255f));
            highlightSelectionOutlineBrush = renderTarget.CreateSolidColorBrush(new ColorF(60 / 255f, 179 / 255f, 113 / 255f, 0.95f));
            whiteBrush = renderTarget.CreateSolidColorBrush(new ColorF(1.0f, 1.0f, 1.0f));
            defaultForegroundBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultForegroundColor);
            dimBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultSelectionDimColor);
            this.leftToRightSelection = true;
            this.d2dFactory = d2dFactory;
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            this.forceRedraw = false;
            this.backgroundHighlight = new BackgroundHighlight(renderTarget, d2dFactory);
        }

        internal void DrawSelection(
            int oldSelectionBegin,
            int oldSelectionEnd,
            List<VisualLine> visualLines,
            int firstVisibleLine,
            int lastVisibleLine,
            Document document,
            SizeF scrollOffset,
            RenderTarget renderTarget)
        {
            if (oldSelectionBegin == Document.BEFOREBEGIN_ORDINAL || oldSelectionBegin == Document.UNDEFINED_ORDINAL ||
                selectionBeginOrdinal == Document.BEFOREBEGIN_ORDINAL || selectionBeginOrdinal == Document.UNDEFINED_ORDINAL |
                oldSelectionEnd == Document.BEFOREBEGIN_ORDINAL || oldSelectionEnd == Document.UNDEFINED_ORDINAL || 
                selectionEndOrdinal == Document.BEFOREBEGIN_ORDINAL || selectionEndOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // Invalid range - bail out
                return;
            }

            if (!forceRedraw && oldSelectionBegin == oldSelectionEnd && selectionBeginOrdinal == selectionEndOrdinal)
            {
                // There was no selection before and there is no selection now - bail out.
                // There is no work to do here.
                return;
            }

            // Find range of affected lines.
            int firstLine = -1;
            int lastLine = 0;
            float minX = renderTarget.Size.Width;
            if (this.ShouldUseHighlightColors || forceRedraw)
            {
                // Full screen needs repaint
                firstLine = firstVisibleLine;
                lastLine = lastVisibleLine;
                minX = visualLines.Count == 0 ? 0 : visualLines[0].Position.X;
            }
            else
            {
                for (int k = firstVisibleLine; k <= lastVisibleLine; k++)
                {
                    VisualLine visualLine = (VisualLine)visualLines[k];
                    bool oldSelection = (visualLine.BeginOrdinal <= oldSelectionBegin && visualLine.NextOrdinal > oldSelectionBegin) ||
                        (visualLine.BeginOrdinal >= oldSelectionBegin && visualLine.BeginOrdinal < oldSelectionEnd);
                    oldSelection = oldSelection && oldSelectionBegin != oldSelectionEnd;

                    bool currentSelection = (visualLine.BeginOrdinal <= selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                        (visualLine.BeginOrdinal >= selectionBeginOrdinal && visualLine.BeginOrdinal < selectionEndOrdinal);
                    currentSelection = currentSelection && selectionBeginOrdinal != selectionEndOrdinal;

                    if (oldSelection || currentSelection)
                    {
                        minX = Math.Min(minX, visualLine.Position.X);
                        lastLine = k;
                        if (firstLine == -1) firstLine = k;
                    }
                }
            }
            
            // If there was atleast one affected line.
            if (firstLine != -1)
            {
                if (firstLine > 0) firstLine--;
                if (lastLine + 1 < visualLines.Count) lastLine++;

                System.Collections.Generic.List<Geometry> geometryList = new System.Collections.Generic.List<Geometry>();

                // Build selection shape
                for (int k = 0; k < visualLines.Count; k++)
                {
                    VisualLine visualLine = (VisualLine)visualLines[k];

                    bool currentSelection = (visualLine.BeginOrdinal <= selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                        (visualLine.BeginOrdinal >= selectionBeginOrdinal && visualLine.BeginOrdinal <= selectionEndOrdinal);
                    currentSelection = currentSelection && selectionBeginOrdinal != selectionEndOrdinal;

                    if (currentSelection)
                    {
                        List<RectF> selectRectangles =  visualLine.GetRangeRectangles(document, selectionBeginOrdinal, selectionEndOrdinal);
                        for (int i = 0; i < selectRectangles.Count; i++)
                        {
                            if (selectRectangles[i].Width > 0.0f)
                            {
                                RoundedRect roundedRect;
                                if (ShouldUseHighlightColors)
                                    roundedRect = new RoundedRect(selectRectangles[i], 2.0f, 2.0f);
                                else 
                                    roundedRect = new RoundedRect(selectRectangles[i], 0.0f, 0.0f);
                                geometryList.Add(d2dFactory.CreateRoundedRectangleGeometry(roundedRect));
                            }
                        }
                    }
                }

                RectF bounds = new RectF(
                                    minX,
                                    ((VisualLine)visualLines[firstLine]).Position.Y,
                                    renderTarget.Size.Width,
                                    ((VisualLine)visualLines[lastLine]).Position.Y + ((VisualLine)visualLines[lastLine]).Height
                               );
                // Expand bounds a bit, to wipe last and first lines correctly.
                if (firstLine == 0)
                {
                    bounds.Top -= 3.0f;
                }
                if (lastLine == visualLines.Count -1)
                {
                    bounds.Bottom += 3.0f;
                }
                bounds.Left -= 3.0f;

                // Begin Render
                renderTarget.PushAxisAlignedClip(bounds, AntiAliasMode.PerPrimitive);
                GeometryGroup selectionGeometry = null;
                if (geometryList.Count != 0)
                {
                    selectionGeometry = this.d2dFactory.CreateGeometryGroup(FillMode.Winding, geometryList);
                    // Wipe out background outside selection background for the selected lines.
                    renderTarget.FillRectangle(bounds, defaultBackgroundBrush);
                }
                else
                {
                    // Wipe out background for affected lines
                    renderTarget.FillRectangle(bounds, defaultBackgroundBrush);
                }
                this.backgroundHighlight.Draw(visualLines, firstLine, lastLine, document, scrollOffset, renderTarget);
                renderTarget.PopAxisAlignedClip();

                // Draw content layer black lines.
                for (int j = firstLine; j <= lastLine; j++)
                {
                    VisualLine blackLine = ((VisualLine)visualLines[j]);
                    // If line is completely selected then simply skip the line.
                    if (blackLine.BeginOrdinal > this.selectionBeginOrdinal && blackLine.NextOrdinal < this.selectionEndOrdinal)
                        continue;

                    blackLine.Draw(defaultForegroundBrush, renderTarget);
                }

                // Draw dimness if there is something selected now
                if (this.ShouldUseHighlightColors && selectionBeginOrdinal != selectionEndOrdinal)
                {
                    renderTarget.FillRectangle(new RectF(scrollOffset.Width + minX, scrollOffset.Height, scrollOffset.Width + renderTarget.Size.Width, scrollOffset.Height + renderTarget.Size.Height), dimBrush);
                }

                if (selectionGeometry != null)
                {
                    if (this.ShouldUseHighlightColors)
                        renderTarget.DrawGeometry(selectionGeometry, highlightSelectionOutlineBrush, 3.0f);
                    else
                        renderTarget.DrawGeometry(selectionGeometry, defaultSelectionOutlineBrush, 1.0f);

                    // Clip to selection shape.
                    Layer layer = renderTarget.CreateLayer(new SizeF(bounds.Width, bounds.Height));
                    LayerParameters layerParameters = new LayerParameters(bounds,
                        selectionGeometry, 
                        AntiAliasMode.Aliased,
                        Matrix3x2F.Identity,
                        1.0f, 
                        null,
                        LayerOptions.InitializeForClearType
                    );

                    renderTarget.PushLayer(layerParameters, layer);

                    if (this.ShouldUseHighlightColors)
                        renderTarget.FillRectangle(bounds, highlightSelectionBrush);
                    else
                        renderTarget.FillRectangle(bounds, defaultSelectionBrush);

                    // Draw content layer - white lines.
                    for (int j = firstLine; j <= lastLine; j++)
                    {
                        VisualLine line = ((VisualLine)visualLines[j]);
                        // If line is completely unselected then simply skip the line.
                        if (line.BeginOrdinal > this.selectionEndOrdinal || line.NextOrdinal < this.selectionBeginOrdinal)
                            continue;

                        line.DrawWithoutEffects(whiteBrush, renderTarget);
                    }

                    renderTarget.PopLayer();
                }

            }
        }

        internal void ResetSelection(int beginOrdinal, List<VisualLine> visualLines, Document document, SizeF scrollOffset, RenderTarget renderTarget)
        {
            int oldSelectionBegin = this.selectionBeginOrdinal;
            int oldSelectionEnd = this.selectionEndOrdinal;

            this.selectionBeginOrdinal = beginOrdinal;
            this.selectionEndOrdinal = beginOrdinal;

            // Draw selection bails when there is no actual selection area change.            
            this.DrawSelection(oldSelectionBegin, oldSelectionEnd, visualLines, 0, visualLines.Count - 1, document, scrollOffset, renderTarget);

            if(this.SelectionChange != null)
                this.SelectionChange();
        }

        /// <summary>
        ///     Returns the ordinal at which selection starts, this document ordinal is selected.
        /// </summary>
        /// <returns></returns>
        internal int GetSelectionBeginOrdinal() { return this.selectionBeginOrdinal; }

        /// <summary>
        ///     Returns the ordinal just after selection end, this document ordinal is not selected 
        ///     but the previous one is selected.
        /// </summary>
        /// <returns></returns>
        internal int GetSelectionEndOrdinal() { return this.selectionEndOrdinal; }

        internal void ExpandSelection(int includeOrdinal, List<VisualLine> visualLines, Document document, SizeF scrollOffset, RenderTarget renderTarget)
        {
            int oldSelectionBeginOrdinal = this.selectionBeginOrdinal;
            int oldSelectionEndOrdinal = this.selectionEndOrdinal;

            if (this.leftToRightSelection)
            {
                if (includeOrdinal < this.selectionBeginOrdinal)
                {
                    // Switching from left to right to right to left.
                    this.leftToRightSelection = false;
                    this.selectionEndOrdinal = this.selectionBeginOrdinal;
                    this.selectionBeginOrdinal = includeOrdinal;
                }
                else
                {
                    // Either shorten or lengthen selection.
                    this.selectionEndOrdinal = includeOrdinal;
                }
            }
            else
            {
                if (includeOrdinal > this.selectionEndOrdinal)
                {
                    // Switching from right to left to left to right.
                    this.leftToRightSelection = true;
                    this.selectionBeginOrdinal = this.selectionEndOrdinal;
                    this.selectionEndOrdinal = includeOrdinal;
                }
                else
                {
                    // Either shorten or lengthen selection.
                    this.selectionBeginOrdinal = includeOrdinal;
                }
            }

            this.DrawSelection(oldSelectionBeginOrdinal, oldSelectionEndOrdinal, visualLines, 0, visualLines.Count - 1, document, scrollOffset, renderTarget);
            
            if (this.SelectionChange != null)
                this.SelectionChange();
        }

        internal void NotifyOfOrdinalShift(int beginOrdinal, int shift)
        {
            bool wasInSelection = false;
            if (this.shouldUseHighlightColors && shift < 0)
            {
                wasInSelection = this.selectionBeginOrdinal != this.selectionEndOrdinal;
            }

            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.selectionBeginOrdinal);
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.selectionEndOrdinal);

            if (wasInSelection && this.selectionBeginOrdinal == this.selectionEndOrdinal)
            {
                // No longer in selection. Because we are drawing dimness we need to force the next paint.
                forceRedraw = true;
            }

            this.backgroundHighlight.NotifyOfOrdinalShift(beginOrdinal, shift);
        }

        internal bool ShouldUseHighlightColors {
            get { return this.shouldUseHighlightColors;  }
            set 
            {
                if (this.shouldUseHighlightColors == true && value == false)
                    this.forceRedraw = true;
                this.shouldUseHighlightColors = value;
            }
        }

        internal BackgroundHighlight BackgroundHighlight
        {
            get { return this.backgroundHighlight; }
        }
        
        internal event SelectionChangeEventHandler SelectionChange;

        int selectionBeginOrdinal;
        int selectionEndOrdinal;
        bool leftToRightSelection;
        internal bool shouldUseHighlightColors;
        private bool forceRedraw;

        SolidColorBrush defaultBackgroundBrush;
        SolidColorBrush defaultSelectionBrush;
        SolidColorBrush defaultSelectionOutlineBrush;
        SolidColorBrush whiteBrush;
        SolidColorBrush defaultForegroundBrush;
        SolidColorBrush dimBrush;
        SolidColorBrush highlightSelectionBrush;
        SolidColorBrush highlightSelectionOutlineBrush;
        D2DFactory d2dFactory;
        DWriteFactory dwriteFactory;
        BackgroundHighlight backgroundHighlight;
    }
}
