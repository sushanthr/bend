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
            defaultSelectionBrush = renderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 1.0f));
            this.renderTarget = renderTarget;
            this.leftToRightSelection = true;
            this.d2dFactory = d2dFactory;
        }

        /// <summary>
        ///     Redraws a line going out of selection
        /// </summary>
        /// <param name="visualLines"></param>
        /// <param name="newSelectionBeginOrdinal"></param>
        /// <param name="newSelectionEndOrdinal"></param>
        private void DrawClearSelection(ArrayList visualLines, int newSelectionBeginOrdinal, int newSelectionEndOrdinal)
        {
            if (selectionBeginOrdinal != Document.UNDEFINED_ORDINAL &&
               selectionEndOrdinal != Document.UNDEFINED_ORDINAL &&
               selectionBeginOrdinal != selectionEndOrdinal)
            {
                lock (renderTarget)
                {
                    renderTarget.BeginDraw();

                    // Wipe all selected lines.
                    for (int i = 0; i < visualLines.Count; i++)
                    {
                        VisualLine visualLine = (VisualLine)visualLines[i];
                        bool currentSelection = (visualLine.BeginOrdinal < selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                            (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.BeginOrdinal < selectionEndOrdinal);
                        bool futureSelection = (visualLine.BeginOrdinal < newSelectionBeginOrdinal && visualLine.NextOrdinal > newSelectionBeginOrdinal) ||
                            (visualLine.BeginOrdinal > newSelectionBeginOrdinal && visualLine.BeginOrdinal < newSelectionEndOrdinal);

                        Point2F position = visualLine.Position;
                        if (currentSelection && !futureSelection)
                        {
                            renderTarget.FillRectangle(
                                             new RectF(position.X - 2, position.Y - 2, position.X + visualLine.Width + 2, position.Y + visualLine.Height + 2),
                                             renderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1)));
                            visualLine.Draw(renderTarget, defaultForegroundBrush);
                        }
                    }

                    renderTarget.EndDraw();
                }
            }
        }

        private void BuildSelectionGeometry(ArrayList visualLines, Document document)
        {
            if (selectionBeginOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionEndOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionBeginOrdinal != selectionEndOrdinal)
            {
                lock (renderTarget)
                {
                    renderTarget.BeginDraw();

                    // Find selection begin
                    bool finishedDrawing = false;
                    int i = 0;
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
                            }
                            else
                            {
                                // Selection continues to next line.
                                xEndPos = position.X + visualLine.Width;
                            }

                            // wipe
                            renderTarget.FillRectangle(
                                         new RectF(position.X - 2, position.Y - 2, position.X + visualLine.Width + 2, position.Y + visualLine.Height + 2),
                                         renderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1)));

                            RoundedRect roundedRect = new RoundedRect(new RectF(
                                position.X + xBeginPos - 1,
                                position.Y - 1,
                                xEndPos + 1,
                                position.Y + visualLine.Height + 1),
                                /*radiusX*/ 2, /*radiusY*/ 2);
                            renderTarget.FillRoundedRectangle(roundedRect, defaultSelectionBrush);

                            if (!finishedDrawing)
                            {
                                RectF squareEdge = new RectF(
                                    position.X + xBeginPos + 4 - 1,
                                    position.Y - 1,
                                    xEndPos + 1,
                                    position.Y + visualLine.Height + 1);
                                renderTarget.FillRectangle(squareEdge, defaultSelectionBrush);
                            }

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
                                RectF squareEdge = new RectF(
                                    position.X - 1,
                                    position.Y - 1,
                                    position.X + visualLine.Width + 1,
                                    position.Y + visualLine.Height + 1);
                                renderTarget.FillRectangle(squareEdge, defaultSelectionBrush);
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

                                // wipe
                                renderTarget.FillRectangle(
                                             new RectF(xEndPos, position.Y - 2, renderTarget.Size.Width, position.Y + visualLine.Height + 2),
                                             renderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1)));

                                RoundedRect roundedRect = new RoundedRect(new RectF(
                                     position.X - 1,
                                     position.Y - 1,
                                     xEndPos + 1,
                                     position.Y + visualLine.Height + 1),
                                    /*radiusX*/ 2, /*radiusY*/ 2);
                                renderTarget.FillRoundedRectangle(roundedRect, defaultSelectionBrush);


                                RectF squareEdge = new RectF(
                                    position.X - 1,
                                    position.Y - 1,
                                    xEndPos - 4 + 1,
                                    position.Y + visualLine.Height + 1);
                                renderTarget.FillRectangle(squareEdge, defaultSelectionBrush);
                            }
                        }
                    }

                    // redraw lines.
                    for (int j = 0; j < visualLines.Count; j++)
                    {
                        VisualLine visualLine = (VisualLine)visualLines[j];
                        if ((visualLine.BeginOrdinal < selectionBeginOrdinal && visualLine.NextOrdinal > selectionBeginOrdinal) ||
                            (visualLine.BeginOrdinal > selectionBeginOrdinal && visualLine.BeginOrdinal < selectionEndOrdinal))
                        {
                            visualLine.Draw(renderTarget, defaultForegroundBrush);
                        }
                    }

                    renderTarget.EndDraw();
                }
            }
        }
        
        public void DrawSelection(ArrayList visualLines, Document document)
        {
            if (selectionBeginOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionEndOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionBeginOrdinal != selectionEndOrdinal)
            {
                PathGeometry selectionGeometry = this.d2dFactory.CreatePathGeometry();
                
                GeometrySink geometrySink = selectionGeometry.Open();      
                Geometry tempGeometry = null;

                // Find selection begin
                bool finishedDrawing = false;
                int i = 0;
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
                        }
                        else
                        {
                            // Selection continues to next line.
                            xEndPos = position.X + visualLine.Width;
                        }

                        RoundedRect roundedRect = new RoundedRect(new RectF(
                            position.X + xBeginPos - 1,
                            position.Y - 1,
                            xEndPos + 1,
                            position.Y + visualLine.Height + 1),
                            /*radiusX*/ 2, /*radiusY*/ 2);
                        if (tempGeometry == null)
                        {
                            tempGeometry = d2dFactory.CreateRoundedRectangleGeometry(roundedRect);
                        }
                        tempGeometry.CombineWithGeometry(d2dFactory.CreateRoundedRectangleGeometry(roundedRect), CombineMode.Union, geometrySink);
                       
                        if (!finishedDrawing)
                        {
                            RectF squareEdge = new RectF(
                                position.X + xBeginPos + 4 - 1,
                                position.Y - 1,
                                xEndPos + 1,
                                position.Y + visualLine.Height + 1);
            //                tempGeometry.CombineWithGeometry(d2dFactory.CreateRectangleGeometry(squareEdge), CombineMode.Union, geometrySink);
                        }

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
                            RectF squareEdge = new RectF(
                                position.X - 1,
                                position.Y - 1,
                                position.X + visualLine.Width + 1,
                                position.Y + visualLine.Height + 1);
                            if (tempGeometry == null)
                            {
                                tempGeometry = d2dFactory.CreateRectangleGeometry(squareEdge);
                            }
                //            tempGeometry.CombineWithGeometry(d2dFactory.CreateRectangleGeometry(squareEdge), CombineMode.Union, geometrySink);
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
                            if (tempGeometry == null)
                            {
                                tempGeometry = d2dFactory.CreateRoundedRectangleGeometry(roundedRect);
                            }
                            //tempGeometry.CombineWithGeometry(d2dFactory.CreateRoundedRectangleGeometry(roundedRect), CombineMode.Union, geometrySink);
      
                            RectF squareEdge = new RectF(
                                position.X - 1,
                                position.Y - 1,
                                xEndPos - 4 + 1,
                                position.Y + visualLine.Height + 1);
                            //tempGeometry.CombineWithGeometry(d2dFactory.CreateRectangleGeometry(squareEdge), CombineMode.Union, geometrySink);
                        }
                    }
                }

                geometrySink.Close();

                lock (renderTarget)
                {
                    renderTarget.BeginDraw();
                    renderTarget.DrawGeometry(selectionGeometry, defaultSelectionBrush, 2.0f);
                    renderTarget.EndDraw();
                }

                geometrySink.Dispose();
            }
        }

        public void ResetSelection(int beginOrdinal, ArrayList visualLines, Document document)
        {
            this.DrawClearSelection(visualLines, Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL);
            this.selectionBeginOrdinal = beginOrdinal;
            this.selectionEndOrdinal = beginOrdinal;
            this.DrawSelection(visualLines, document);
        }

        public int GetSelectionBeginOrdinal() { return this.selectionBeginOrdinal; }

        public void ExpandSelection(int includeOrdinal, ArrayList visualLines, Document document)
        {
            if (this.leftToRightSelection)
            {
                if (includeOrdinal < this.selectionBeginOrdinal)
                {
                    // Switching from left to right to right to left.
                    this.leftToRightSelection = false;
                    this.DrawClearSelection(visualLines, includeOrdinal, selectionBeginOrdinal);
                    this.selectionEndOrdinal = this.selectionBeginOrdinal;
                    this.selectionBeginOrdinal = includeOrdinal;
                }
                else
                {
                    // Either shorten or lengthen selection.
                    this.DrawClearSelection(visualLines, this.selectionBeginOrdinal, includeOrdinal);
                    this.selectionEndOrdinal = includeOrdinal;
                }
            }
            else
            {
                if (includeOrdinal > this.selectionEndOrdinal)
                {
                    // Switching from right to left to left to right.
                    this.leftToRightSelection = true;
                    this.DrawClearSelection(visualLines, this.selectionEndOrdinal, includeOrdinal);
                    this.selectionBeginOrdinal = this.selectionEndOrdinal;
                    this.selectionEndOrdinal = includeOrdinal;
                }
                else
                {
                    // Either shorten or lengthen selection.
                    this.DrawClearSelection(visualLines, includeOrdinal, this.selectionEndOrdinal);
                    this.selectionBeginOrdinal = includeOrdinal;
                }
            }

            this.DrawSelection(visualLines, document);
        }

        public int GetSelectionEndOrdinal() { return this.selectionEndOrdinal; }

        int selectionBeginOrdinal;
        int selectionEndOrdinal;
        bool leftToRightSelection;
        HwndRenderTarget renderTarget;
        SolidColorBrush defaultForegroundBrush;
        SolidColorBrush defaultSelectionBrush;
        D2DFactory d2dFactory;
    }
}
