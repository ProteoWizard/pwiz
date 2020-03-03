using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using DigitalRune.Windows.Docking;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;

namespace pwiz.SkylineTestUtil
{
    public class ScreenshotManager
    {
        protected const string ROOT_ELEMENT = "shot_list";

        private List<SkylineScreenshot> _shotSequence = new List<SkylineScreenshot>();
//        private ShotType _defaultShotType = ShotType.ActiveWindow;
        private SkylineWindow _skylineWindow;
        private int _currentShotIndex;
        private TestContext _ctx;
        private XmlDocument _storage;


        public class PointFactor
        {
            private float _factor;

            public PointFactor(float pFactor) { _factor = pFactor; }

            public float getFloat() { return _factor; }
            public static Point operator *(Point pt, PointFactor pFactor) => new Point((int)Math.Round(pt.X * pFactor._factor), (int)Math.Round(pt.Y * pFactor._factor));
            public static Size operator *(Size sz, PointFactor pFactor) => new Size((int)Math.Round(sz.Width * pFactor._factor), (int)Math.Round(sz.Height * pFactor._factor));
            public static Rectangle operator *(Rectangle rect, PointFactor pFactor) => new Rectangle(rect.Location * pFactor, rect.Size * pFactor);
        }

        public class PointAdditive
        {
            private Point _add;
            public PointAdditive(Point pAdd) { _add = pAdd; }
            public PointAdditive(int pX, int pY) { _add = new Point(pX, pY); }
            public static PointAdditive operator +(Point pt, PointAdditive pAdd) => new PointAdditive(new Point(pt.X + pAdd._add.X, pt.Y + pAdd._add.Y));
            public static Size operator +(Size sz, PointAdditive pAdd) => new Size(sz.Width + pAdd._add.X, sz.Height + pAdd._add.Y);

            public static implicit operator Point(PointAdditive add) => add._add;
        }
        public enum ShotType{ ActiveWindow, SkylineWindow, SkylineCustomArea}

        private abstract class SkylineScreenshot
        {
//            private readonly ShotType _type;
//            private readonly SkylineWindow _skylineWindow;

            protected const string SHOT_ELEMENT = "shot";
            protected const string SHOT_TYPE_ATTRIBUTE = "type";
            protected const string SHOT_TYPE_VAL_ACTIVE_FORM = "active_form";
            protected const string SHOT_TYPE_VAL_CUSTOM_AREA = "skyline_relative_frame";
            protected const string SHOT_FRAME_ELEMENT = "frame";
            protected const string SHOT_FRAME_LEFT_ATTRIBUTE = "left";
            protected const string SHOT_FRAME_TOP_ATTRIBUTE = "top";
            protected const string SHOT_FRAME_RIGHT_ATTRIBUTE = "right";
            protected const string SHOT_FRAME_BOTTOM_ATTRIBUTE = "bottom";


            [DllImport("gdi32.dll")]
            static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
            [DllImport("user32.dll")]
            private static extern IntPtr GetForegroundWindow();

            public enum DeviceCap
            {
                VERTRES = 10,
                DESKTOPVERTRES = 117,
            }
            
            /**
             * Factory method
             */
            public static SkylineScreenshot createScreenshot(SkylineWindow pSkylineWindow, [NotNull] XmlNode shotNode)
            {
                // ReSharper disable PossibleNullReferenceException
                if (shotNode.Attributes[SHOT_TYPE_ATTRIBUTE] == null)
                    throw new InvalidDataException("Invalid XML. type attribute was expected but was not found.");

                if (shotNode.Attributes[SHOT_TYPE_ATTRIBUTE].Value == SHOT_TYPE_VAL_ACTIVE_FORM)
                    return new ActiveWindowShot(pSkylineWindow, shotNode);
                else if (shotNode.Attributes[SHOT_TYPE_ATTRIBUTE].Value == SHOT_TYPE_VAL_CUSTOM_AREA)
                    return new CustomAreaShot(pSkylineWindow, shotNode);
                else  throw new InvalidDataException("Unsupported screenshot type");
                // ReSharper enable PossibleNullReferenceException
            }

            public SkylineScreenshot(ShotType pShotType, SkylineWindow pSkylineWindow)
            {
//                _type = pShotType;
//                _skylineWindow = pSkylineWindow;
            }

            protected PointFactor GetScalingFactor()
            {
                Graphics g = Graphics.FromHwnd(IntPtr.Zero);
                IntPtr desktop = g.GetHdc();
                int LogicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
                int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

                float ScreenScalingFactor = PhysicalScreenHeight / (float)LogicalScreenHeight;

                return new PointFactor(ScreenScalingFactor); // 1.25 = 125%
            }


            protected Rectangle GetWindowRectangle(Form frm)
            {
                Rectangle snapshotBounds = Rectangle.Empty;

                DockState[] dockedStates = new DockState[]{DockState.DockBottom, DockState.DockLeft, DockState.DockBottom, DockState.DockTop, DockState.Document};
                if (frm is DockableForm && dockedStates.Any((state) => ((frm as DockableForm).DockState == state) )  )
                {
                    Point origin = Point.Empty;
                    frm.Invoke(new Action(() => { origin = frm.PointToScreen(new Point(0, 0)); }));
                    PointAdditive frameOffset = new PointAdditive(-((frm as DockableForm).Pane.Width - frm.Width) / 2,
                        -((frm as DockableForm).Pane.Height - frm.Height));
                    snapshotBounds = new Rectangle(origin + frameOffset, (frm as DockableForm).Pane.Size);
                }
                else
                {
                    if (frm.ParentForm is FloatingWindow)
                        frm = frm.ParentForm;
                    int frameWidth = (frm.DesktopBounds.Width - frm.ClientRectangle.Width) / 2 - SystemInformation.Border3DSize.Width;
                    Size imageSize = frm.Size + new PointAdditive(-2 * frameWidth, -frameWidth - 1);
                    Point sourcePoint = frm.Location + new PointAdditive(frameWidth, 0);
                    snapshotBounds = new Rectangle(sourcePoint, imageSize);

                }
                return snapshotBounds * GetScalingFactor();
            }

            /**
             * Incapsulates UI actions required to configure the screenshot. In the case of CustomAreaShot it should
             * show the framing window and take its coordinates. Nothing to be done for an ActiveWindowShot.
             */
            public abstract void SetUp();
            public abstract Bitmap Take(Form activeWindow);
            public abstract XmlNode Serialize(XmlDocument pDoc);
        }

        private class ActiveWindowShot : SkylineScreenshot
        {
            public ActiveWindowShot(SkylineWindow pSkylineWindow, XmlNode pNode) : 
                base(ShotType.ActiveWindow, pSkylineWindow)
            {

            }
            public ActiveWindowShot(SkylineWindow pSkylineWindow) :
                base(ShotType.ActiveWindow, pSkylineWindow)
            {

            }

            public override void SetUp() {}

            [NotNull]
            public override Bitmap Take(Form activeWindow)
            {
                Rectangle shotFrame = GetWindowRectangle(activeWindow);
                Bitmap bmCapture = new Bitmap(shotFrame.Width, shotFrame.Height, PixelFormat.Format32bppArgb);
                Graphics graphCapture = Graphics.FromImage(bmCapture);
                graphCapture.CopyFromScreen(shotFrame.Location,
                    new Point(0, 0), shotFrame.Size);
                graphCapture.Dispose();
                return bmCapture;
            }

            [NotNull]
            public override XmlNode Serialize(XmlDocument pDoc)
            {
                XmlNode node = pDoc.CreateElement(SHOT_ELEMENT);
                XmlAttribute typeAttr = pDoc.CreateAttribute(SHOT_TYPE_ATTRIBUTE);
                typeAttr.Value = SHOT_TYPE_VAL_ACTIVE_FORM;
                node.Attributes.Append(typeAttr);
                pDoc.DocumentElement.AppendChild(node);
                return node;
            }

        }

        private class CustomAreaShot : SkylineScreenshot
        {
            private Rectangle _shotFrame;
            public CustomAreaShot(SkylineWindow pSkylineWindow, [NotNull] XmlNode pNode) : base(ShotType.ActiveWindow, pSkylineWindow)
            {
                if (pNode.FirstChild != null && pNode.FirstChild.LocalName == SHOT_FRAME_ELEMENT)
                {
                    XmlAttributeCollection rAtts = pNode.FirstChild.Attributes;
                    _shotFrame = new Rectangle(Int16.Parse(rAtts[SHOT_FRAME_LEFT_ATTRIBUTE].Value),
                        Int16.Parse(rAtts[SHOT_FRAME_TOP_ATTRIBUTE].Value),
                        Int16.Parse(rAtts[SHOT_FRAME_RIGHT_ATTRIBUTE].Value),
                        Int16.Parse(rAtts[SHOT_FRAME_BOTTOM_ATTRIBUTE].Value)
                    );
                }
                else throw new InvalidDataException("Expected frame coordinates for this type of a screenshot, but it was not found.");
            }
            public CustomAreaShot(SkylineWindow pSkylineWindow) : base(ShotType.SkylineCustomArea, pSkylineWindow)
            {
                
            }

            [NotNull]
            public override XmlNode Serialize(XmlDocument pDoc)
            {
                throw new NotImplementedException();
            }

            /**
             * Display the snapshot area selector and return the resulting rectangle
             * in display coordinates.
             */
            public Rectangle ShowAreaSelector()
            {
                throw new NotImplementedException();
            }

            public override void SetUp()
            {
            }

            public override Bitmap Take(Form activeWindow)
            {
                Bitmap bmCapture = new Bitmap(_shotFrame.Width, _shotFrame.Height, PixelFormat.Format32bppArgb);
                Graphics graphCapture = Graphics.FromImage(bmCapture);
                graphCapture.CopyFromScreen(_shotFrame.Location,
                    new Point(0, 0), _shotFrame.Size);
                graphCapture.Dispose();
                return bmCapture;
            }

        }

        private string filePath
        {
            get
            {
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(exeDir ?? "", _ctx.TestName + "_shots");
            }
        }

        public ScreenshotManager([NotNull] TestContext ctx, [NotNull] SkylineWindow pSkylineWindow)
        {
            //look up the settings file, read and parse if found
            //set up defaults otherwise
            _ctx = ctx;



            _storage = new XmlDocument();

            if (File.Exists(filePath))
            {
                _storage.Load(filePath);
                XmlNode root = _storage.DocumentElement;
                if (root.HasChildNodes)
                {
                    foreach (XmlNode shotNode in root.ChildNodes)
                    {
                        _shotSequence.Add(SkylineScreenshot.createScreenshot(pSkylineWindow, shotNode));
                    }
                }
            }
            else
                _storage.AppendChild(_storage.CreateElement(ROOT_ELEMENT));

            _currentShotIndex = -1;
        }


        public void TakeNextShot(Form activeWindow)
        {
            _skylineWindow = Program.MainWindow;
            if (activeWindow == null)
                activeWindow = _skylineWindow;
            Bitmap shotPic;
            if ( ++_currentShotIndex < _shotSequence.Count)
            {
                shotPic = _shotSequence[_currentShotIndex].Take(activeWindow);
            }
            else
            {
                //check UI and create a blank shot according to the user selection
                SkylineScreenshot newShot = new ActiveWindowShot(_skylineWindow);
                _shotSequence.Add(newShot);
                _currentShotIndex = _shotSequence.Count - 1;
                shotPic = _shotSequence.Last().Take(activeWindow);
                _storage.DocumentElement.AppendChild(newShot.Serialize(_storage));
                SaveToFile();
            }

            if (shotPic != null)
            {
                //Have to do it this way because of the limitation on OLE access from background threads.
                Thread clipThread = new Thread(() => Clipboard.SetImage(shotPic));
                clipThread.SetApartmentState(ApartmentState.STA);
                clipThread.Start();
                clipThread.Join();
            }
        }

        private void SaveToFile()
        {
            if(File.Exists(filePath))
                File.Delete(filePath);

            _storage.Save(filePath);
        }
    }

}

