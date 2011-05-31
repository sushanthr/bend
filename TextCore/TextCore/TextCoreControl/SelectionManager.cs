using System;
using System.Collections;

using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    public class SelectionManager
    {
        public SelectionManager(HwndRenderTarget renderTarget, D2DFactory d2dFactory)
        {
            defaultForegroundBrush = renderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 1));
            defaultBackgroundBrush = renderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1));
            defaultSelectionBrush = renderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 1.0f));
            defaultSelectionOutlineBrush = renderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 0.5f));
            this.leftToRightSelection = true;
            this.d2dFactory = d2dFactory;
        }

        public void DrawSelection(
            int oldSelectionBegin,
            int oldSelectionEnd,
            ArrayList visualLines,
            Document document, 
            RenderTarget renderTarget)
        {
            
            // Wipe out background for lines going out of selection.
            for (int k = 0; k < visualLines.Count; k++)
            {
                VisualLine visualLine = (VisualLine)visualLines[k];
                bool oldSelection = (visualLine.BeginOrdinal < oldSelectionBegin && visualLine.NextOrdinal > oldSelectionBegin) ||
                    (visualLine.BeginOrdinal > oldSelectionBegin && visualLine.BeginOrdinal < oldSelectionEnd);

                bool currentSelection = (visualLine.BeginOrdinal < selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                    (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.BeginOrdinal < selectionEndOrdinal);

                Point2F position = visualLine.Position;
                if (oldSelection && !currentSelection)
                {
                    renderTarget.FillRectangle(
                                        new RectF(position.X - 2, position.Y - 2, position.X + visualLine.Width + 2, position.Y + visualLine.Height + 2),
                                        renderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1)));
                }
            }
               
            if (selectionBeginOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionEndOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionBeginOrdinal != selectionEndOrdinal)
            {
                System.Collections.Generic.List<Geometry> geometryList = new System.Collections.Generic.List<Geometry>();

                // Find selection begin
                bool finishedDrawing = false;
                int i = 0, firstLine = 0, lastLine = visualLines.Count - 1;
                for (; i < visualLines.Count; i++)
                {
                    VisualLine visualLine = (VisualLine)visualLines[i];
                    if (visualLine.BeginOrdinal < selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal)
                    {
                        Point2F position = visualLine.Position;

                        float xBeginPos = visualLine.CharPosition(document, selectionBeginOrdinal);
                        float xEndPos;
                        if (visualLine.NextOrdinal > selectionEndOrdinal)
                        {
                            finishedDrawing = true;
                            xEndPos = visualLine.CharPosition(document, selectionEndOrdinal);
                            firstLine = i;
                            lastLine = i;
                        }
                        else
                        {
                            // Selection continues to next line.
                            xEndPos = position.X + visualLine.Width;
                            firstLine = i;
                        }

                        RoundedRect roundedRect = new RoundedRect(new RectF(
                            position.X + xBeginPos - 1,
                            position.Y - 1,
                            xEndPos + 1,
                            position.Y + visualLine.Height + 1),
                            /*radiusX*/ 2, /*radiusY*/ 2);
                        geometryList.Add(d2dFactory.CreateRoundedRectangleGeometry(roundedRect));

                        break;
                    }
                }

                if (!finishedDrawing)
                {
                    i++;

                    // Highlight lines in between
                    for (; i < visualLines.Count; i++)
                    {
                        VisualLine visualLine = (VisualLine)visualLines[i];
                        if (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.NextOrdinal < selectionEndOrdinal)
                        {
                            Point2F position = visualLine.Position;
                            RoundedRect roundedRect = new RoundedRect(new RectF(
                                position.X - 1,
                                position.Y - 1,
                                position.X + visualLine.Width + 1,
                                position.Y + visualLine.Height + 1), 2.0f, 2.0f);
                            geometryList.Add(d2dFactory.CreateRoundedRectangleGeometry(roundedRect));
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Highlight the last line
                    if (i < visualLines.Count)
                    {
                        VisualLine visualLine = (VisualLine)visualLines[i];
                        if (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.NextOrdinal >= selectionEndOrdinal)
                        {
                            Point2F position = visualLine.Position;
                            float xEndPos = visualLine.CharPosition(document, selectionEndOrdinal);

                            RoundedRect roundedRect = new RoundedRect(new RectF(
                                 position.X - 1,
                                 position.Y - 1,
                                 xEndPos + 1,
                                 position.Y + visualLine.Height + 1),
                                /*radiusX*/ 2, /*radiusY*/ 2);
                            geometryList.Add(d2dFactory.CreateRoundedRectangleGeometry(roundedRect));
                            lastLine = i;
                        }
                    }
                }

                if (geometryList.Count != 0)
                {
                    GeometryGroup selectionGeometry = this.d2dFactory.CreateGeometryGroup(FillMode.Winding, geometryList);
   
                    // Wipe out background outside selection background for the selected lines.
                    RectF bounds = new RectF(
                        0,
                        ((VisualLine)visualLines[firstLine]).Position.Y - 2,
                        renderTarget.Size.Width,
                        ((VisualLine)visualLines[lastLine]).Position.Y + ((VisualLine)visualLines[lastLine]).Height + 2);

                    renderTarget.PushAxisAlignedClip(bounds, AntiAliasMode.PerPrimitive);
                    renderTarget.DrawGeometry(selectionGeometry, defaultBackgroundBrush, float.MaxValue);
                    renderTarget.PopAxisAlignedClip();
                       
                    renderTarget.DrawGeometry(selectionGeometry, defaultSelectionOutlineBrush, 1.0f);
                    renderTarget.FillGeometry(selectionGeometry, defaultSelectionBrush);

                    // Draw content layer.
                    for (int j = firstLine; j <= lastLine; j++)
                    {
                        ((VisualLine)visualLines[j]).DrawInverted(renderTarget,
                            document, 
                            selectionBeginOrdinal,
                            selectionEndOrdinal, 
                            defaultForegroundBrush,
                            defaultBackgroundBrush
                            );
                    }
                }
            }

            // Redraw content for lines going out of selection.
            for (int k = 0; k < visualLines.Count; k++)
            {
                VisualLine visualLine = (VisualLine)visualLines[k];
                bool oldSelection = (visualLine.BeginOrdinal < oldSelectionBegin && visualLine.NextOrdinal > oldSelectionBegin) ||
                    (visualLine.BeginOrdinal > oldSelectionBegin && visualLine.BeginOrdinal < oldSelectionEnd);

                bool currentSelection = (visualLine.BeginOrdinal < selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                    (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.BeginOrdinal < selectionEndOrdinal);

                Point2F position = visualLine.Position;
                if (oldSelection && !currentSelection)
                {
                    visualLine.Draw(renderTarget, defaultForegroundBrush);
                }
            }
        }

        public void ResetSelection(int beginOrdinal, ArrayList visualLines, Document document, RenderTarget renderTarget)
        {
            int oldSelectionBegin = this.selectionBeginOrdinal;
            int oldSelectionEnd = this.selectionEndOrdinal;

            this.selectionBeginOrdinal = beginOrdinal;
            this.selectionEndOrdinal = beginOrdinal;

            this.DrawSelection(oldSelectionBegin, oldSelectionEnd, visualLines, document, renderTarget);
        }

        public int GetSelectionBeginOrdinal() { return this.selectionBeginOrdinal; }

        public void ExpandSelection(int includeOrdinal, ArrayList visualLines, Document document, RenderTarget renderTarget)
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

            this.DrawSelection(oldSelectionBeginOrdinal, oldSelectionEndOrdinal, visualLines, document, renderTarget);
        }

        public int GetSelectionEndOrdinal() { return this.selectionEndOrdinal; }

        int selectionBeginOrdinal;
        int selectionEndOrdinal;
        bool leftToRightSelection;
        SolidColorBrush defaultForegroundBrush;
        SolidColorBrush defaultBackgroundBrush;
        SolidColorBrush defaultSelectionBrush;
        SolidColorBrush defaultSelectionOutlineBrush;
        D2DFactory d2dFactory;
    }
}
