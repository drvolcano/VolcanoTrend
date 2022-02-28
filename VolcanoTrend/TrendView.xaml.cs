using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VolcanoTrend.Trend;

namespace VolcanoTrend
{
    /// <summary>
    /// Interaktionslogik für Trend.xaml
    /// </summary>
    public partial class TrendView : UserControl
    {
        /// <summary>
        /// Vertikale Unterteilung
        /// </summary>
        public int RasterV { get; set; } = 10;

        private int PaddingLeft { get; set; } = 15;
        private int PaddingRight { get; set; } = 15;
        private int PaddingTop { get; set; } = 15;
        private int PaddingBottom { get; set; } = 15;

        private int RulerFang { get; } = 10;
        private int RulerFangTouch { get; } = 10;

        /// <summary>
        /// Wieveile Punkte maximal pro Pixel-Spalte gezeichnet werden
        /// </summary>
        private double MaxPointsPerPixel { get; } = 10;

        public Color ColorBackground { get; set; } = Colors.White;
        public Color ColorGrid { get; set; } = Colors.LightGray;

        private bool hasvalidsize = false;

        private TrendRuler SelectedRuler = null;

        private System.Drawing.Rectangle TrendArea;

        private DateTime MouseLocation;
        private Point MousePoint;
        private Point DownLocation;

        public long StartXOnDown;
        public long EndXOnDown;
        public long RulerOnDown;

        private int updatelevelandbelow = 0;
        private Dictionary<int, FingerInfo> Finger { get; } = new Dictionary<int, FingerInfo>();

        private VolcanoBitmapLib.VolcanoBitmap Layer3;//Ruler
        private VolcanoBitmapLib.VolcanoBitmap Layer2;//Kurven
        private VolcanoBitmapLib.VolcanoBitmap Layer1;//Hintergrund mit Linien

        private double TicksPerPixel;

        public event EventHandler Refreshed;

        private long _StartX;

        public DateTime StartX
        { get { return new DateTime(_StartX); } set { _StartX = value.Ticks; Curve_PropertyChanged(null, null); } }

        private long _EndX;

        public DateTime EndX
        { get { return new DateTime(_EndX); } set { _EndX = value.Ticks; Curve_PropertyChanged(null, null); } }

        /// <summary>
        /// Alle Kurven
        /// </summary>
        private List<TrendCurve> Curves { get; } = new List<TrendCurve>();

        /// <summary>
        /// Akke Liniale
        /// </summary>
        private List<TrendRuler> Rulers { get; } = new List<TrendRuler>();

        private class FingerInfo
        {
            public int ID;
            public long DownTime;
            public double DownPosition;
            public long ActualTime;
            public double ActualPosition;
        }

        /// <summary>
        /// Trend an angegebenem Zeitpunkt vergrössern oder verkleinern
        /// </summary>
        /// <param name="factor"></param>
        /// <param name="location"></param>
        public void Zoom(double factor, DateTime location)
        {
            Zoom(factor, location.Ticks);
        }

        /// <summary>
        /// Trend an angegebenem Zeitpunkt vergrössern oder verkleinern
        /// </summary>
        /// <param name="factor"></param>
        /// <param name="location"></param>
        public void Zoom(double factor, long location)
        {
            long newStart = (location + (long)((_StartX - location) * factor));
            long newEnd = (location + (long)((_EndX - location) * factor));
            _StartX = newStart;
            _EndX = newEnd;

            StartXOnDown = _StartX;
            EndXOnDown = _EndX;
            DownLocation = MousePoint;

            REFRESH_LVL2();
        }

        /// <summary>
        /// Trend an angegebenem Zeitpunkt vergrössern oder verkleinern
        /// </summary>
        /// <param name="span"></param>
        /// <param name="location"></param>
        public void Zoom(TimeSpan span, DateTime location)
        {
            Zoom(span, location.Ticks);
        }

        /// <summary>
        /// Trend an angegebenem Zeitpunkt vergrössern oder verkleinern
        /// </summary>
        /// <param name="span"></param>
        /// <param name="location"></param>
        public void Zoom(TimeSpan span, long location)
        {
            double lastspan = _EndX - _StartX;
            double factor = span.Ticks / lastspan;

            long newEnd = (location + (long)((_EndX - location) * factor));

            long newStart = newEnd - span.Ticks;

            _StartX = newStart;
            _EndX = newEnd;

            StartXOnDown = _StartX;
            EndXOnDown = _EndX;
            DownLocation = MousePoint;

            REFRESH_LVL2();
        }

        /// <summary>
        /// Lineal hinzufügen zum Trend
        /// </summary>
        /// <param name="ruler"></param>
        public void AddRuler(TrendRuler ruler)
        {
            Rulers.Add(ruler);
        }

        /// <summary>
        /// Kurve hinzufügen zum Trend
        /// </summary>
        /// <param name="curve"></param>
        public void AddCurve(TrendCurve curve)
        {
            Curves.Add(curve);
            curve.PropertyChanged += Curve_PropertyChanged;
        }

        /// <summary>
        /// Kurve zeichnen
        /// </summary>
        /// <param name="bmp">Bitmap, auf das die Kurve gezeichnet werden soll</param>
        /// <param name="curve">Kurve, die gezeichnet werden soll</param>
        /// <param name="Start">Frühester Punkt ,der dargestellt werden soll</param>
        /// <param name="End">Spätester Punkt der dargestellt werden soll</param>
        private void DrawCurve(VolcanoBitmapLib.VolcanoBitmap bmp, TrendCurve curve, long Start, long End)
        {
            if (!curve.Visible)
                return;

            TrendPoint lastpoint = null;
            TrendPoint lastused = null;

            List<Point> points = new List<Point>();

            //Nur darstellbare Punkte zeichnen (+ enen ausserhalb des Bereichs)
            int lower = curve.GetIndexAt(Start, TrendCurve.IndexSide.lower) - 1;
            int upper = curve.GetIndexAt(End, TrendCurve.IndexSide.upper) + 1;

            if (upper > curve.Points.Count - 1)
                upper = curve.Points.Count - 1;

            if (lower < 0)
                lower = 0;

            //Kompressionsalgorithmus
            for (int i = lower; i <= upper; i++)
            {
                TrendPoint p = curve.Points[i];

                //Erst ausführen, wenn mindestens 1 Punkt durchlaufen wurde
                if (lastpoint != null)
                {
                    //Wenn noch kein Punkt festgelegt wurde --> letzten Punkt verwenden
                    if (lastused == null)
                        lastused = lastpoint;

                    //Pixel sparen (max x Punkte pro Pixel)
                    if (p.TimeStamp > lastused.TimeStamp + TicksPerPixel / MaxPointsPerPixel)
                    {
                        points.Add(Translate(lastused, curve));
                        lastused = p;
                    }
                }

                //Obergrenze überschritten
                if (i == upper)
                    if (lastused != p)
                    {
                        //Diesen einen Punkt noch hinzufügen
                        points.Add(Translate(p, curve));
                        break;
                    }

                lastpoint = p;
            }

            //Punkte zusammenschieben
            if (points.Count >= 2)
            {
                if (points[0].X < 0)
                    points[0] = Interpolate(points[0], points[1], 0);

                if (points[points.Count - 1].X > bmp.Width)
                    points[points.Count - 1] = Interpolate(points[points.Count - 1], points[points.Count - 2], bmp.Width);
            }

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(TrendArea.X, TrendArea.Y - 2, TrendArea.Width, TrendArea.Height + 4);

            bmp.DrawCurve(points, curve.Thickness, curve.Color, rect);
        }

        /// <summary>
        /// Umrechnen von Zeit/Wert auf Pixel
        /// </summary>
        /// <param name="bmp">Bitmap, auf das gezeichnet werden soll</param>
        /// <param name="curve">Kufve, die den TrendPoint enthält</param>
        /// <param name="Null">Zeitpunkt, an dem die Darstellung beginnt</param>
        /// <param name="fx">Skalierung in X</param>
        /// <param name="fy">Skalierung in Y</param>
        /// <returns></returns>
        private Point Translate(TrendPoint point, TrendCurve curve)
        {
            double spanh = curve.Max - curve.Min;
            double fy = TrendArea.Height / spanh;

            return new Point(
               TicksToPixel(point.TimeStamp),
                ((double)TrendArea.Height - (point.Value - curve.Min) * fy) + TrendArea.Y);
        }

        private Point Interpolate(Point p1, Point p2, double p3)
        {
            double k = (p2.Y - p1.Y) / (p2.X - p1.X);
            return new Point(p3, p1.Y + (p3 - p1.X) * k);
        }

        public void SetRange(DateTime Start, DateTime End)
        {
            SetRange(Start.Ticks, End.Ticks);
        }

        public void SetRange(long Start, long End)
        {
            _EndX = End;
            _StartX = Start;

            REFRESH_LVL2();
        }

        public void ShowFullCurve(TrendCurve curve)
        {
            if (curve.Points.Count >= 2)
                SetRange(curve.Points[0].TimeStamp, curve.Points[curve.Points.Count - 1].TimeStamp);
        }

        private void Curve_PropertyChanged(object sender, TrendCurve c_)
        {
            REFRESH_LVL2();
        }

        public void REFRESH_LVL2()
        {
            if (updatelevelandbelow > 2 || updatelevelandbelow == 0)
                updatelevelandbelow = 2;
        }

        private void REFRESH_LVL3()
        {
            if (updatelevelandbelow > 3 || updatelevelandbelow == 0)
                updatelevelandbelow = 3;
        }

        //Schneller? : Buffer.BlockCopy
        public static void ClearArrayCopy(int[] pixels, int[] clearTo, int len)
        {
            Array.Copy(clearTo, 0, pixels, 0, len);
        }

        /// <summary>
        /// Bitmep mit Hintergrundfarbe und Rasterlinien rendern
        /// </summary>
        private void Render_LVL1()
        {
            TrendArea = new System.Drawing.Rectangle(PaddingLeft, PaddingTop, (int)(ActualWidth - this.PaddingLeft - this.PaddingRight), (int)ActualHeight - this.PaddingTop - this.PaddingBottom);

            //Bitmap mit Farbe füllen
            Layer1.Clear(ColorBackground);

            //Raster zeichnen
            for (int i = 0; i < RasterV + 1; i++)
            {
                int Y = (int)(i * (TrendArea.Height - 0) / RasterV);

                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, Layer1.Width, Layer1.Height);

                Layer1.DrawCurve(new List<Point> { new Point(TrendArea.X, TrendArea.Y + Y), new Point(TrendArea.X + TrendArea.Width, TrendArea.Y + Y) }, 1.0, ColorGrid, rect);
            }
        }

        public WriteableBitmap GetImageLvl2()
        {
            return Layer2.Bitmap;
        }

        /// <summary>
        /// Kurven zeichnen
        /// </summary>
        private void Render_LVL2()
        {
            TicksPerPixel = (double)(_EndX - _StartX) / (double)TrendArea.Width;

            //Bereits gezeichnetes Raster aus Layer 1 übernehmen
            Layer2.Fill(Layer1);

            //Kurven zeichnen
            foreach (TrendCurve curve in Curves)
                DrawCurve(Layer2, curve, _StartX, _EndX);
        }

        /// <summary>
        /// Ruler zeichnen
        /// </summary>
        private void Render_LVL3()
        {
            //Layer 3 wird zur Anzeige genutzt - daher muss es gesperrt werden, um darauf zeichnen zu können
            Layer3.Lock();

            //Bereits gezeichnetes Raster und Kurven aus Layer 2 übernehmen
            Layer3.Fill(Layer2);

            //Ruler zeichnen
            foreach (TrendRuler ruler in Rulers)
            {
                //Ruler in den Zeichenbereich bringen
                if (ruler.Location.Ticks < _StartX)
                    ruler.Location = new DateTime(_StartX);

                if (ruler.Location.Ticks > _EndX)
                    ruler.Location = new DateTime(_EndX);

                //Ruler zeichnen
                if (_EndX != 0)
                    DrawRuler(ruler);
            }

            //Festlegen, dass der komplette Zeichenbereich aktualisiert werden soll
            Layer3.Bitmap.AddDirtyRect(new Int32Rect(0, 0, (int)Layer3.Width, (int)Layer3.Height));

            //Layer 3 wieder freigeben zur Anzeige
            Layer3.Unlock();
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            int width = (int)ActualWidth;
            int height = (int)ActualHeight;

            hasvalidsize = width != 0 && height != 0;

            if (hasvalidsize)
            {
                int level = updatelevelandbelow;
                updatelevelandbelow = 0;

                if (level != 0)
                {
                    if (level <= 1)
                    {
                        //Layer für Raster
                        Layer1 = new VolcanoBitmapLib.VolcanoBitmap(width, height);
                        Layer1.Lock();

                        //Layer für Trend
                        Layer2 = new VolcanoBitmapLib.VolcanoBitmap(width, height);
                        Layer2.Lock();

                        //Layer für Ruler
                        Layer3 = new VolcanoBitmapLib.VolcanoBitmap(width, height);

                        imgctrl.Source = Layer3.Bitmap;
                        hasvalidsize = true;

                        Render_LVL1();
                    }

                    if (level <= 2)
                        Render_LVL2();

                    if (level <= 3)
                        Render_LVL3();

                    Refreshed?.Invoke(this, null);
                }
            }
        }

        public TrendView()
        {
            InitializeComponent();

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            RenderOptions.SetBitmapScalingMode(imgctrl, BitmapScalingMode.Unspecified);
            RenderOptions.SetEdgeMode(imgctrl, EdgeMode.Unspecified);

            this.SizeChanged += Trend_SizeChanged;

            //Maus
            MouseMove += Trend_MouseMove;
            MouseDown += Trend_MouseDown;
            MouseUp += Trend_MouseUp;
            MouseWheel += Trend_MouseWheel;
            MouseLeave += Trend_MouseLeave;
            MouseEnter += TrendView_MouseEnter;

            //Touch
            TouchDown += Trend_TouchDown;
            TouchMove += Trend_TouchMove;
            TouchUp += Trend_TouchUp;
        }

        #region Touch

        private void Trend_TouchUp(object sender, TouchEventArgs e)
        {
            e.Handled = true;

            Finger.Remove(e.TouchDevice.Id);

            if (Finger.Count == 0)
            {
                SelectedRuler = null;
                REFRESH_LVL3();
            }
        }

        private void Trend_TouchMove(object sender, TouchEventArgs e)
        {
            if (DatenWerdenGeladen) return;
            if (!hasvalidsize) return;
            if (imgctrl.ActualWidth == 0) return;

            e.Handled = true;

            //3. Finger ignorieren
            if (!Finger.ContainsKey(e.TouchDevice.Id))
                return;

            Finger[e.TouchDevice.Id].ActualPosition = e.TouchDevice.GetTouchPoint(imgctrl).Position.X;
            Finger[e.TouchDevice.Id].ActualTime = GetDateTimeAtTouchLocation(e);

            if (SelectedRuler == null)
            {
                if (Finger.Count == 1)
                {
                    FingerInfo f1 = Finger.Values.ElementAt(0);
                    long DeltaT = _EndX - _StartX;

                    _StartX = _StartX - f1.ActualTime + f1.DownTime;
                    _EndX = _StartX + DeltaT;

                    REFRESH_LVL2();

                    moved = true;
                }

                if (Finger.Count == 2)
                {
                    FingerInfo f1 = Finger.Values.ElementAt(0);
                    FingerInfo f2 = Finger.Values.ElementAt(1);

                    //Zeit zwischen beiden Punkten
                    long DeltaT = f1.DownTime - f2.DownTime;
                    //Pixel zwischen beiden Punkten
                    double DeltaP = f1.ActualPosition - f2.ActualPosition;

                    double f = DeltaT / DeltaP;

                    if (f > 0)
                    {
                        _StartX = f1.DownTime - (long)(f1.ActualPosition * f);
                        _EndX = _StartX + (long)(imgctrl.ActualWidth * f);

                        REFRESH_LVL2();

                        moved = true;
                    }
                }
            }
            else
            {
                if (Finger.Count == 1)
                {
                    SelectedRuler.Location = new DateTime(GetDateTimeAtTouchLocation(e));
                    REFRESH_LVL3();
                }
            }

            StartXOnDown = 0;
        }

        private void Trend_TouchDown(object sender, TouchEventArgs e)
        {
            if (Finger.Count < 2)
                if (!Finger.ContainsKey(e.TouchDevice.Id))
                {
                    FingerInfo info = new FingerInfo
                    {
                        ID = e.TouchDevice.Id,
                        DownPosition = e.TouchDevice.GetTouchPoint(imgctrl).Position.X
                    };
                    info.ActualPosition = info.DownPosition;
                    info.DownTime = GetDateTimeAtTouchLocation(e);
                    info.ActualTime = info.DownTime;

                    Finger.Add(e.TouchDevice.Id, info);

                    if (Finger.Count == 1)
                        foreach (TrendRuler r in Rulers)
                        {
                            double pixel = TicksToPixel(r.Location.Ticks);
                            double delta = Math.Abs(info.DownPosition - pixel);

                            if (delta < RulerFangTouch)
                            {
                                SelectedRuler = r;
                                RulerOnDown = SelectedRuler.Location.Ticks;
                                REFRESH_LVL3();
                            }
                        }

                    e.Handled = true;
                }
        }

        #endregion Touch

        #region Mouse

        private void TrendView_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
                SelectedRuler = null;
        }

        private void Trend_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Trend_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StartXOnDown = 0;//Bugfix: wenn Maus mit gedrückter Taste in Trend gefahren wird gibt es Probleme
            SelectedRuler = null;

            REFRESH_LVL2();
        }

        private void Trend_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Zoom((1.0 + e.Delta / 1000.0), MouseLocation);
        }

        private void Trend_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                foreach (TrendRuler r in Rulers)
                {
                    double pixel = TicksToPixel(r.Location.Ticks);
                    double delta = Math.Abs(MousePoint.X - pixel);

                    if (delta < RulerFang)
                    {
                        SelectedRuler = r;
                        RulerOnDown = SelectedRuler.Location.Ticks;
                        REFRESH_LVL3();
                    }
                }

                StartXOnDown = _StartX;
                EndXOnDown = _EndX;

                DownLocation = MousePoint;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
            }
        }

        #endregion Mouse

        public DateTime GetDateTimeAtMouseLocation(MouseEventArgs e)
        {
            Point location = e.GetPosition(imgctrl);
            return new DateTime(PixelToTicks(location.X));
        }

        private long PixelToTicks(double X)
        {
            return (long)(_StartX + (X - TrendArea.X) * TicksPerPixel);
        }

        private double TicksToPixel(long T)
        {
            return TrendArea.X + (double)(T - _StartX) / TicksPerPixel;
        }

        public long GetDateTimeAtTouchLocation(TouchEventArgs e)
        {
            TouchPoint location = e.TouchDevice.GetTouchPoint(imgctrl);
            return PixelToTicks(location.Position.X);
        }

        public bool moved;

        public bool DatenWerdenGeladen;

        private void Trend_MouseMove(object sender, MouseEventArgs e)
        {
            //-----------------------------------------------------
            // Abbrechen, wenn noch nicht komplett initialisiert
            //-----------------------------------------------------

            if (DatenWerdenGeladen)
                return;

            if (!hasvalidsize)
                return;

            if (imgctrl.ActualWidth == 0)
                return;

            //-----------------------------------------------------
            // Prüfen, ob sich die Maus über einem Ruler befindet
            //-----------------------------------------------------

            bool found = false;

            foreach (TrendRuler ruler in Rulers)
            {
                double pixel = TicksToPixel(ruler.Location.Ticks);
                double delta = Math.Abs(MousePoint.X - pixel);

                if (delta < RulerFang)
                {
                    Mouse.OverrideCursor = Cursors.ScrollWE;
                    found = true;
                }
            }

            //-----------------------------------------------------

            MouseLocation = GetDateTimeAtMouseLocation(e);
            MousePoint = e.GetPosition(imgctrl);

            //-----------------------------------------------------
            // Verschieben
            //-----------------------------------------------------

            if (e.LeftButton == MouseButtonState.Pressed && StartXOnDown != 0)
            {
                double MouseDelta = e.GetPosition(imgctrl).X - DownLocation.X;
                long Delta = (long)(MouseDelta * TicksPerPixel);

                //Ruler oder gesamten Trend verschieben
                if (SelectedRuler == null)
                {
                    Mouse.OverrideCursor = Cursors.Arrow;
                    found = true;
                    _StartX = StartXOnDown - Delta;
                    _EndX = EndXOnDown - Delta;

                    REFRESH_LVL2();
                }
                else
                {
                    Mouse.OverrideCursor = Cursors.ScrollWE;
                    found = true;
                    SelectedRuler.Location = new DateTime(RulerOnDown + Delta);
                    REFRESH_LVL3();
                }

                moved = true;
            }

            if (!found)
                Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void DrawRuler(TrendRuler ruler)
        {
            int x = (int)TicksToPixel(ruler.Location.Ticks);

            //Vertikelen Strich zeichnen
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, Layer3.Width, Layer3.Height);

            if (SelectedRuler == ruler)
                Layer3.DrawCurve(new List<Point>() { new Point(x, TrendArea.Y), new Point(x, TrendArea.Y + TrendArea.Height) }, 4.0, Colors.Black, rect);
            else
                Layer3.DrawCurve(new List<Point>() { new Point(x, TrendArea.Y), new Point(x, TrendArea.Y + TrendArea.Height) }, 3.0, Colors.Black, rect);

            foreach (TrendCurve curve in Curves)
                if (curve.Visible)
                {
                    double value_intepol = curve.GetValueAt(ruler.Location, true);

                    Point pnt = Translate(new TrendPoint(ruler.Location.Ticks, value_intepol), curve);

                    Layer3.DrawDot(pnt, 12, Colors.Black);
                    Layer3.DrawDot(pnt, 6, curve.Color);
                }
        }

        /// <summary>
        /// Grösse des Zeichenbereichs ändern
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        private void ChangeBitmapSize()
        {
            if (updatelevelandbelow > 1 || updatelevelandbelow == 0)
                updatelevelandbelow = 1;
        }

        private void Trend_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ChangeBitmapSize();
        }
    }
}