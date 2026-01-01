
using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators
{
    /// <summary>
    /// Hydrodynamic Correction Detector - Higuchi Fractal Dimension
    /// Version: 2.2 (Fixed for cTrader compatibility)
    /// </summary>
    [Indicator(IsOverlay = false, AutoRescale = true, ScalePrecision = 3, AccessRights = AccessRights.None, TimeZone = TimeZones.UTC)]
    public class HydrodynamicCorrectionDetector : Indicator
    {
        [Parameter("Window Size (N)", DefaultValue = 50, MinValue = 30, Group = "Higuchi Settings")]
        public int WindowSize { get; set; }

        [Parameter("Max K", DefaultValue = 8, MinValue = 2, MaxValue = 20, Group = "Higuchi Settings")]
        public int MaxK { get; set; }

        [Parameter("Trigger Level", DefaultValue = 1.5, MinValue = 1.0, MaxValue = 2.0, Group = "Visual Settings")]
        public double TriggerLevelValue { get; set; }

        [Output("FD > 1.6 (Chaos)", PlotType = PlotType.Histogram, LineColor = "Red", Thickness = 4)]
        public IndicatorDataSeries HistogramHigh { get; set; }

        [Output("1.5 < FD ≤ 1.6 (Warning)", PlotType = PlotType.Histogram, LineColor = "Gold", Thickness = 4)]
        public IndicatorDataSeries HistogramMedium { get; set; }

        [Output("FD ≤ 1.5 (Trend)", PlotType = PlotType.Histogram, LineColor = "SkyBlue", Thickness = 4)]
        public IndicatorDataSeries HistogramLow { get; set; }

        [Output("Trigger Level", LineColor = "Red", Thickness = 2, PlotType = PlotType.Line)]
        public IndicatorDataSeries TriggerLevel { get; set; }

        [Output("Raw FD", PlotType = PlotType.Line, LineColor = "Transparent")]
        public IndicatorDataSeries FDSeries { get; set; }

        private double[] priceBuffer;
        private readonly List<double> xValues = new List<double>();
        private readonly List<double> yValues = new List<double>();

        protected override void Initialize()
        {
            priceBuffer = new double[WindowSize];
            Print($"[CorrectionDetector] Initialized - Window: {WindowSize}, MaxK: {MaxK}");
        }

        public override void Calculate(int index)
        {
            if (index < 0)
                return;

            TriggerLevel[index] = TriggerLevelValue;

            if (index < WindowSize - 1)
            {
                HistogramHigh[index] = double.NaN;
                HistogramMedium[index] = double.NaN;
                HistogramLow[index] = double.NaN;
                FDSeries[index] = 1.0;
                return;
            }

            try
            {
                // آماده‌سازی بافر قیمت
                int startIdx = index - WindowSize + 1;
                for (int i = 0; i < WindowSize; i++)
                {
                    int targetIdx = startIdx + i;
                    if (targetIdx < 0)
                    {
                        FDSeries[index] = 1.0;
                        SetHistograms(index, 1.0);
                        return;
                    }
                    priceBuffer[i] = Bars.ClosePrices[targetIdx];
                }

                // محاسبه Fractal Dimension
                double fd = CalculateHiguchiFD();
                FDSeries[index] = fd;

                // تنظیم هیستوگرام‌ها
                SetHistograms(index, fd);
            }
            catch
            {
                FDSeries[index] = 1.0;
                SetHistograms(index, 1.0);
            }
        }

        private double CalculateHiguchiFD()
        {
            xValues.Clear();
            yValues.Clear();

            // ═══════════════════════════════════════════════════════
            // Higuchi Fractal Dimension Algorithm
            // ═══════════════════════════════════════════════════════
            for (int k = 1; k <= MaxK; k++)
            {
                double lengthSum = 0.0;

                for (int m = 0; m < k; m++)
                {
                    double Lm = 0.0;
                    int pointsInSubset = (WindowSize - m - 1) / k;

                    if (pointsInSubset < 1)
                        continue;

                    for (int i = 1; i <= pointsInSubset; i++)
                    {
                        int currentIdx = m + i * k;
                        int previousIdx = m + (i - 1) * k;
                        
                        if (currentIdx >= WindowSize || previousIdx >= WindowSize)
                            continue;
                            
                        double difference = Math.Abs(priceBuffer[currentIdx] - priceBuffer[previousIdx]);
                        Lm += difference;
                    }

                    double normFactor = (WindowSize - 1.0) / (pointsInSubset * k * 1.0);
                    Lm = Lm * normFactor / k;
                    lengthSum += Lm;
                }

                double avgLk = lengthSum / k;
                if (avgLk <= 0)
                    avgLk = 1e-10;

                xValues.Add(Math.Log(1.0 / k));
                yValues.Add(Math.Log(avgLk));
            }

            // رگرسیون خطی برای محاسبه شیب (FD)
            if (xValues.Count < 2)
                return 1.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = xValues.Count;

            for (int i = 0; i < n; i++)
            {
                sumX += xValues[i];
                sumY += yValues[i];
                sumXY += xValues[i] * yValues[i];
                sumX2 += xValues[i] * xValues[i];
            }

            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
                return 1.0;

            double fd = (n * sumXY - sumX * sumY) / denominator;
            return Math.Max(1.0, Math.Min(2.0, fd));
        }

        private void SetHistograms(int index, double fd)
        {
            HistogramHigh[index] = double.NaN;
            HistogramMedium[index] = double.NaN;
            HistogramLow[index] = double.NaN;

            if (fd > 1.6)
                HistogramHigh[index] = fd;
            else if (fd > 1.5)
                HistogramMedium[index] = fd;
            else
                HistogramLow[index] = fd;
        }
    }
}using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators
{
    /// <summary>
    /// Hydrodynamic Correction Detector - Higuchi Fractal Dimension
    /// Version: 2.2 (Fixed for cTrader compatibility)
    /// </summary>
    [Indicator(IsOverlay = false, AutoRescale = true, ScalePrecision = 3, AccessRights = AccessRights.None, TimeZone = TimeZones.UTC)]
    public class HydrodynamicCorrectionDetector : Indicator
    {
        [Parameter("Window Size (N)", DefaultValue = 50, MinValue = 30, Group = "Higuchi Settings")]
        public int WindowSize { get; set; }

        [Parameter("Max K", DefaultValue = 8, MinValue = 2, MaxValue = 20, Group = "Higuchi Settings")]
        public int MaxK { get; set; }

        [Parameter("Trigger Level", DefaultValue = 1.5, MinValue = 1.0, MaxValue = 2.0, Group = "Visual Settings")]
        public double TriggerLevelValue { get; set; }

        [Output("FD > 1.6 (Chaos)", PlotType = PlotType.Histogram, LineColor = "Red", Thickness = 4)]
        public IndicatorDataSeries HistogramHigh { get; set; }

        [Output("1.5 < FD ≤ 1.6 (Warning)", PlotType = PlotType.Histogram, LineColor = "Gold", Thickness = 4)]
        public IndicatorDataSeries HistogramMedium { get; set; }

        [Output("FD ≤ 1.5 (Trend)", PlotType = PlotType.Histogram, LineColor = "SkyBlue", Thickness = 4)]
        public IndicatorDataSeries HistogramLow { get; set; }

        [Output("Trigger Level", LineColor = "Red", Thickness = 2, PlotType = PlotType.Line)]
        public IndicatorDataSeries TriggerLevel { get; set; }

        [Output("Raw FD", PlotType = PlotType.Line, LineColor = "Transparent")]
        public IndicatorDataSeries FDSeries { get; set; }

        private double[] priceBuffer;
        private readonly List<double> xValues = new List<double>();
        private readonly List<double> yValues = new List<double>();

        protected override void Initialize()
        {
            priceBuffer = new double[WindowSize];
            Print($"[CorrectionDetector] Initialized - Window: {WindowSize}, MaxK: {MaxK}");
        }

        public override void Calculate(int index)
        {
            if (index < 0)
                return;

            TriggerLevel[index] = TriggerLevelValue;

            if (index < WindowSize - 1)
            {
                HistogramHigh[index] = double.NaN;
                HistogramMedium[index] = double.NaN;
                HistogramLow[index] = double.NaN;
                FDSeries[index] = 1.0;
                return;
            }

            try
            {
                // آماده‌سازی بافر قیمت
                int startIdx = index - WindowSize + 1;
                for (int i = 0; i < WindowSize; i++)
                {
                    int targetIdx = startIdx + i;
                    if (targetIdx < 0)
                    {
                        FDSeries[index] = 1.0;
                        SetHistograms(index, 1.0);
                        return;
                    }
                    priceBuffer[i] = Bars.ClosePrices[targetIdx];
                }

                // محاسبه Fractal Dimension
                double fd = CalculateHiguchiFD();
                FDSeries[index] = fd;

                // تنظیم هیستوگرام‌ها
                SetHistograms(index, fd);
            }
            catch
            {
                FDSeries[index] = 1.0;
                SetHistograms(index, 1.0);
            }
        }

        private double CalculateHiguchiFD()
        {
            xValues.Clear();
            yValues.Clear();

            // ═══════════════════════════════════════════════════════
            // Higuchi Fractal Dimension Algorithm
            // ═══════════════════════════════════════════════════════
            for (int k = 1; k <= MaxK; k++)
            {
                double lengthSum = 0.0;

                for (int m = 0; m < k; m++)
                {
                    double Lm = 0.0;
                    int pointsInSubset = (WindowSize - m - 1) / k;

                    if (pointsInSubset < 1)
                        continue;

                    for (int i = 1; i <= pointsInSubset; i++)
                    {
                        int currentIdx = m + i * k;
                        int previousIdx = m + (i - 1) * k;
                        
                        if (currentIdx >= WindowSize || previousIdx >= WindowSize)
                            continue;
                            
                        double difference = Math.Abs(priceBuffer[currentIdx] - priceBuffer[previousIdx]);
                        Lm += difference;
                    }

                    double normFactor = (WindowSize - 1.0) / (pointsInSubset * k * 1.0);
                    Lm = Lm * normFactor / k;
                    lengthSum += Lm;
                }

                double avgLk = lengthSum / k;
                if (avgLk <= 0)
                    avgLk = 1e-10;

                xValues.Add(Math.Log(1.0 / k));
                yValues.Add(Math.Log(avgLk));
            }

            // رگرسیون خطی برای محاسبه شیب (FD)
            if (xValues.Count < 2)
                return 1.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = xValues.Count;

            for (int i = 0; i < n; i++)
            {
                sumX += xValues[i];
                sumY += yValues[i];
                sumXY += xValues[i] * yValues[i];
                sumX2 += xValues[i] * xValues[i];
            }

            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
                return 1.0;

            double fd = (n * sumXY - sumX * sumY) / denominator;
            return Math.Max(1.0, Math.Min(2.0, fd));
        }

        private void SetHistograms(int index, double fd)
        {
            HistogramHigh[index] = double.NaN;
            HistogramMedium[index] = double.NaN;
            HistogramLow[index] = double.NaN;

            if (fd > 1.6)
                HistogramHigh[index] = fd;
            else if (fd > 1.5)
                HistogramMedium[index] = fd;
            else
                HistogramLow[index] = fd;
        }
    }
}
