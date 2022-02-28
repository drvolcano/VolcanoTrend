using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace VolcanoTrend.Trend
{
    public class TrendCurve
    {
        public enum IndexSide
        {
            /// <summary>
            /// Nähester Index (darüber oder darunter)
            /// </summary>
            nearest,

            /// <summary>
            /// Index muss unterhalb liegen
            /// </summary>
            lower,

            /// <summary>
            /// Index muss überhalb liegen
            /// </summary>
            upper,
        }

        /// <summary>
        /// Gibt den Index des Punktes zurück, der am nächsten am angegebenen Zeitpunkt liegt
        /// </summary>
        /// <param name="timestamp"></param>
        /// <param name="side"></param>
        /// <returns></returns>
        public int GetIndexAt(DateTime timestamp, IndexSide side)
        {
            return GetIndexAt(timestamp.Ticks, side);
        }

        /// <summary>
        /// Gibt den Index des Punktes zurück, der am nächsten am angegebenen Zeitpunkt liegt
        /// </summary>
        /// <param name="timestamp"></param>
        /// <param name="side"></param>
        /// <returns></returns>
        public int GetIndexAt(long timestamp, IndexSide side)
        {
            //upper und lower auf die weitest möglichen Werte setzen
            int upper = Points.Count - 1;
            int lower = 0;

            //Solange upper und lower mehr als 1 auseinander liegen:
            while (upper - lower > 1)
            {
                int middle = (upper + lower) / 2;

                //Den Index-Bereich halbieren, auf den Bereich in dem der Zeitstempel liegt
                if (Points[middle].TimeStamp > timestamp)
                    upper = middle;
                else
                    lower = middle;
            }

            switch (side)
            {
                case IndexSide.lower: return lower;
                case IndexSide.upper: return upper;
                case IndexSide.nearest: return (lower < upper) ? lower : upper;
            }

            return -1;
        }

        /// <summary>
        /// Gibt den Kurvenwert am angegebenen Zeitstempel zurück
        /// </summary>
        /// <param name="Location"></param>
        /// <param name="interpolate"></param>
        /// <returns></returns>
        public double GetValueAt(DateTime Location, bool interpolate)
        {
            if (Points.Count == 0)
                return double.NaN;

            int upper = Points.Count - 1;
            int lower = 0;

            //Solange upper und lower mehr als 1 auseinander liegen:
            while (upper - lower > 1)
            {
                int middle = (upper + lower) / 2;

                //Den Index-Bereich halbieren, auf den Bereich in dem der Zeitstempel liegt
                if (Points[middle].TimeStamp > Location.Ticks)
                    upper = middle;
                else
                    lower = middle;
            }

            //nächstgelegenen Punkt zurückgeben einen Wert zwischen 2 Punkten interpolieren
            if (interpolate)
                return TrendPoint.Interpolate(Points[lower], Points[upper], Location).Value;
            else
            {
                long Dbelow = Location.Ticks - Points[lower].TimeStamp;
                long Dabove = -Location.Ticks + Points[upper].TimeStamp;

                //näheren Wert zurückgeben
                if (Dbelow < Dabove)
                    return Points[lower].Value;
                else
                    return Points[upper].Value;
            }
        }

        public double GetMiddleIn(DateTime Location1, DateTime Location2)
        {
            DateTime higher = Location1;
            DateTime lower = Location2;

            if (lower > higher)
            {
                higher = Location2;
                lower = Location1;
            }

            int idx_high = GetIndexAt(higher, IndexSide.nearest);
            int idx_low = GetIndexAt(lower, IndexSide.nearest);

            double count = 0;
            double sum = 0;

            for (int i = idx_low; i <= idx_high; i++)
            {
                count++;
                sum += Points[i].Value;
            }

            return sum / count;
        }

        /// <summary>
        /// Maximal dargestellter Wert
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// Minimal dargestellter Wert
        /// </summary>
        public double Min { get; set; }

        private double _Thickness = 2.0;

        public double Thickness
        { get { return _Thickness; } set { _Thickness = value; PropertyChanged?.Invoke(this, this); } }

        private bool _Visible = true;

        public bool Visible
        { get { return _Visible; } set { _Visible = value; PropertyChanged?.Invoke(this, this); } }

        private Color _Color = Colors.Blue;

        public Color Color
        { get { return _Color; } set { _Color = value; PropertyChanged?.Invoke(this, this); } }

        public List<TrendPoint> Points = new List<TrendPoint>();

        public void AddPoint(TrendPoint p)
        {
            Points.Add(p);
        }

        public event CurveEventArgs PropertyChanged;

        public delegate void CurveEventArgs(object sender, TrendCurve c);
    }
}