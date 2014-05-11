using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Drawing;

namespace TextCoreControl
{
    public class FlightRecorder
    {
        #region Flight Event
        [System.Xml.Serialization.XmlInclude(typeof(TextEdtiorPreviewKeyDownFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(LoadFileFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(SaveFileFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(ReplaceTextFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(ReplaceAllTextFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(ReplaceWithRegexAtOrdinalFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(SelectFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(CancelSelectFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(SetBackgroundHighlightFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(ResetBackgroundHighlightFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(VScrollBarFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(HScrollBarFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(KeyHandlerFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(DisplayManagerPreviewKeyDownFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(MouseHandlerFlightEvent))]
        [System.Xml.Serialization.XmlInclude(typeof(SizeChangeFlightEvent))]
        public class FlightEvent
        {
            public virtual void Playback(TextEditor textEditor)
            {

            }
        }

        #region Display Manager Flight Events
        public class SizeChangeFlightEvent : FlightEvent
        {
            public SizeChangeFlightEvent()
            {
                this.actualHeight = 0;
                this.actualWidth = 0;
            }

            public SizeChangeFlightEvent(double actualWidth, double actualHeight)
            {
                this.actualHeight = actualHeight;
                this.actualWidth = actualWidth;
            }

            public override void Playback(TextEditor textEditor)
            {
                System.Windows.Application.Current.MainWindow.Height += (actualHeight - textEditor.RenderHost.ActualHeight);
                System.Windows.Application.Current.MainWindow.Width += (actualWidth - textEditor.RenderHost.ActualWidth);                
            }

            public double actualHeight;
            public double actualWidth;
        }

        public class MouseHandlerFlightEvent : FlightEvent
        {
            public MouseHandlerFlightEvent()
            {

            }

            public MouseHandlerFlightEvent(int x, int y, int type, int flags)
            {
                this.x = x;
                this.y = y;
                this.type = type;
                this.flags = flags;
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.DisplayManager.MouseHandler(x, y, type, flags);
            }

            public int x;
            public int y;
            public int type;
            public int flags;
        }

        public class KeyHandlerFlightEvent : FlightEvent
        {
            public KeyHandlerFlightEvent()
            {

            }

            public KeyHandlerFlightEvent(int wparam, int lparam)
            {
                this.wparam = wparam;
                this.lparam = lparam;
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.DisplayManager.KeyHandler(wparam, lparam);
            }

            public int wparam;
            public int lparam;
        }

        public class DisplayManagerPreviewKeyDownFlightEvent : FlightEvent
        {
            public DisplayManagerPreviewKeyDownFlightEvent()
            {

            }

            public DisplayManagerPreviewKeyDownFlightEvent(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifier)
            {
                this.key = key;
                this.modifier = modifier;
            }

            public override void Playback(TextEditor textEditor)
            {
                bool handled;
                textEditor.DisplayManager.RenderHost_PreviewKeyDown(key, modifier, out handled);
            }

            public System.Windows.Input.Key key;
            public System.Windows.Input.ModifierKeys modifier;
        }

        public class VScrollBarFlightEvent : FlightEvent
        {
            public VScrollBarFlightEvent()
            {
            }

            public VScrollBarFlightEvent(double newValue)
            {
                this.newValue = newValue;
            }

            public override void Playback(TextEditor textEditor)
            {
                System.Windows.Controls.Primitives.ScrollEventArgs e = new System.Windows.Controls.Primitives.ScrollEventArgs(System.Windows.Controls.Primitives.ScrollEventType.ThumbTrack, newValue);
                textEditor.DisplayManager.vScrollBar_Scroll(null, e);
            }

            public double newValue;
        }

        public class HScrollBarFlightEvent : FlightEvent
        {
            public HScrollBarFlightEvent()
            {
            }

            public HScrollBarFlightEvent(double newValue)
            {
                this.newValue = newValue;
            }

            public override void Playback(TextEditor textEditor)
            {
                System.Windows.Controls.Primitives.ScrollEventArgs e = new System.Windows.Controls.Primitives.ScrollEventArgs(System.Windows.Controls.Primitives.ScrollEventType.ThumbTrack, newValue);
                textEditor.DisplayManager.hScrollBar_Scroll(null, e);
            }

            public double newValue;
        }
        #endregion

        #region TextEditor Flight Events
        public class TextEdtiorPreviewKeyDownFlightEvent : FlightEvent
        {
            public TextEdtiorPreviewKeyDownFlightEvent()
            {

            }

            public TextEdtiorPreviewKeyDownFlightEvent(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifier)
            {
                this.key = key;
                this.modifier = modifier;
            }

            public override void Playback(TextEditor textEditor)
            {
                bool handled;
                textEditor.TextControl_PreviewKeyDown(key, modifier, out handled);
            }

            public System.Windows.Input.Key key;
            public System.Windows.Input.ModifierKeys modifier;
        }

        public class LoadFileFlightEvent : FlightEvent
        {
            public LoadFileFlightEvent()
            {

            }

            public LoadFileFlightEvent(string fullFilePath)
            {
                this.fullFilePath = fullFilePath;
            }

            public override void Playback(TextEditor textEditor)
            {
                if (!File.Exists(fullFilePath))
                {
                    fullFilePath = Path.GetFileName(fullFilePath);
                    fullFilePath = Path.GetDirectoryName(textEditor.PlaybackFlightRecordFullPath) + "\\" + fullFilePath;
                }
                textEditor.LoadFile(fullFilePath);
            }

            public string fullFilePath;
        }

        public class SaveFileFlightEvent : FlightEvent
        {
            public SaveFileFlightEvent()
            {

            }

            public SaveFileFlightEvent(string fullFilePath)
            {
                this.fullFilePath = fullFilePath;
            }

            public override void Playback(TextEditor textEditor)
            {
                string fileName = Path.GetFileName(fullFilePath);
                string altDirectory = Path.GetDirectoryName(textEditor.PlaybackFlightRecordFullPath);
                textEditor.SaveFile(altDirectory + "\\" + fileName);
            }

            public string fullFilePath;
        }

        public class ReplaceTextFlightEvent : FlightEvent
        {
            public ReplaceTextFlightEvent() { }

            public ReplaceTextFlightEvent(int index, int length, string newText)
            {
                this.index = index;
                this.length = length;
                this.newText = newText;
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.ReplaceText(index, length, newText);
            }

            public int index;
            public int length;
            public string newText;
        }

        public class ReplaceAllTextFlightEvent : FlightEvent
        {
            public ReplaceAllTextFlightEvent() { }

            public ReplaceAllTextFlightEvent(string findText, string replaceText, bool matchCase, bool useRegEx, bool inBackgroundHighlightRange)
            {
                this.findText = findText;
                this.replaceText = replaceText;
                this.matchCase = matchCase;
                this.useRegEx = useRegEx;
                this.inBackgroundHighlightRange = inBackgroundHighlightRange;
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.ReplaceAllText(findText, replaceText, matchCase, useRegEx, inBackgroundHighlightRange);
            }

            public string findText;
            public string replaceText;
            public bool matchCase;
            public bool useRegEx;
            public bool inBackgroundHighlightRange;
        }

        public class ReplaceWithRegexAtOrdinalFlightEvent : FlightEvent
        {
            public ReplaceWithRegexAtOrdinalFlightEvent() { }

            public ReplaceWithRegexAtOrdinalFlightEvent(string findText, string replaceText, bool matchCase, int beginOrdinal)
            {
                this.findText = findText;
                this.replaceText = replaceText;
                this.matchCase = matchCase;
                this.beginOrdinal = beginOrdinal;
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.ReplaceWithRegexAtOrdinal(findText, replaceText, matchCase, beginOrdinal);
            }

            public string findText;
            public string replaceText;
            public bool matchCase;
            public int beginOrdinal;
        }

        public class SelectFlightEvent : FlightEvent
        {
            public SelectFlightEvent() { }
            public SelectFlightEvent(int beginOrdinal, uint length) 
            { 
                this.beginOrdinal = beginOrdinal; 
                this.length = length; 
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.Select(beginOrdinal, length);
            }

            public int beginOrdinal;
            public uint length;
        }

        public class CancelSelectFlightEvent : FlightEvent
        {
            public CancelSelectFlightEvent() { }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.CancelSelect();
            }
        }

        public class SetBackgroundHighlightFlightEvent : FlightEvent
        {
            public SetBackgroundHighlightFlightEvent() { }
            public SetBackgroundHighlightFlightEvent(int beginOrdinal, int endOrdinal) 
            { 
                this.beginOrdinal = beginOrdinal; 
                this.endOrdinal = endOrdinal; 
            }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.SetBackgroundHighlight(beginOrdinal, endOrdinal);
            }

            public int beginOrdinal;
            public int endOrdinal;
        }

        public class ResetBackgroundHighlightFlightEvent : FlightEvent
        {
            public ResetBackgroundHighlightFlightEvent() { }

            public override void Playback(TextEditor textEditor)
            {
                textEditor.ResetBackgroundHighlight();
            }
        }
        #endregion

        #endregion

        internal FlightRecorder(TextEditor textEditor)
        {
            this.textEditor = textEditor;
            this.flightEventCollection = new List<FlightEvent>();
            this.snapshotCount = 0;
            this.flightId = "";
        }

        internal void AddFlightEvent(FlightEvent flightEvent)
        {
            if (this.isRecording)
            { 
                this.flightEventCollection.Add(flightEvent);
            }
        }

        internal void StartRecording()
        {
            this.isRecording = true;
            this.SetFlightIdFromNextAvailableIdInDirectory();
        }

        private void SetFlightIdFromNextAvailableIdInDirectory()
        {
            String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            string attemptPath = filePath + "FlightRecord" + this.flightId + ".xml";
            uint fileIdNumber = 1;
            while (File.Exists(attemptPath))
            {
                this.flightId = fileIdNumber.ToString();
                attemptPath = filePath + "FlightRecord" + this.flightId + ".xml";
                fileIdNumber++;
            }
        }

        internal void StopRecording()
        {
            this.isRecording = false;
        }

        internal bool IsRecording { get { return this.isRecording; } }

        private void WriteEventCollectionToFile()
        {
            if (flightEventCollection.Count != 0)
            { 
                // There is atleast one flight event.
                XmlSerializer serializer = new XmlSerializer(flightEventCollection.GetType());
                String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
                TextWriter writer = new StreamWriter(filePath + "FlightRecord" + flightId  + ".xml");
                serializer.Serialize(writer, flightEventCollection);
                writer.Close();
                flightEventCollection = new List<FlightEvent>();
            }
        }
        
        internal void TakeSnapshot()
        {
            BitmapSource bitmapSource = this.textEditor.DisplayManager.Rasterize();
            string outputDirectory;
            if (this.isPlayingBack)
            {
                outputDirectory = Path.GetDirectoryName(textEditor.PlaybackFlightRecordFullPath);
            }
            else
            {
                outputDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }

            String filePath = outputDirectory  + "\\";
            filePath = filePath + "FlightSnapShot" + flightId + "_" + snapshotCount.ToString() + ".png";
            bool writeImage = this.isRecording;
            if (this.isPlayingBack)
            {
                writeImage = true;
                // Compare images and only write if the images are different.
                if (File.Exists(filePath))
                {
                    Bitmap bitmapCurrent = GetBitmap(bitmapSource);
                    using (Bitmap bitmapExisting = new Bitmap(filePath))
                    { 
                        bool areEqualImages = AreEqualBitmaps(bitmapCurrent, bitmapExisting);
                        writeImage = !areEqualImages;
                    }
                }
            }
            if (writeImage)
            {
                this.wasImageSaved = true;
                PngBitmapEncoder pngEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                { 
                    pngEncoder.Save(stream);
                    stream.Close();
                }
            }
            snapshotCount++;
        }

        private static Bitmap GetBitmap(BitmapSource bitmapSource)
        {
            // Convert from bitmap source to bitmap.
            MemoryStream memoryStream = new MemoryStream();
            BmpBitmapEncoder bitmapEncoder = new BmpBitmapEncoder();
            bitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            bitmapEncoder.Save(memoryStream);
            Bitmap outputBitmap = new Bitmap(memoryStream);
            memoryStream.Close();
            return outputBitmap;
        }

        private static bool AreEqualBitmaps(Bitmap left, Bitmap right)
        {
            bool areImagesEqual = true;
            if (left.Width == right.Width && left.Height == right.Height)
            {
                for (int h = 0; h < left.Height && areImagesEqual; h++)
                {
                    for (int w = 0; w < left.Width && areImagesEqual; w++)
                    {
                        Color leftPixel = left.GetPixel(w, h);
                        Color rightPixel = right.GetPixel(w,h);
                        if (leftPixel != rightPixel)
                        {
                            areImagesEqual = false;
                        }
                    }
                }
            }
            else
            {
                areImagesEqual = false;
            }
            return areImagesEqual;
        }

        internal void Playback(string fullFilePath)
        {
            this.isRecording = false;
            XmlSerializer serializer = new XmlSerializer(flightEventCollection.GetType());
            FileStream fs = new FileStream(fullFilePath, FileMode.Open);
            this.flightEventCollection = (List<FlightEvent>)serializer.Deserialize(fs);
            fs.Close();
            SetFlightIdFromFileName(fullFilePath);
            this.isPlayingBack = true;
            this.playBackIndex = 0;
            playBackTimer = new DispatcherTimer();
            // Timespan is in nano seconds. 10 ^ -9.
            playBackTimer.Interval = new TimeSpan(250000);
            playBackTimer.Tick += playBackTimer_Tick;
            playBackTimer.Start();
        }

        private void SetFlightIdFromFileName(string fullFilePath)
        {
            if (fullFilePath.EndsWith(".xml"))
            {
                int index = fullFilePath.Length - 5;
                int count = 0;
                while (index >= 0)
                {
                    char letter = fullFilePath[index];
                    if (!char.IsNumber(letter))
                    {
                        break;
                    }
                    else
                    {
                        index--;
                        count++;
                    }
                }

                if (count != 0)
                {
                    this.flightId = fullFilePath.Substring(index + 1, count);
                }
            }
        }

        void playBackTimer_Tick(object sender, EventArgs e)
        {
            if (playBackIndex < flightEventCollection.Count)
            {
                FlightEvent flightEvent = flightEventCollection[playBackIndex];
                flightEvent.Playback(this.textEditor);
                playBackIndex++;
            }
            else
            {
                isPlayingBack = false;
                playBackTimer.Stop();
                playBackTimer = null;
                playBackIndex = 0;
                this.flightEventCollection = new List<FlightEvent>();

                if (this.isExitAfterPlayBack)
                {
                    int exitCode = this.wasImageSaved ? -1 : 0;
                    Environment.Exit(exitCode);
                }
            }
        }

        ~FlightRecorder()
        {
            WriteEventCollectionToFile();
        }

        public bool ExitAfterPlayback
        {
            set { this.isExitAfterPlayBack = value; }
        }

        public List<FlightEvent> flightEventCollection;
        private TextEditor textEditor;
        DispatcherTimer playBackTimer;
        int playBackIndex;
        bool isRecording;
        bool isPlayingBack;
        bool isExitAfterPlayBack;
        bool wasImageSaved;
        uint snapshotCount;
        string flightId;
    }
}
