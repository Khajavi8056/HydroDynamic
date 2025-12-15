// ══════════════════════════════════════════════════════════════════════════════
// 🤖 ربات معاملاتی HydroDynamic - نسخه 8.2 نهایی اصلاح شده
// ══════════════════════════════════════════════════════════════════════════════
// پلتفرم: cTrader
// Timeframe: Pip Range Bars (توصیه: 10 pips)
// زبان: C# با کامنت‌گذاری کامل فارسی
//
// ✅ تمام باگ‌ها برطرف شده
// ✅ Hurst Exponent اصلاح شده (Multi-Scale R/S Analysis)
// ✅ Toxicity Monitor اضافه شده (با Median)
// ✅ P_zero Tracking بهبود یافته (Window-based)
// ✅ TIP Normalization (Z-Score based)
// ✅ آماده برای Compile و اجرا
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// ربات معاملاتی Hydrodynamic
    /// استراتژی 5 مرحله‌ای برای ورود به معامله:
    /// 1. تشخیص Trend (SuperSmoother + Hurst Exponent)
    /// 2. تشخیص Correction (Fractal Dimension)
    /// 3. شناسایی P_zero (نقطه بازگشت)
    /// 4. تأیید Timing (TIP - Tick Imbalance Pressure)
    /// 5. بررسی سلامت بازار (Toxicity Monitor)
    ///
    /// خروج با 5 سطح دفاعی:
    /// 1. Hard Stop Loss
    /// 2. Target Management (TP1 & TP2)
    /// 3. Trailing Stop
    /// 4. Trend Reversal Exit
    /// 5. Time Stops
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HydroDynamicTradingBot : Robot
    {
        // ════════════════════════════════════════════════════════════════════════
        // پارامترهای ورودی - قابل تنظیم توسط کاربر
        // ════════════════════════════════════════════════════════════════════════
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 1: تنظیمات Trend (تشخیص روند)
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("Smooth Length", DefaultValue = 10, MinValue = 5, Group = "Trend")]
        public int SmoothLength { get; set; }
      
        [Parameter("Hurst Period", DefaultValue = 100, MinValue = 30, Group = "Trend")]
        public int HurstPeriod { get; set; }
      
        [Parameter("Hurst Threshold", DefaultValue = 0.55, MinValue = 0.5, MaxValue = 0.7, Group = "Trend")]
        public double HurstThreshold { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 2: تنظیمات Fractal Dimension (تشخیص Correction)
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("Window Size", DefaultValue = 50, MinValue = 30, Group = "FD")]
        public int WindowSize { get; set; }
      
        [Parameter("Max K", DefaultValue = 8, MinValue = 2, MaxValue = 20, Group = "FD")]
        public int MaxK { get; set; }
      
        [Parameter("FD Chaos Start", DefaultValue = 1.65, MinValue = 1.0, MaxValue = 2.0, Group = "FD")]
        public double FDChaosThreshold { get; set; }
      
        [Parameter("FD Stable Exit", DefaultValue = 1.45, MinValue = 1.0, MaxValue = 2.0, Group = "FD")]
        public double FDStableThreshold { get; set; }
      
        [Parameter("P_zero Lookback", DefaultValue = 20, MinValue = 10, MaxValue = 50, Group = "FD")]
        public int PZeroLookback { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 3: تنظیمات TIP (سیگنال ورود)
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("TIP Z-Score Threshold", DefaultValue = 2.0, MinValue = 1.0, MaxValue = 3.5, Group = "TIP")]
        public double TIPZScoreThreshold { get; set; }
      
        [Parameter("TIP Lookback", DefaultValue = 5, MinValue = 3, MaxValue = 10, Group = "TIP")]
        public int TIPLookbackBars { get; set; }
      
        [Parameter("TIP History Size", DefaultValue = 100, MinValue = 50, MaxValue = 200, Group = "TIP")]
        public int TIPHistorySize { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 4: تنظیمات Toxicity (سلامت بازار)
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("Spread History", DefaultValue = 50, MinValue = 30, MaxValue = 100, Group = "Toxicity")]
        public int SpreadHistorySize { get; set; }
      
        [Parameter("Toxicity Threshold", DefaultValue = 2.5, MinValue = 1.5, MaxValue = 5.0, Group = "Toxicity")]
        public double ToxicityThreshold { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 5: مدیریت ریسک
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("Risk Percent", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Group = "Risk")]
        public double RiskPercent { get; set; }
      
        [Parameter("Max Positions", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "Risk")]
        public int MaxPositions { get; set; }
      
        [Parameter("Stop Buffer Pips", DefaultValue = 5, MinValue = 0, MaxValue = 20, Group = "Risk")]
        public double StopLossBuffer { get; set; }
      
        [Parameter("ATR Period", DefaultValue = 14, MinValue = 5, MaxValue = 50, Group = "Risk")]
        public int ATRPeriod { get; set; }
      
        [Parameter("Use Dynamic SL", DefaultValue = true, Group = "Risk")]
        public bool UseDynamicStopLoss { get; set; }
      
        [Parameter("Base SL Multiplier", DefaultValue = 2.0, MinValue = 1.0, MaxValue = 5.0, Group = "Risk")]
        public double BaseStopMultiplier { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 6: استراتژی خروج
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("TP1 Close %", DefaultValue = 50, MinValue = 0, MaxValue = 100, Group = "Exit")]
        public double TP1Percent { get; set; }
      
        [Parameter("Ballistic Multiplier", DefaultValue = 1.618, MinValue = 1.0, MaxValue = 3.0, Group = "Exit")]
        public double BallisticMultiplier { get; set; }
      
        [Parameter("Trailing ATR x", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 3.0, Group = "Exit")]
        public double TrailingATRMultiple { get; set; }
      
        [Parameter("Enable Time Stops", DefaultValue = true, Group = "Exit")]
        public bool EnableTimeStops { get; set; }
      
        [Parameter("Time Stop 1", DefaultValue = 30, MinValue = 10, MaxValue = 100, Group = "Exit")]
        public int TimeStop1Bars { get; set; }
      
        [Parameter("Time Stop 2", DefaultValue = 50, MinValue = 20, MaxValue = 150, Group = "Exit")]
        public int TimeStop2Bars { get; set; }
      
        [Parameter("Trend Reversal Exit", DefaultValue = true, Group = "Exit")]
        public bool EnableTrendReversalExit { get; set; }
      
        // ──────────────────────────────────────────────────────────────────────
        // گروه 7: کنترل و نمایش
        // ──────────────────────────────────────────────────────────────────────
      
        [Parameter("Trading Enabled", DefaultValue = false, Group = "Control")]
        public bool TradingEnabled { get; set; }
      
        [Parameter("Magic Number", DefaultValue = 123456, Group = "Control")]
        public int MagicNumber { get; set; }
      
        [Parameter("Log Level", DefaultValue = LogLevel.INFO, Group = "Display")]
        public LogLevel LoggingLevel { get; set; }
      
        [Parameter("Show Stats", DefaultValue = true, Group = "Display")]
        public bool ShowPerformanceStats { get; set; }
      
        // ════════════════════════════════════════════════════════════════════════
        // متغیرهای داخلی - سری‌های داده
        // ════════════════════════════════════════════════════════════════════════
      
        // سری‌های اندیکاتور
        private IndicatorDataSeries _smoothed; // قیمت Smooth شده
        private IndicatorDataSeries _trendState; // وضعیت Trend: +1/-1/0
        private IndicatorDataSeries _slope; // شیب خط Smooth
        private IndicatorDataSeries _hurst; // Hurst Exponent
        private IndicatorDataSeries _fractalDimension; // Fractal Dimension
      
        // ضرایب SuperSmoother
        private double _c1, _c2, _c3;
      
        // بافر برای محاسبات
        private double[] _priceBuffer;
      
        // متغیرهای P_zero
        private double _pZero; // نقطه بازگشت
        private bool _inCorrection; // آیا در Correction هستیم؟
        private double _lastHigh; // بالاترین قیمت اخیر
        private double _lastLow; // پایین‌ترین قیمت اخیر
        private bool _pZeroValid; // آیا P_zero معتبر است؟
      
        // متغیرهای TIP
        private int _buyTicks; // تعداد tick های خرید
        private int _sellTicks; // تعداد tick های فروش
        private double _lastAsk; // آخرین Ask
        private double _currentTIP; // TIP خام
        private Queue<double> _tipHistory; // تاریخچه برای Normalize
        private double _normalizedTIP; // TIP نرمال شده (Z-Score)
      
        // متغیرهای Toxicity
        private Queue<double> _spreadHistory; // تاریخچه Spread
        private double _effectiveSpread; // Effective Spread
        private double _toxicityScore; // امتیاز Toxicity
        private bool _marketSafe; // آیا بازار امن است؟
      
        // مدیریت معاملات
        private Dictionary<int, TradeContext> _activeTrades;
        private readonly object _tradesLock = new object();
      
        // اندیکاتور ATR
        private AverageTrueRange _atr;
      
        // سیستم لاگ
        private Logger _logger;
        private PerformanceMonitor _perfMonitor;
      
        // ════════════════════════════════════════════════════════════════════════
        // ENUM: سطوح لاگ
        // ════════════════════════════════════════════════════════════════════════
        public enum LogLevel
        {
            DEBUG = 0, // تمام جزئیات
            INFO = 1, // اطلاعات مهم
            WARNING = 2, // هشدارها
            ERROR = 3, // فقط خطاها
            NONE = 4 // هیچ چیز
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CLASS: Logger - سیستم لاگ حرفه‌ای
        // ════════════════════════════════════════════════════════════════════════
        private class Logger
        {
            private readonly Robot _robot;
            private readonly LogLevel _level;
            private readonly StringBuilder _buffer;
            private int _logCount;
          
            public Logger(Robot robot, LogLevel level)
            {
                _robot = robot;
                _level = level;
                _buffer = new StringBuilder();
                _logCount = 0;
            }
          
            public void Debug(string message)
            {
                if (_level <= LogLevel.DEBUG)
                    Log("DEBUG", message, "🔍");
            }
          
            public void Info(string message)
            {
                if (_level <= LogLevel.INFO)
                    Log("INFO", message, "ℹ️");
            }
          
            public void Warning(string message)
            {
                if (_level <= LogLevel.WARNING)
                    Log("WARN", message, "⚠️");
            }
          
            public void Error(string message, Exception ex = null)
            {
                if (_level <= LogLevel.ERROR)
                {
                    string full = message;
                    if (ex != null)
                        full += $"\nException: {ex.Message}";
                    Log("ERROR", full, "❌");
                }
            }
          
            private void Log(string level, string message, string icon)
            {
                _logCount++;
                string time = _robot.Server.Time.ToString("HH:mm:ss");
                string formatted = $"[{time}] {icon} {level}: {message}";
                _robot.Print(formatted);
                _buffer.AppendLine(formatted);
              
                if (_logCount % 1000 == 0)
                    _buffer.Clear();
            }
          
            public string GetSummary()
            {
                return $"Total logs: {_logCount}";
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CLASS: Performance Monitor - ردیابی عملکرد
        // ════════════════════════════════════════════════════════════════════════
        private class PerformanceMonitor
        {
            private DateTime _startTime;
            private int _totalTrades;
            private int _winningTrades;
            private int _losingTrades;
            private double _totalProfit;
            private double _totalLoss;
            private double _largestWin;
            private double _largestLoss;
          
            public void Start()
            {
                _startTime = DateTime.UtcNow;
                _totalTrades = 0;
                _winningTrades = 0;
                _losingTrades = 0;
                _totalProfit = 0;
                _totalLoss = 0;
                _largestWin = 0;
                _largestLoss = 0;
            }
          
            public void RecordTrade(double pnl)
            {
                _totalTrades++;
              
                if (pnl > 0)
                {
                    _winningTrades++;
                    _totalProfit += pnl;
                    if (pnl > _largestWin)
                        _largestWin = pnl;
                }
                else
                {
                    _losingTrades++;
                    _totalLoss += Math.Abs(pnl);
                    if (Math.Abs(pnl) > _largestLoss)
                        _largestLoss = Math.Abs(pnl);
                }
            }
          
            public string GetReport()
            {
                var sb = new StringBuilder();
                var runtime = DateTime.UtcNow - _startTime;
              
                sb.AppendLine("════════════════════════════════════════");
                sb.AppendLine("📊 PERFORMANCE SUMMARY");
                sb.AppendLine("════════════════════════════════════════");
                sb.AppendLine($"Runtime: {runtime.Days}d {runtime.Hours}h {runtime.Minutes}m");
                sb.AppendLine($"Total Trades: {_totalTrades}");
                sb.AppendLine($"Winning: {_winningTrades} ({(_totalTrades > 0 ? (double)_winningTrades / _totalTrades * 100 : 0):F1}%)");
                sb.AppendLine($"Losing: {_losingTrades} ({(_totalTrades > 0 ? (double)_losingTrades / _totalTrades * 100 : 0):F1}%)");
                sb.AppendLine($"Total Profit: ${_totalProfit:F2}");
                sb.AppendLine($"Total Loss: ${_totalLoss:F2}");
                sb.AppendLine($"Net P&L: ${(_totalProfit - _totalLoss):F2}");
                sb.AppendLine($"Largest Win: ${_largestWin:F2}");
                sb.AppendLine($"Largest Loss: ${_largestLoss:F2}");
              
                if (_totalTrades > 0)
                {
                    double avgWin = _winningTrades > 0 ? _totalProfit / _winningTrades : 0;
                    double avgLoss = _losingTrades > 0 ? _totalLoss / _losingTrades : 0;
                    double profitFactor = _totalLoss > 0 ? _totalProfit / _totalLoss : 0;
                  
                    sb.AppendLine($"Avg Win: ${avgWin:F2}");
                    sb.AppendLine($"Avg Loss: ${avgLoss:F2}");
                    sb.AppendLine($"Profit Factor: {profitFactor:F2}");
                }
              
                sb.AppendLine("════════════════════════════════════════");
              
                return sb.ToString();
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CLASS: Trade Context - اطلاعات هر معامله
        // ════════════════════════════════════════════════════════════════════════
        private class TradeContext
        {
            public int PositionId { get; set; }
            public double EntryPrice { get; set; }
            public double PZero { get; set; }
            public double Stretch { get; set; }
            public double TP1 { get; set; }
            public double TP2 { get; set; }
            public bool TP1Hit { get; set; }
            public bool TP2Hit { get; set; }
            public bool TrailingActive { get; set; }
            public int EntryBarIndex { get; set; }
            public int EntryTrendState { get; set; }
            public TradeType Direction { get; set; }
            public DateTime EntryTime { get; set; }
          
            public override string ToString()
            {
                return $"Pos {PositionId}: {Direction} @ {EntryPrice:F5}, P_zero={PZero:F5}";
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // ON START - مقداردهی اولیه ربات
        // ════════════════════════════════════════════════════════════════════════
                protected override void OnStart()
        {
            try
            {
                // راه‌اندازی سیستم لاگ
                _logger = new Logger(this, LoggingLevel);
                _perfMonitor = new PerformanceMonitor();
                _perfMonitor.Start();
              
                _logger.Info("════════════════════════════════════════");
                _logger.Info(" HydroDynamic Bot v8.2 - FINAL");
                _logger.Info("════════════════════════════════════════");
                _logger.Info($" Symbol: {SymbolName}");
                _logger.Info($" Timeframe: {TimeFrame}");
                _logger.Info($" Trading: {(TradingEnabled ? "ON ✅" : "OFF ⚠️")}");
                _logger.Info("════════════════════════════════════════");
              
                // ایجاد سری‌های داده
                _smoothed = CreateDataSeries();
                _trendState = CreateDataSeries();
                _slope = CreateDataSeries();
                _hurst = CreateDataSeries();
                _fractalDimension = CreateDataSeries();
              
                // محاسبه ضرایب SuperSmoother
                double arg = 1.414 * Math.PI / SmoothLength;
                double a1 = Math.Exp(-arg);
                double b1 = 2.0 * a1 * Math.Cos(arg);
              
                _c2 = b1;
                _c3 = -a1 * a1;
                _c1 = (1.0 - _c2 - _c3) / 2.0;
              
                // مقداردهی متغیرها
                _priceBuffer = new double[WindowSize];
                _pZero = 0;
                _inCorrection = false;
                _pZeroValid = false;
                _lastHigh = 0;
                _lastLow = double.MaxValue;
              
                _buyTicks = 0;
                _sellTicks = 0;
                _lastAsk = Symbol.Ask;
                _currentTIP = 0;
                _normalizedTIP = 0;
                _tipHistory = new Queue<double>();
              
                _spreadHistory = new Queue<double>();
                _effectiveSpread = 0;
                _toxicityScore = 0;
                _marketSafe = true;
              
                _activeTrades = new Dictionary<int, TradeContext>();
                _atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.Simple);
              
                // محاسبه داده‌های تاریخی
                _logger.Info($"⏳ Calculating {Bars.Count} bars...");
              
                // ✅ درست: فقط ۵۰۰ تا کندل آخر رو حساب کن
                int lookback = 500; 
                int startIndex = Math.Max(0, Bars.Count - lookback);
                     
                _logger.Info($"⏳ Warming up indicators (Last {lookback} bars)...");
                     
                for (int i = startIndex; i < Bars.Count; i++)
                {
                    CalculateTrend(i);
                    CalculateFD(i);
                    UpdatePZero(i);
                }
                // 🗑️ اون خطوط لاگ مزاحم اینجا حذف شدن
           
                _logger.Info($"✅ Ready - Analysis Complete");
              
                // ثبت event
                Positions.Closed += OnPositionClosed;
              
                _logger.Info("🚀 Bot started successfully!");
            }
            catch (Exception ex)
            {
                Print($"❌ CRITICAL: {ex.Message}");
                Stop();
            }
        }

      
        // ════════════════════════════════════════════════════════════════════════
        // ON TICK - پردازش هر تغییر قیمت
        // ════════════════════════════════════════════════════════════════════════
        protected override void OnTick()
        {
            try
            {
                // شمارش Buy/Sell Ticks برای TIP
                double currentAsk = Symbol.Ask;
              
                if (currentAsk > _lastAsk)
                    _buyTicks++;
                else if (currentAsk < _lastAsk)
                    _sellTicks++;
              
                _lastAsk = currentAsk;
              
                // مدیریت معاملات باز
                if (TradingEnabled && _activeTrades.Count > 0)
                {
                    ManagePositions();
                }
              
                // به‌روزرسانی Toxicity Monitor
                UpdateToxicity();
            }
            catch (Exception ex)
            {
                _logger.Error("Error in OnTick", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // ON BAR - پردازش بستن هر bar
        // ════════════════════════════════════════════════════════════════════════
        protected override void OnBar()
        {
            try
            {
                int index = Bars.Count - 1;
              
                // محاسبه اندیکاتورها
                CalculateTrend(index);
                CalculateFD(index);
                UpdatePZero(index);
                CalculateTIP();
              
                // بررسی شرایط ورود
                if (TradingEnabled)
                {
                    CheckEntry(index);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in OnBar", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CALCULATE TREND - محاسبه روند بازار
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// محاسبه Trend با SuperSmoother + Hurst Exponent
        ///
        /// مراحل:
        /// 1. SuperSmoother Filter → حذف نویز
        /// 2. محاسبه Slope → جهت حرکت
        /// 3. Hurst Exponent → تأیید Persistence
        /// 4. تعیین TrendState: +1 (صعودی), -1 (نزولی), 0 (خنثی)
        /// </summary>
        private void CalculateTrend(int index)
        {
            if (index < 2)
            {
                _smoothed[index] = Bars.ClosePrices[index];
                _slope[index] = 0;
                _hurst[index] = 0.5;
                _trendState[index] = 0;
                return;
            }
          
            try
            {
                // ─────────────────────────────────────────────────────
                // 1. SuperSmoother Filter
                // ─────────────────────────────────────────────────────
                // فرمول بازگشتی:
                // Filt[i] = c1×(Price[i]+Price[i-1])/2 + c2×Filt[i-1] + c3×Filt[i-2]
              
                double priceAvg = (Bars.ClosePrices[index] + Bars.ClosePrices[index - 1]) / 2.0;
              
                _smoothed[index] = _c1 * priceAvg
                                 + _c2 * _smoothed[index - 1]
                                 + _c3 * _smoothed[index - 2];
              
                // ─────────────────────────────────────────────────────
                // 2. محاسبه Slope (شیب)
                // ─────────────────────────────────────────────────────
                _slope[index] = _smoothed[index] - _smoothed[index - 1];
              
                // ─────────────────────────────────────────────────────
                // 3. محاسبه Hurst Exponent (CORRECTED)
                // ─────────────────────────────────────────────────────
                _hurst[index] = CalculateHurst(index);
              
                // ─────────────────────────────────────────────────────
                // 4. تعیین وضعیت Trend
                // ─────────────────────────────────────────────────────
                // شرط Trend صعودی: Slope > 0 AND Hurst > Threshold
                if (_slope[index] > 0 && _hurst[index] > HurstThreshold)
                {
                    _trendState[index] = 1; // صعودی
                }
                // شرط Trend نزولی: Slope < 0 AND Hurst > Threshold
                else if (_slope[index] < 0 && _hurst[index] > HurstThreshold)
                {
                    _trendState[index] = -1; // نزولی
                }
                else
                {
                    _trendState[index] = 0; // خنثی
                }
              
                // لاگ (فقط بار آخر)
                if (index == Bars.Count - 1)
                {
                    _logger.Debug($"Trend: State={_trendState[index]:F0}, Hurst={_hurst[index]:F3}, Slope={_slope[index]:F5}");
                }
            }
            catch (Exception ex)
            {
                _trendState[index] = 0;
                _logger.Error($"Error in CalculateTrend at {index}", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CALCULATE HURST - محاسبه Hurst Exponent با R/S Analysis
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// محاسبه Hurst Exponent با روش Rescaled Range (R/S) Analysis
        ///
        /// ✅ CORRECTED: Multi-Scale Regression (نه تک scale)
        ///
        /// الگوریتم:
        /// 1. برای هر time scale (tau = 5, 10, 20, 40):
        /// - تقسیم returns به subset های طول tau
        /// - محاسبه R/S برای هر subset
        /// - میانگین R/S
        /// 2. Linear regression: log(R/S) = H × log(tau) + constant
        /// 3. Slope = Hurst Exponent
        ///
        /// تفسیر:
        /// H < 0.5 → Mean-reverting (برگشت به میانگین)
        /// H = 0.5 → Random walk (تصادفی)
        /// H > 0.5 → Trending (روندار)
        /// H > 0.55 → Strong trend (روند قوی - مناسب معامله)
        ///
        /// منبع: Hurst, H.E. (1951) - Long-term storage capacity
        /// </summary>
        private double CalculateHurst(int index)
        {
            if (index < HurstPeriod)
                return 0.5; // Random walk
          
            try
            {
                // ─────────────────────────────────────────────────────
                // 1. استخراج قیمت‌ها
                // ─────────────────────────────────────────────────────
                double[] prices = new double[HurstPeriod];
                for (int i = 0; i < HurstPeriod; i++)
                {
                    int idx = index - HurstPeriod + 1 + i;
                    prices[i] = Bars.ClosePrices[idx];
                }
              
                // ─────────────────────────────────────────────────────
                // 2. محاسبه Log Returns
                // ─────────────────────────────────────────────────────
                // Return[i] = ln(Price[i+1] / Price[i])
                double[] returns = new double[HurstPeriod - 1];
                for (int i = 0; i < returns.Length; i++)
                {
                    if (prices[i] > 0 && prices[i + 1] > 0)
                        returns[i] = Math.Log(prices[i + 1] / prices[i]);
                    else
                        returns[i] = 0;
                }
              
                // ─────────────────────────────────────────────────────
                // 3. محاسبه R/S برای Time Scales مختلف
                // ─────────────────────────────────────────────────────
                List<double> logTaus = new List<double>();
                List<double> logRS = new List<double>();
              
                // Time scales: 5, 10, 20, 40 bars
                int[] taus = new int[] { 5, 10, 20, 40 };
              
                foreach (int tau in taus)
                {
                    if (tau > returns.Length)
                        continue;
                  
                    int numSubsets = returns.Length / tau;
                    if (numSubsets < 2)
                        continue;
                  
                    double sumRS = 0;
                    int validSubsets = 0;
                  
                    // برای هر subset
                    for (int subset = 0; subset < numSubsets; subset++)
                    {
                        // استخراج subset
                        double[] subReturns = new double[tau];
                        for (int i = 0; i < tau; i++)
                        {
                            int idx = subset * tau + i;
                            if (idx < returns.Length)
                                subReturns[i] = returns[idx];
                        }
                      
                        // میانگین subset
                        double subMean = subReturns.Average();
                      
                        // محاسبه Cumulative Deviations
                        // Y[k] = Σ(X[i] - Mean)
                        double cum = 0;
                        double maxCum = double.MinValue;
                        double minCum = double.MaxValue;
                      
                        foreach (double r in subReturns)
                        {
                            cum += r - subMean;
                            if (cum > maxCum) maxCum = cum;
                            if (cum < minCum) minCum = cum;
                        }
                      
                        // Range = Max - Min
                        double range = maxCum - minCum;
                      
                        // Standard Deviation
                        double variance = 0;
                        foreach (double r in subReturns)
                        {
                            variance += (r - subMean) * (r - subMean);
                        }
                        double std = Math.Sqrt(variance / tau);
                      
                        // R/S
                        if (std > 1e-10 && range > 0)
                        {
                            sumRS += range / std;
                            validSubsets++;
                        }
                    }
                  
                    // میانگین R/S برای این tau
                    if (validSubsets > 0)
                    {
                        double avgRS = sumRS / validSubsets;
                        if (avgRS > 0)
                        {
                            logTaus.Add(Math.Log(tau));
                            logRS.Add(Math.Log(avgRS));
                        }
                    }
                }
              
                // ─────────────────────────────────────────────────────
                // 4. Linear Regression
                // ─────────────────────────────────────────────────────
                // log(R/S) = H × log(tau) + const
                // Slope = Hurst Exponent
              
                if (logTaus.Count < 2)
                    return 0.5;
              
                double sumX = logTaus.Sum();
                double sumY = logRS.Sum();
                double sumXY = 0;
                double sumX2 = 0;
              
                for (int i = 0; i < logTaus.Count; i++)
                {
                    sumXY += logTaus[i] * logRS[i];
                    sumX2 += logTaus[i] * logTaus[i];
                }
              
                int n = logTaus.Count;
                double denom = n * sumX2 - sumX * sumX;
              
                if (Math.Abs(denom) < 1e-10)
                    return 0.5;
              
                // Slope = Hurst
                double hurst = (n * sumXY - sumX * sumY) / denom;
              
                // محدود به [0.01, 0.99]
                return Math.Max(0.01, Math.Min(0.99, hurst));
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in CalculateHurst at {index}", ex);
                return 0.5;
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CALCULATE FD - محاسبه Fractal Dimension با الگوریتم Higuchi
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// محاسبه Fractal Dimension با الگوریتم Higuchi
        ///
        /// مفهوم:
        /// FD نشان‌دهنده complexity/chaos بازار است
        ///
        /// الگوریتم:
        /// 1. برای هر k (فاصله نمونه‌برداری):
        /// - محاسبه طول curve در scale k
        /// 2. Linear regression: log(L[k]) vs log(1/k)
        /// 3. Slope = Fractal Dimension
        ///
        /// تفسیر:
        /// FD ~ 1.0-1.3 → Smooth (صاف)
        /// FD ~ 1.4-1.6 → Random (تصادفی)
        /// FD ~ 1.7-2.0 → Chaotic (آشوب)
        ///
        /// استفاده:
        /// FD > 1.65 → شروع Correction
        /// FD < 1.45 → Stabilized (آماده ورود)
        ///
        /// منبع: Higuchi, T. (1988)
        /// </summary>
        private void CalculateFD(int index)
        {
            if (index < WindowSize - 1)
            {
                _fractalDimension[index] = 1.0;
                return;
            }
          
            try
            {
                // ─────────────────────────────────────────────────────
                // 1. استخراج قیمت‌ها
                // ─────────────────────────────────────────────────────
                int startIdx = index - WindowSize + 1;
                for (int i = 0; i < WindowSize; i++)
                {
                    _priceBuffer[i] = Bars.ClosePrices[startIdx + i];
                }
              
                // ─────────────────────────────────────────────────────
                // 2. الگوریتم Higuchi
                // ─────────────────────────────────────────────────────
                List<double> xValues = new List<double>(); // log(1/k)
                List<double> yValues = new List<double>(); // log(L[k])
              
                // برای هر k
                for (int k = 1; k <= MaxK; k++)
                {
                    double lengthSum = 0.0;
                    int validSubsets = 0;
                  
                    // برای هر offset m
                    for (int m = 0; m < k; m++)
                    {
                        double Lmk = 0.0;
                        int points = (WindowSize - m - 1) / k;
                      
                        if (points < 1)
                            continue;
                      
                        // محاسبه طول curve
                        for (int i = 1; i <= points; i++)
                        {
                            int curr = m + i * k;
                            int prev = m + (i - 1) * k;
                          
                            if (curr >= WindowSize || prev >= WindowSize)
                                continue;
                          
                            Lmk += Math.Abs(_priceBuffer[curr] - _priceBuffer[prev]);
                        }
                      
                        // Normalization
                        double norm = (WindowSize - 1.0) / (points * k * k);
                        lengthSum += Lmk * norm;
                        validSubsets++;
                    }
                  
                    // میانگین برای این k
                    if (validSubsets > 0)
                    {
                        double avgLk = lengthSum / validSubsets;
                        if (avgLk > 0 && !double.IsNaN(avgLk))
                        {
                            xValues.Add(Math.Log(1.0 / k));
                            yValues.Add(Math.Log(avgLk));
                        }
                    }
                }
              
                // ─────────────────────────────────────────────────────
                // 3. Linear Regression
                // ─────────────────────────────────────────────────────
                if (xValues.Count < 2)
                {
                    _fractalDimension[index] = 1.0;
                    return;
                }
              
                double sumX = xValues.Sum();
                double sumY = yValues.Sum();
                double sumXY = 0;
                double sumX2 = 0;
              
                for (int i = 0; i < xValues.Count; i++)
                {
                    sumXY += xValues[i] * yValues[i];
                    sumX2 += xValues[i] * xValues[i];
                }
              
                int n = xValues.Count;
                double denom = n * sumX2 - sumX * sumX;
              
                if (Math.Abs(denom) < 1e-10)
                {
                    _fractalDimension[index] = 1.0;
                    return;
                }
              
                // Slope = FD
                double fd = (n * sumXY - sumX * sumY) / denom;
              
                // محدود به [1.0, 2.0]
                _fractalDimension[index] = Math.Max(1.0, Math.Min(2.0, fd));
              
                // لاگ
                if (index == Bars.Count - 1)
                {
                    _logger.Debug($"FD: {_fractalDimension[index]:F3}, Status: {(_fractalDimension[index] > FDChaosThreshold ? "CHAOS" : "STABLE")}");
                }
            }
            catch (Exception ex)
            {
                _fractalDimension[index] = 1.0;
                _logger.Error($"Error in CalculateFD at {index}", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // UPDATE P_ZERO - تشخیص Correction و محاسبه P_zero
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// تشخیص فاز Correction و محاسبه P_zero
        ///
        /// ✅ CORRECTED: Window-based tracking (نه Global)
        ///
        /// مفهوم P_zero:
        /// در یک Trend صعودی:
        /// 1. قیمت به بالاترین نقطه می‌رسد
        /// 2. Correction شروع می‌شود (FD بالا می‌رود)
        /// 3. بعد از آرامش، قیمت به P_zero برمی‌گردد
        /// 4. P_zero = آن نقطه بالا قبل از Correction
        ///
        /// State Machine:
        /// Normal → FD↑ → Correction Started (P_zero ثبت)
        /// In Correction → FD↓ → Stabilized (آماده ورود)
        /// </summary>
        private void UpdatePZero(int index)
        {
            if (index < 1)
                return;
          
            try
            {
                double fd = _fractalDimension[index];
                int trend = (int)_trendState[index];
              
                // ─────────────────────────────────────────────────────
                // 1. به‌روزرسانی High/Low در Window (CORRECTED)
                // ─────────────────────────────────────────────────────
                // قبلاً: Global tracking → نادرست
                // حالا: فقط در PZeroLookback bars اخیر
              
                _lastHigh = Bars.HighPrices[index];
                _lastLow = Bars.LowPrices[index];
              
                int start = Math.Max(0, index - PZeroLookback);
                for (int i = start; i <= index; i++)
                {
                    if (Bars.HighPrices[i] > _lastHigh)
                        _lastHigh = Bars.HighPrices[i];
                    if (Bars.LowPrices[i] < _lastLow)
                        _lastLow = Bars.LowPrices[i];
                }
              
                // ─────────────────────────────────────────────────────
                // 2. تشخیص شروع Correction
                // ─────────────────────────────────────────────────────
                if (!_inCorrection && fd >= FDChaosThreshold)
                {
                    _inCorrection = true;
                  
                    // تعیین P_zero بر اساس Trend
                    if (trend == 1)
                        _pZero = _lastHigh;
                    else if (trend == -1)
                        _pZero = _lastLow;
                    else
                        _pZero = Bars.ClosePrices[index];
                  
                    _pZeroValid = false;
                  
                    _logger.Info($"🌀 CORRECTION STARTED - FD: {fd:F2}, P_zero: {_pZero:F5}");
                }
              
                // ─────────────────────────────────────────────────────
                // 3. تشخیص پایان Correction
                // ─────────────────────────────────────────────────────
                if (_inCorrection && fd < FDStableThreshold)
                {
                    _inCorrection = false;
                    _pZeroValid = true;
                  
                    _logger.Info($"✅ CORRECTION ENDED - FD: {fd:F2}, P_zero: {_pZero:F5}, READY");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in UpdatePZero at {index}", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CALCULATE TIP - محاسبه Tick Imbalance Pressure
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// محاسبه TIP - سیگنال ورود
        ///
        /// ✅ CORRECTED: Normalized با Z-Score (نه Raw value)
        ///
        /// فرمول:
        /// TIP = (|TIM| × BAA) / PS
        ///
        /// TIM = (BuyTicks - SellTicks) / TotalTicks
        /// BAA = DominantSideTicks / TotalTicks
        /// PS = PriceRange / TotalTicks
        ///
        /// مفهوم:
        /// TIP بالا = حجم زیاد، قیمت کم → Zero-Velocity Momentum
        ///
        /// Normalization:
        /// Z-Score = (TIP - μ) / σ
        /// Threshold: 2.0 σ (2 انحراف معیار)
        /// </summary>
        private void CalculateTIP()
        {
            int total = _buyTicks + _sellTicks;
          
            if (total == 0)
            {
                _currentTIP = 0;
                _normalizedTIP = 0;
                return;
            }
          
            try
            {
                // ─────────────────────────────────────────────────────
                // 1. محاسبه TIM (Tick Imbalance)
                // ─────────────────────────────────────────────────────
                double TIM = (double)(_buyTicks - _sellTicks) / total;
              
                // ─────────────────────────────────────────────────────
                // 2. محاسبه Price Range
                // ─────────────────────────────────────────────────────
                int idx = Bars.Count - 1;
                int lookback = Math.Min(TIPLookbackBars, idx);
              
                if (lookback < 1)
                {
                    _currentTIP = 0;
                    _normalizedTIP = 0;
                    _buyTicks = 0;
                    _sellTicks = 0;
                    return;
                }
              
                double high = Bars.HighPrices[idx];
                double low = Bars.LowPrices[idx];
              
                for (int i = 1; i <= lookback; i++)
                {
                    int id = idx - i;
                    if (id >= 0)
                    {
                        if (Bars.HighPrices[id] > high)
                            high = Bars.HighPrices[id];
                        if (Bars.LowPrices[id] < low)
                            low = Bars.LowPrices[id];
                    }
                }
              
                double range = high - low;
              
                // ─────────────────────────────────────────────────────
                // 3. محاسبه PS (Price Sensitivity)
                // ─────────────────────────────────────────────────────
                double PS = Math.Max(0.000001, range / total);
              
                // ─────────────────────────────────────────────────────
                // 4. محاسبه BAA (Bulk-Acting Aggression)
                // ─────────────────────────────────────────────────────
                double BAA = TIM >= 0 ?
                    (double)_buyTicks / total :
                    (double)_sellTicks / total;
              
                // ─────────────────────────────────────────────────────
                // 5. محاسبه TIP
                // ─────────────────────────────────────────────────────
                _currentTIP = (Math.Abs(TIM) * BAA) / PS;
              
                // ─────────────────────────────────────────────────────
                // 6. Normalization - Z-Score (CORRECTED)
                // ─────────────────────────────────────────────────────
                _tipHistory.Enqueue(_currentTIP);
              
                if (_tipHistory.Count > TIPHistorySize)
                    _tipHistory.Dequeue();
              
                if (_tipHistory.Count >= 30)
                {
                    double[] hist = _tipHistory.ToArray();
                    double mean = hist.Average();
                    double variance = hist.Sum(t => (t - mean) * (t - mean)) / hist.Length;
                    double std = Math.Sqrt(variance);
                  
                    if (std > 1e-10)
                    {
                        _normalizedTIP = (_currentTIP - mean) / std;
                    }
                    else
                    {
                        _normalizedTIP = 0;
                    }
                }
                else
                {
                    _normalizedTIP = 0;
                }
              
                // لاگ سیگنال‌های قوی
                if (_normalizedTIP > TIPZScoreThreshold)
                {
                    _logger.Info($"🔥 TIP SIGNAL - Raw: {_currentTIP:F2}, Z-Score: {_normalizedTIP:F2}");
                }
            }
            catch (Exception ex)
            {
                _currentTIP = 0;
                _normalizedTIP = 0;
                _logger.Error("Error in CalculateTIP", ex);
            }
            finally
            {
                _buyTicks = 0;
                _sellTicks = 0;
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // UPDATE TOXICITY - مانیتورینگ سلامت بازار
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Toxicity Monitor - تشخیص بازار Toxic
        ///
        /// ✅ CORRECTED: استفاده از Median (نه Mean)
        ///
        /// مفهوم:
        /// وقتی Informed Traders فعال شوند:
        /// - Market Makers احساس خطر می‌کنند
        /// - Spread را پهن می‌کنند
        /// - این Toxic Flow است
        ///
        /// محاسبه:
        /// 1. Spread = (Ask - Bid) / Mid
        /// 2. تاریخچه → مرتب → Median
        /// 3. MAD = Median Absolute Deviation
        /// 4. Effective = Median + 2×MAD
        /// 5. Score = Effective / Normal
        ///
        /// چرا Median؟
        /// - Outliers را ignore می‌کند
        /// - Spike موقت → Median ثابت
        /// - Mean اشتباهاً بالا می‌رود
        ///
        /// تفسیر:
        /// Score < 1.5 → نرمال ✅
        /// Score 1.5-2.5 → ناآرام ⚠️
        /// Score > 2.5 → Toxic ❌
        ///
        /// منبع: Easley et al. (2012)
        /// </summary>
        private void UpdateToxicity()
        {
            try
            {
                // ─────────────────────────────────────────────────────
                // 1. محاسبه Spread نسبی
                // ─────────────────────────────────────────────────────
                double bid = Symbol.Bid;
                double ask = Symbol.Ask;
                double mid = (bid + ask) / 2.0;
              
                if (mid <= 0)
                    return;
              
                double spread = (ask - bid) / mid;
              
                // ─────────────────────────────────────────────────────
                // 2. اضافه به تاریخچه
                // ─────────────────────────────────────────────────────
                _spreadHistory.Enqueue(spread);
              
                if (_spreadHistory.Count > SpreadHistorySize)
                    _spreadHistory.Dequeue();
              
                if (_spreadHistory.Count < 10)
                {
                    _effectiveSpread = spread;
                    _toxicityScore = 0;
                    _marketSafe = true;
                    return;
                }
              
                // ─────────────────────────────────────────────────────
                // 3. محاسبه Median (CORRECTED)
                // ─────────────────────────────────────────────────────
                double[] spreads = _spreadHistory.ToArray();
                Array.Sort(spreads);
              
                int count = spreads.Length;
                double median;
              
                if (count % 2 == 0)
                    median = (spreads[count / 2 - 1] + spreads[count / 2]) / 2.0;
                else
                    median = spreads[count / 2];
              
                // ─────────────────────────────────────────────────────
                // 4. محاسبه MAD (Median Absolute Deviation)
                // ─────────────────────────────────────────────────────
                double[] deviations = new double[count];
                for (int i = 0; i < count; i++)
                {
                    deviations[i] = Math.Abs(spreads[i] - median);
                }
              
                Array.Sort(deviations);
              
                double mad;
                if (count % 2 == 0)
                    mad = (deviations[count / 2 - 1] + deviations[count / 2]) / 2.0;
                else
                    mad = deviations[count / 2];
              
                // ─────────────────────────────────────────────────────
                // 5. Effective Spread
                // ─────────────────────────────────────────────────────
                _effectiveSpread = median + (2.0 * mad);
              
                // ─────────────────────────────────────────────────────
                // 6. Toxicity Score
                // ─────────────────────────────────────────────────────
                // ✅ اصلاح شده: مخرج کسر باید خودِ میانه باشه
              double normal = median; 
              
              // اگر میانه خیلی کوچیک بود (نزدیک صفر)، برای جلوگیری از انفجار، امتیاز رو 1 بذار
              if (normal > 1e-6) 
                  _toxicityScore = _effectiveSpread / normal;
              else 
                  _toxicityScore = 1.0;
                // ─────────────────────────────────────────────────────
                // 7. تعیین وضعیت بازار
                // ─────────────────────────────────────────────────────
                _marketSafe = _toxicityScore < ToxicityThreshold;
              
                if (!_marketSafe)
                {
                    _logger.Warning($"⚠️ TOXIC MARKET - Score: {_toxicityScore:F2}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in UpdateToxicity", ex);
                _marketSafe = true;
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // CHECK ENTRY - بررسی شرایط ورود
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// بررسی 6 شرط ورود:
        /// 1. Trend معتبر (Hurst > Threshold)
        /// 2. P_zero معتبر (Correction تمام شده)
        /// 3. FD Stabilized (بازار آرام)
        /// 4. Price در موقعیت مناسب
        /// 5. TIP Signal قوی (Z-Score > Threshold)
        /// 6. Market Safe (Toxicity پایین) ✅
        /// </summary>
        private void CheckEntry(int index)
        {
            try
            {
                // بررسی محدودیت معاملات
                if (Positions.Count >= MaxPositions)
                    return;
              
              // ✅ اصلاح شده: بشمار ببین چند تا بازه، اگه جا داشتیم وارد شو
              int myPositions = Positions.Count(p => p.Label == MagicNumber.ToString());
                if (myPositions >= MaxPositions)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 1. Trend معتبر
                // ─────────────────────────────────────────────────────
                int trend = (int)_trendState[index];
                double hurst = _hurst[index];
              
                if (trend == 0 || hurst <= HurstThreshold)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 2. P_zero معتبر
                // ─────────────────────────────────────────────────────
                if (!_pZeroValid || _pZero == 0)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 3. FD Stabilized
                // ─────────────────────────────────────────────────────
                double fd = _fractalDimension[index];
              
                if (fd >= FDStableThreshold + 0.05)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 4. موقعیت قیمت
                // ─────────────────────────────────────────────────────
                double price = Bars.ClosePrices[index];
              
                bool priceOK = trend == 1 ? price < _pZero : price > _pZero;
              
                if (!priceOK)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 5. TIP Signal (CORRECTED - با Z-Score)
                // ─────────────────────────────────────────────────────
                if (_normalizedTIP <= TIPZScoreThreshold)
                    return;
              
                // ─────────────────────────────────────────────────────
                // 6. Market Safe (ADDED)
                // ─────────────────────────────────────────────────────
                if (!_marketSafe)
                {
                    _logger.Warning($"⚠️ Skipped - TOXIC market (Score: {_toxicityScore:F2})");
                    return;
                }
              
                // ═════════════════════════════════════════════════════
                // 🎯 همه شرایط OK - اجرای معامله
                // ═════════════════════════════════════════════════════
                _logger.Info("════════════════════════════════════════");
                _logger.Info("🎯 ALL CONDITIONS MET");
                _logger.Info($" Trend: {trend}, Hurst: {hurst:F3}");
                _logger.Info($" P_zero: {_pZero:F5}");
                _logger.Info($" FD: {fd:F3}");
                _logger.Info($" TIP Z-Score: {_normalizedTIP:F2}");
                _logger.Info($" Toxicity: {_toxicityScore:F2} ✅");
                _logger.Info("════════════════════════════════════════");
              
                ExecuteTrade(trend);
            }
            catch (Exception ex)
            {
                _logger.Error("Error in CheckEntry", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // EXECUTE TRADE - اجرای معامله
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// اجرای معامله با:
        /// 1. محاسبه Stop Loss پویا
        /// 2. Position Sizing بر اساس ریسک
        /// 3. محاسبه Targets (TP1, TP2)
        /// 4. ثبت Trade Context
        /// </summary>
        private void ExecuteTrade(int trendState)
        {
            try
            {
                TradeType dir = trendState == 1 ? TradeType.Buy : TradeType.Sell;
                double price = dir == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
                double stretch = Math.Abs(price - _pZero);
              
                // ─────────────────────────────────────────────────────
                // 1. محاسبه Stop Loss (Dynamic)
                // ─────────────────────────────────────────────────────
                double slDist;
              
                if (UseDynamicStopLoss && _atr != null && !double.IsNaN(_atr.Result.LastValue))
                {
                    int idx = Bars.Count - 1;
                    double S = _fractalDimension[idx] - 1.0;
                    double H = _hurst[idx];
                    double M = ((1.0 + S) / (1.0 + H)) * BaseStopMultiplier;
                  
                    double atrStop = Math.Max(M * _atr.Result.LastValue, 0.5 * _atr.Result.LastValue);
                    double stretchStop = stretch + (StopLossBuffer * Symbol.PipSize);
                  
                    slDist = Math.Max(atrStop, stretchStop);
                }
                else
                {
                    slDist = stretch + (StopLossBuffer * Symbol.PipSize);
                }
              
                // ─────────────────────────────────────────────────────
                // 2. Position Sizing
                // ─────────────────────────────────────────────────────
                double slPips = slDist / Symbol.PipSize;
                double risk = Account.Balance * (RiskPercent / 100.0);
                double volume = risk / (slPips * Symbol.PipValue);
              
                volume = Symbol.NormalizeVolumeInUnits(volume);
                volume = Math.Max(Symbol.VolumeInUnitsMin, volume);
                volume = Math.Min(Symbol.VolumeInUnitsMax, volume);
              
                if (volume < Symbol.VolumeInUnitsMin)
                {
                    _logger.Warning($"Volume too small: {volume}");
                    return;
                }
              
                // ─────────────────────────────────────────────────────
                // 3. محاسبه Targets
                // ─────────────────────────────────────────────────────
                double tp1 = _pZero;
                double tp2 = dir == TradeType.Buy ?
                    _pZero + (stretch * BallisticMultiplier) :
                    _pZero - (stretch * BallisticMultiplier);
              
                // ─────────────────────────────────────────────────────
                // 4. اجرای Order
                // ─────────────────────────────────────────────────────
                var result = ExecuteMarketOrder(dir, SymbolName, volume,
                    MagicNumber.ToString(), slPips, null);
              
                if (result != null && result.IsSuccessful && result.Position != null)
                {
                    // ثبت Context
                    lock (_tradesLock)
                    {
                        _activeTrades[result.Position.Id] = new TradeContext
                        {
                            PositionId = result.Position.Id,
                            EntryPrice = price,
                            PZero = _pZero,
                            Stretch = stretch,
                            TP1 = tp1,
                            TP2 = tp2,
                            TP1Hit = false,
                            TP2Hit = false,
                            TrailingActive = false,
                            EntryBarIndex = Bars.Count - 1,
                            EntryTrendState = trendState,
                            Direction = dir,
                            EntryTime = Server.Time
                        };
                    }
                  
                    // Reset P_zero
                    _pZeroValid = false;
                    _lastHigh = 0;
                    _lastLow = double.MaxValue;
                  
                    _logger.Info("════════════════════════════════════════");
                    _logger.Info("🚀 TRADE EXECUTED");
                    _logger.Info($"Position: {result.Position.Id}");
                    _logger.Info($"Direction: {dir}");
                    _logger.Info($"Entry: {price:F5}");
                    _logger.Info($"Volume: {volume / 1000:F2} lots");
                    _logger.Info($"Stop: {slPips:F1} pips");
                    _logger.Info($"Risk: ${risk:F2}");
                    _logger.Info($"TP1: {tp1:F5}");
                    _logger.Info($"TP2: {tp2:F5}");
                    _logger.Info("════════════════════════════════════════");
                    // 🎨 رسم خطوط تارگت و متن (اصلاح شده)
                    // TP1: خط سبز + متن
                    string tp1Name = "TP1_" + result.Position.Id;
                    Chart.DrawHorizontalLine(tp1Name, tp1, Color.Lime, 1, LineStyle.Solid);
                    Chart.DrawText("Txt_" + tp1Name, "TP1 (P_zero)", Bars.Count, tp1, Color.Lime);
                    // TP2: خط فیروزه‌ای + متن
                    string tp2Name = "TP2_" + result.Position.Id;
                    Chart.DrawHorizontalLine(tp2Name, tp2, Color.Cyan, 1, LineStyle.Dots);
                    Chart.DrawText("Txt_" + tp2Name, "TP2 (Ballistic)", Bars.Count, tp2, Color.Cyan);
                }
                else
                {
                    _logger.Error($"❌ Trade failed: {result?.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in ExecuteTrade", ex);
            }
        }
        
      
        // ════════════════════════════════════════════════════════════════════════
        // MANAGE POSITIONS - مدیریت معاملات باز (5-Level Exit)
        // ════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// مدیریت معاملات با 5 سطح دفاعی:
        /// 1. Hard Stop Loss
        /// 2. Target Management (TP1 & TP2)
        /// 3. Trailing Stop
        /// 4. Trend Reversal Exit
        /// 5. Time Stops
        /// </summary>
        private void ManagePositions()
        {
            try
            {
                List<int> toRemove = new List<int>();
                List<TradeContext> contexts;
              
                lock (_tradesLock)
                {
                    contexts = _activeTrades.Values.ToList();
                }
              
                foreach (var ctx in contexts)
                {
                    var pos = Positions.FirstOrDefault(p => p.Id == ctx.PositionId);
                  
                    if (pos == null)
                    {
                        toRemove.Add(ctx.PositionId);
                        continue;
                    }
                  
                    double price = pos.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
                    int idx = Bars.Count - 1;
                    int bars = idx - ctx.EntryBarIndex;
                  
                    // ═════════════════════════════════════════════════
                    // LEVEL 4: Trend Reversal Exit
                    // ═════════════════════════════════════════════════
                    if (EnableTrendReversalExit && idx >= 0 && idx < _trendState.Count)
                    {
                        int currTrend = (int)_trendState[idx];
                      
                        if (ctx.EntryTrendState != 0 && currTrend != 0 &&
                            ctx.EntryTrendState != currTrend)
                        {
                            _logger.Info($"🔄 TREND REVERSAL - Pos {pos.Id}");
                            ClosePositionSafe(pos);
                            toRemove.Add(ctx.PositionId);
                            _perfMonitor?.RecordTrade(pos.NetProfit);
                                        // 🧹 پاک کردن خطوط مربوط به همین معامله
            Chart.RemoveObject("TP1_" + pos.Id);
            Chart.RemoveObject("TP2_" + pos.Id);

                            continue;
                        }
                    }
                  
                    // ═════════════════════════════════════════════════
                    // LEVEL 5: Time Stops
                    // ═════════════════════════════════════════════════
                    if (EnableTimeStops)
                    {
                        if (!ctx.TP1Hit && bars > TimeStop1Bars)
                        {
                            _logger.Info($"⏰ TIME STOP 1 - Pos {pos.Id}");
                            ClosePositionSafe(pos);
                            toRemove.Add(ctx.PositionId);
                            _perfMonitor?.RecordTrade(pos.NetProfit);
                            continue;
                        }
                      
                        if (ctx.TP1Hit && !ctx.TP2Hit && bars > TimeStop2Bars)
                        {
                            _logger.Info($"⏰ TIME STOP 2 - Pos {pos.Id}");
                            ClosePositionSafe(pos);
                            toRemove.Add(ctx.PositionId);
                            _perfMonitor?.RecordTrade(pos.NetProfit);
                            continue;
                        }
                    }
                  
                    // ═════════════════════════════════════════════════
                    // LEVEL 2: TP1
                    // ═════════════════════════════════════════════════
                    if (!ctx.TP1Hit)
                    {
                        bool tp1Hit = pos.TradeType == TradeType.Buy ?
                            price >= ctx.TP1 : price <= ctx.TP1;
                      
                        if (tp1Hit)
                        {
                            double closeVol = pos.VolumeInUnits * (TP1Percent / 100.0);
                            closeVol = Symbol.NormalizeVolumeInUnits(closeVol);
                          
                            if (closeVol >= Symbol.VolumeInUnitsMin && closeVol <= pos.VolumeInUnits)
                            {
                                var res = ClosePositionSafe(pos, closeVol);
                              
                                if (res != null && res.IsSuccessful)
                                {
                                    ctx.TP1Hit = true;
                                    ctx.TrailingActive = true;
                                    ModifyPositionSafe(pos, ctx.EntryPrice, null);
                                    _logger.Info($"✅ TP1 HIT - Pos {pos.Id}");
                                }
                            }
                        }
                    }
                  
                    // ═════════════════════════════════════════════════
                    // LEVEL 2: TP2
                    // ═════════════════════════════════════════════════
                    if (ctx.TP1Hit && !ctx.TP2Hit)
                    {
                        bool tp2Hit = pos.TradeType == TradeType.Buy ?
                            price >= ctx.TP2 : price <= ctx.TP2;
                      
                        if (tp2Hit)
                        {
                            _logger.Info($"🎯 TP2 HIT - Pos {pos.Id}");
                            ClosePositionSafe(pos);
                            toRemove.Add(ctx.PositionId);
                            ctx.TP2Hit = true;
                            _perfMonitor?.RecordTrade(pos.NetProfit);
                            continue;
                        }
                    }
                  
                    // ═════════════════════════════════════════════════
                    // LEVEL 3: Trailing Stop
                    // ═════════════════════════════════════════════════
                    if (ctx.TrailingActive && _atr != null && !double.IsNaN(_atr.Result.LastValue))
                    {
                        double trailDist = _atr.Result.LastValue * TrailingATRMultiple;
                      
                        if (pos.TradeType == TradeType.Buy)
                        {
                            double newSL = price - trailDist;
                            if (pos.StopLoss == null || newSL > pos.StopLoss.Value)
                                ModifyPositionSafe(pos, newSL, pos.TakeProfit);
                        }
                        else
                        {
                            double newSL = price + trailDist;
                            if (pos.StopLoss == null || newSL < pos.StopLoss.Value)
                                ModifyPositionSafe(pos, newSL, pos.TakeProfit);
                        }
                    }
                }
              
                // پاک‌سازی
                if (toRemove.Count > 0)
                {
                    lock (_tradesLock)
                    {
                        foreach (var id in toRemove)
                            _activeTrades.Remove(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in ManagePositions", ex);
            }
        }
      
        // ════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ════════════════════════════════════════════════════════════════════════
      
        private TradeResult ClosePositionSafe(Position pos, double? volume = null)
        {
            try
            {
                if (pos == null)
                    return null;
              
                var result = volume.HasValue ?
                    ClosePosition(pos, volume.Value) :
                    ClosePosition(pos);
              
                if (result != null && result.IsSuccessful)
                    _logger.Info($"✅ Closed {pos.Id}");
                else
                    _logger.Error($"❌ Failed to close {pos.Id}");
              
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception closing {pos?.Id}", ex);
                return null;
            }
        }
      
        private TradeResult ModifyPositionSafe(Position pos, double? sl, double? tp)
        {
            try
            {
                if (pos == null)
                    return null;
              
               return ModifyPosition(pos, sl, tp, ProtectionType.Absolute);
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception modifying {pos?.Id}", ex);
                return null;
            }
        }
      
                private void OnPositionClosed(PositionClosedEventArgs args)
        {
            try
            {
                var pos = args.Position;
              
                _logger.Info("════════════════════════════════════════");
                _logger.Info("📊 POSITION CLOSED");
                _logger.Info($"ID: {pos.Id}");
                _logger.Info($"P&L: ${pos.NetProfit:F2}");
                _logger.Info("════════════════════════════════════════");
              
               // 🧹 تمیزکاری نهایی: حذف خطوط و متن‌ها
                string id = pos.Id.ToString();
                Chart.RemoveObject("TP1_" + id);
                Chart.RemoveObject("TP2_" + id);
                Chart.RemoveObject("Txt_TP1_" + id); // پاک کردن متن TP1
                Chart.RemoveObject("Txt_TP2_" + id); // پاک کردن متن TP2
                lock (_tradesLock)
                {
                    _activeTrades.Remove(pos.Id);
                }
              
                _perfMonitor?.RecordTrade(pos.NetProfit);
            }
            catch (Exception ex)
            {
                _logger.Error("Error in OnPositionClosed", ex);
            }
        }

      
        // ════════════════════════════════════════════════════════════════════════
        // ON STOP
        // ════════════════════════════════════════════════════════════════════════
        protected override void OnStop()
        {
            try
            {
                _logger.Info("════════════════════════════════════════");
                _logger.Info("⏹️ STOPPING BOT");
                _logger.Info("════════════════════════════════════════");
              
                if (_perfMonitor != null && ShowPerformanceStats)
                {
                    _logger.Info(_perfMonitor.GetReport());
                }
              
                lock (_tradesLock)
                {
                    _logger.Info($"Active Trades: {_activeTrades.Count}");
                  
                    if (_activeTrades.Count > 0)
                    {
                        _logger.Warning("⚠️ Open positions!");
                        foreach (var ctx in _activeTrades.Values)
                            _logger.Info($" - {ctx}");
                    }
                }
              
                _logger.Info($"Market Status:");
                _logger.Info($" P_zero: {_pZero:F5}, Valid: {_pZeroValid}");
                _logger.Info($" In Correction: {_inCorrection}");
                _logger.Info($" Market Safe: {_marketSafe}");
                _logger.Info($" Toxicity: {_toxicityScore:F2}");
              
                _logger.Info(_logger.GetSummary());
                _logger.Info("════════════════════════════════════════");
                _logger.Info("✅ Bot stopped!");
                _logger.Info("════════════════════════════════════════");
              
                Positions.Closed -= OnPositionClosed;
            }
            catch (Exception ex)
            {
                Print($"❌ Error in OnStop: {ex.Message}");
            }
        }
    }
}
// ══════════════════════════════════════════════════════════════════════════════
// پایان کد - HydroDynamic Trading Bot v8.2
// ════════════════════════════════