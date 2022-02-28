using System;
using System.Windows;
using System.Windows.Media;

namespace VolcanoTrend
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var curve1 = new Trend.TrendCurve()
            {
                Color = Colors.Red,
                Max = 2,
                Min = -2,
                Thickness = 1
            };

            var StartTime = DateTime.Now;

            for (int i = 0; i < 1000; i++)
            {
                curve1.AddPoint(new Trend.TrendPoint(StartTime.AddMilliseconds(i), Math.Sin(Math.PI * 2 * 5 * i / 1000)));
            }

            trendView.AddCurve(curve1);
            trendView.StartX = StartTime;
            trendView.EndX = StartTime.AddSeconds(1);

            trendView.REFRESH_LVL2();
        }
    }
}