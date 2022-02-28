using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VolcanoTrend
{
    /// <summary>
    /// Stellt Funktionen zur performanten Bildbearbeitung zur Verfügung
    /// </summary>
    public static class VolcanoBitmapLib
    {
        /// <summary>
        /// Farbe in UInt32 umwandeln
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static UInt32 UInt32FromColor(Color color)
        {
            return (UInt32)(
                (color.A << 24) |
                (color.R << 16) |
                (color.G << 8) |
                (color.B << 0));
        }

        /// <summary>
        ///
        /// </summary>
        public unsafe class VolcanoBitmap
        {
            /// <summary>
            /// Reserviert den Hintergrundpuffer für Aktualisierungen.
            /// </summary>
            public void Lock()
            {
                Bitmap.Lock();
            }

            /// <summary>
            /// Gibt den Hintergrundpuffer frei, um ihn für die Anzeige verfügbar zu machen
            /// </summary>
            public void Unlock()
            {
                Bitmap.Unlock();
            }

            public void LoadRenderTargetBitmap(RenderTargetBitmap rbmp)
            {
                Bitmap.Lock();

                rbmp.CopyPixels(new Int32Rect(0, 0, rbmp.PixelWidth, rbmp.PixelHeight),
                  Bitmap.BackBuffer,
                  Bitmap.BackBufferStride * Bitmap.PixelHeight, Bitmap.BackBufferStride);

                Bitmap.Unlock();
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="width">Breite in Pixeln</param>
            /// <param name="height">Höhe in Pixeln</param>
            public VolcanoBitmap(RenderTargetBitmap rbmp)
            {
                int width = rbmp.PixelWidth;
                int height = rbmp.PixelHeight;

                Bitmap = new WriteableBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgr32,
                    null);

                Bitmap.Lock();

                rbmp.CopyPixels(new Int32Rect(0, 0, rbmp.PixelWidth, rbmp.PixelHeight),
                  Bitmap.BackBuffer,
                  Bitmap.BackBufferStride * Bitmap.PixelHeight, Bitmap.BackBufferStride);

                Bitmap.Unlock();

                //Erstelle einen Byte-Buffer für Kurvenfunktionen
                alphabuffer = new byte[height * width];

                //Erstelle eine Pixelbuffer
                pixelbuffer = (UInt32*)Bitmap.BackBuffer.ToPointer();

                Width = width;
                Height = height;
                SizeInPixel = width * height;
                SizeInBytes = SizeInPixel * Bitmap.Format.BitsPerPixel / 8;
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="width">Breite in Pixeln</param>
            /// <param name="height">Höhe in Pixeln</param>
            public VolcanoBitmap(int width, int height)
            {
                Bitmap = new WriteableBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgr32,
                    null);

                //Erstelle einen Byte-Buffer für Kurvenfunktionen
                alphabuffer = new byte[height * width];

                //Erstelle eine Pixelbuffer
                pixelbuffer = (UInt32*)Bitmap.BackBuffer.ToPointer();

                Width = width;
                Height = height;
                SizeInPixel = width * height;
                SizeInBytes = SizeInPixel * Bitmap.Format.BitsPerPixel / 8;
            }

            /// <summary>
            /// Breite des Bitmaps in Pixeln
            /// </summary>
            public int Width { get; }

            /// <summary>
            /// Höhe des Bitmaps in Pixeln
            /// </summary>
            public int Height { get; }

            /// <summary>
            /// Internes Bitmap
            /// </summary>
            public WriteableBitmap Bitmap { get; }

            /// <summary>
            /// Grösse des Bitmaps in Bytes
            /// </summary>
            public int SizeInBytes { get; }

            /// <summary>
            /// Grösse des Bitmaps in Bytes
            /// </summary>
            public int SizeInPixel { get; }

            /// <summary>
            /// Buffer, der zum Zeichnen benutzt wird
            /// </summary>
            public UInt32* pixelbuffer;

            /// <summary>
            /// Füllt das komplette Bitmap mit einer Farbe
            /// </summary>
            /// <param name="Color"></param>
            /// <param name="destination"></param>
            public void Clear(Color Color)
            {
                UInt32 c = UInt32FromColor(Color);

                int maxpos = Width * Height;

                for (int pos = 0; pos < maxpos; pos++)
                    pixelbuffer[pos] = c;
            }

            /// <summary>
            /// Überschreibt die Pixel diese Bitmaps mit denen eines zweiten
            /// </summary>
            /// <param name="source"></param>
            public void Fill(VolcanoBitmap source)
            {
                Buffer.MemoryCopy(source.pixelbuffer, pixelbuffer, SizeInBytes, source.SizeInBytes);
            }

            public void SetPixel(int pos, Color Color)
            {
                UInt32 c = UInt32FromColor(Color);
                pixelbuffer[pos] = c;
            }

            public void SetPixel(Point Point, Color Color)
            {
                int pos = (int)Point.Y * Width + (int)Point.X;
                SetPixel(pos, Color);
            }

            public void DrawPixel(int pos, Color Color)
            {
                UInt32 c = UInt32FromColor(Color);
                UInt32 ca = (c & 0xff00ff);
                UInt32 cb = (c & 0x00ff00);

                int alpha = Color.A;

                UInt32 colora = pixelbuffer[pos];
                UInt32 rb = colora & 0xff00ff;
                UInt32 g = colora & 0x00ff00;
                rb += (ca - rb) * (byte)alpha >> 8;
                g += (cb - g) * (byte)alpha >> 8;
                pixelbuffer[pos] = (rb & 0xff00ff) | (g & 0xff00);
            }

            public void DrawPixel(Point Point, Color Color)
            {
                int pos = (int)Point.Y * Width + (int)Point.X;
                DrawPixel(pos, Color);
            }

            /// <summary>
            /// Einzelnen Punkt zeichnen
            /// </summary>
            /// <param name="Point"></param>
            /// <param name="Thickness"></param>
            /// <param name="Color"></param>
            public void DrawDot(Point Point, double Thickness, Color Color)
            {
                UInt32 c = UInt32FromColor(Color);

                int minx = 0;
                int miny = 0;
                int maxx = Width;
                int maxy = Height;

                double blur = 1.0;

                double posx = Point.X;
                double posy = Point.Y;

                double r = Thickness / 2;
                double rPOW = r * r;
                double rblur = r + blur;
                double rblurPOW = rblur * rblur;

                double rfull = r - 0.5;
                double rfullPOW = rfull * rfull;

                int R = (int)Math.Ceiling(r + blur);

                int posxi = (int)Math.Round(posx);
                int posyi = (int)Math.Round(posy);

                //get color components of line
                byte ColorB_R = (byte)(c >> 16 & 0xFF);
                byte ColorB_G = (byte)(c >> 8 & 0xFF);
                byte ColorB_B = (byte)(c & 0xFF);

                UInt32 ca = (c & 0xff00ff);
                UInt32 cb = (c & 0x00ff00);

                for (int x = -R; x <= R; x++)
                    for (int y = -R; y <= R; y++)
                    {
                        int px = posxi + x;
                        int py = posyi + y;

                        if (px >= minx && px < maxx &&
                            py >= miny && py < maxy)
                        {
                            //distance between pixel and center of circle
                            double dx = px - posx;
                            double dy = py - posy;

                            //suqare distance between pixel and  center of circle (a²+b²=c²)
                            double distancePOW = dx * dx + dy * dy;

                            //only draw pixel when near or inside circle
                            if (distancePOW < rblurPOW)
                            {
                                //location in buffer of bitmap
                                int i = py * Width + px;
                                double alpha;

                                //Kreisinneres voll zeichnen (abzüglich Überlappungsfaktor)
                                if (distancePOW <= rfullPOW)
                                {
                                    alpha = 255.0;
                                }
                                else
                                {
                                    double distance = Math.Sqrt(distancePOW);
                                    double faktor = 1 - (distance - rfull) / blur;
                                    if (faktor < 0.0) faktor = 0.0;

                                    //calculate pen intensity
                                    alpha = 255.0 * faktor;
                                }

                                if (alpha != 0)
                                {
                                    UInt32 colora = pixelbuffer[i];
                                    UInt32 rb = colora & 0xff00ff;
                                    UInt32 g = colora & 0x00ff00;
                                    rb += (ca - rb) * (byte)alpha >> 8;
                                    g += (cb - g) * (byte)alpha >> 8;
                                    pixelbuffer[i] = (rb & 0xff00ff) | (g & 0xff00);
                                }
                            }
                        }
                    }
            }

            private readonly byte[] alphabuffer;

            /// <summary>
            /// Zeichnet eine geglättete Linie mit variabler Dicke
            /// </summary>
            /// <param name="points">Liste von Punkten zwischen denen die Kurve gezeichnet wird</param>
            /// <param name="thickness">Dicke der Linie in Pixeln. Die Performance bticht zum Quadrat der Dicke ein</param>
            /// <param name="Color">Farbe der Kurve</param>
            /// <param name="area">Bereich in dem die Kurve gezeichnet werden soll</param>
            public void DrawCurve(List<Point> points, double thickness, Color color, System.Drawing.Rectangle area)
            {
                GenerateCurve(points, thickness, alphabuffer, area);
                //   GenerateCurve2(points, thickness, alphabuffer, area);
                RenderCurve(alphabuffer, color);
            }

            /// <summary>
            /// Generiert einen Buffer mit Alphawerten
            /// </summary>
            /// <param name="points"></param>
            /// <param name="thickness"></param>
            /// <param name="alphabuffer"></param>
            /// <param name="area"></param>
            public void GenerateCurve(List<Point> points, double thickness, byte[] alphabuffer, System.Drawing.Rectangle area)
            {
                //Zeichenbereich definieren
                int minx = area.X;
                int miny = area.Y;
                int maxx = area.X + area.Width;
                int maxy = area.Y + area.Height;

                //Alphabuffer komplett leeren
                Array.Clear(alphabuffer, 0, alphabuffer.Length);

                //Bereich ausserhalb der Kurve der halbtransparent gezeichnet wird.
                double blur = 1.0;

                //Abstand zwischen 2 Zeichnungen
                //Ein Abstand von einem Vielfachen von 1 verursacht Probleme (0.5, 1, 2, 4,....)
                //(wird aber durch einen Korrekturfaktor nochmal angepasst, darum passt 1.0 hier auch)
                double stepwidth = 1;

                //Abbrechen, wenn nicht mindestens 2 Punkte angegeben wurden
                if (points.Count < 2)
                    return;

                //Startpunkt definieen
                double x0 = points[0].X;
                double y0 = points[0].Y;

                //Radius des fixen Bereichs Pinsels
                double r_solid = thickness / 2;

                //Radius in dem der Pinsel maximal zeichnet
                double r_blur = r_solid + blur;
                double r_blur_POW = r_blur * r_blur;//für Pythagoras

                //Radium, in dem garantiert voll% gezeichnet wird
                double r_full = r_solid - 0.5;
                double r_full_POW = r_full * r_full;//für Pythagoras

                //Maimalen Zeichenbereich definieren
                int r_max = (int)Math.Ceiling(r_blur);

                //Alpha-Verlauf definieren
                double alphaf = 255.0 / thickness * stepwidth;

                //Einzelne Linien zeichnen
                for (int p = 1; p < points.Count; p++)
                {
                    //Koordinaten des 2. Punktes
                    double x1 = points[p].X;
                    double y1 = points[p].Y;

                    //Differenz zwischen erstem und zweitem Punkt berechnen
                    double dtx = x1 - x0;
                    double dty = y1 - y0;

                    //Direkten Weg zwischen den beiden Punkten berechnen
                    double d = Math.Sqrt(dtx * dtx + dty * dty);

                    //Anzahl zu zeichnender Punkte berechnen
                    double steps_ideal = d / stepwidth;
                    int steps = (int)Math.Ceiling(steps_ideal);

                    //Verhältnis Schrittweite theoretisch zu praktisch
                    double corr_factor = steps_ideal / (double)steps;

                    //Einzelne Pinsel-Punkte zeichnen
                    for (int step = 0; step <= steps; step++)

                        //Ersten Punnkt nur bei erster Linie zeichen
                        //Bei allen folgenden Linien ist dieser bereits der Endpunkt der vorhergehenden
                        if (step > 0 || p == 1)
                        //    if(step== steps) //Punktwolke anzeigen
                        {
                            //Mittelpunkt berechnen
                            double pos = (double)step / (double)steps;
                            double center_x = x0 + dtx * pos;
                            double center_y = y0 + dty * pos;

                            //Mittelpunkt auf Raster berechnen
                            int center_x_raster = (int)Math.Round(center_x);
                            int center_y_raster = (int)Math.Round(center_y);

                            //Einzelne Pixel berechnen
                            for (int dx_raster = -r_max; dx_raster <= r_max; dx_raster++)
                                for (int dy_raster = -r_max; dy_raster <= r_max; dy_raster++)
                                {
                                    //Position auf Raster berechnen
                                    int pixel_x = center_x_raster + dx_raster;
                                    int pixel_y = center_y_raster + dy_raster;

                                    //Auf Zeichenbereich beschränken
                                    if (pixel_x >= minx && pixel_x < maxx &&
                                        pixel_y >= miny && pixel_y < maxy)
                                    {
                                        //Poistion im Alphabuffer berechnen
                                        int i = pixel_y * Width + pixel_x;

                                        //Aktuellen Alpha-Wert im Buffer herholen
                                        byte lastalpha = alphabuffer[i];

                                        //Nicht zeichnen, wenn Alpha bereit auf max
                                        if (lastalpha != 255)
                                        {
                                            //Abstand des Pixels zur Pinselmitte
                                            double dx = pixel_x - center_x;
                                            double dy = pixel_y - center_y;

                                            //Quadratische Abstand zur Pinselmitte (a²+b²=c²)
                                            double distancePOW = dx * dx + dy * dy;

                                            //Nur zeichnen, wenn innheralb Zeichenbereich
                                            if (distancePOW < r_blur_POW)
                                            {
                                                double alpha;

                                                //Inneren Bereich immer voll zeichnen
                                                if (distancePOW < r_full_POW)
                                                {
                                                    alpha = 255;
                                                }
                                                else
                                                {
                                                    //Abstand zur Pinselmitte
                                                    double distance = Math.Sqrt(distancePOW);
                                                    double faktor = 1 - (distance - r_full) / blur;

                                                    if (faktor < 0.0)
                                                        faktor = 0.0;

                                                    //Pixelintensität berechnen
                                                    alpha = alphaf * faktor * corr_factor;
                                                }

                                                //Neues Alpha berechnen
                                                double newalpha = lastalpha + alpha;

                                                //Alpha kann maxima 255 sein
                                                if (newalpha > 255)
                                                    newalpha = 255;

                                                //Zurückschreiben in Buffer
                                                alphabuffer[i] = (byte)(newalpha);
                                            }
                                        }
                                    }
                                }
                        }
                    x0 = x1;
                    y0 = y1;
                }
            }

            /// <summary>
            /// Zeichnet die von GenerateCurve generierte Kurve
            /// </summary>
            /// <param name="alphabuffer"></param>
            /// <param name="color"></param>
            public void RenderCurve(byte[] alphabuffer, Color color)
            {
                //Farbe der Kurve
                UInt32 colour32 = UInt32FromColor(color);

                UInt32 ca = (colour32 & 0xff00ff);
                UInt32 cb = (colour32 & 0x00ff00);

                //Alphawerte zum zeichnen der Kurve benutzen
                Parallel.For(0, alphabuffer.Length, i =>
                {
                    byte alpha = alphabuffer[i];

                    //only draw, if there is something to draw
                    if (alpha == 255)
                        pixelbuffer[i] = colour32;
                    else if (alpha != 0)
                    {
                        UInt32 oldColour = pixelbuffer[i];
                        UInt32 rb = oldColour & 0xff00ff;
                        UInt32 g = oldColour & 0x00ff00;
                        rb += (ca - rb) * alpha >> 8;
                        g += (cb - g) * alpha >> 8;
                        pixelbuffer[i] = (rb & 0xff00ff) | (g & 0xff00);
                    }
                });
            }
        }
    }
}