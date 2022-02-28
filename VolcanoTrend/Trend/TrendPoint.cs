using System;

namespace VolcanoTrend.Trend
{
    /// <summary>
    /// Zeitpunkt mit Wert
    /// </summary>
    public class TrendPoint
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="Ticks"></param>
        /// <param name="Value"></param>
        public TrendPoint(long Ticks, double Value)
        {
            this.TimeStamp = Ticks;
            this.Value = Value;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="Time"></param>
        /// <param name="Value"></param>
        public TrendPoint(DateTime Time, double Value)
        {
            this.TimeStamp = Time.Ticks;
            this.Value = Value;
        }

        /// <summary>
        /// Zeitpunkt des Wertes
        /// </summary>
        public long TimeStamp { get; }

        /// <summary>
        /// Wert zum gegebenen Zeitpunkt
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Berechnet einen 3. Punkt auf einer gedachten Linie zwschen 2 anderen zum angegebenen Zeitpunkt
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns></returns>
        public static TrendPoint Interpolate(TrendPoint p1, TrendPoint p2, DateTime p3)
        {
            double k = (p2.Value - p1.Value) / (p2.TimeStamp - p1.TimeStamp);
            return new TrendPoint(p3.Ticks, p1.Value + (p3.Ticks - p1.TimeStamp) * k);
        }
    }
}