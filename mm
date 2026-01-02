using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// 🎯 HIGUCHI CHAOS HUNTER v2.0 - ربات معاملاتی مبتنی بر بعد فرکتال هیگوچی
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// 
    /// توسعه‌دهنده: khajavi 
    /// تاریخ: 2026-01-02
    /// نسخه: 2.0 (Final - Bug Fixed)
    /// 
    /// تغییرات نسخه 2.0:
    /// ✅ رفع باگ خودزنی در تریلینگ (Chaos Guard اصلاح شد)
    /// ✅ مدیریت باکس‌های غول‌پیکر (Adaptive SL)
    /// ✅ ریست هوشمند باکس (فقط با شروع سیکل جدید)
    /// ✅ موتور گرافیکی کامل (رسم باکس‌ها روی چارت)
    /// ✅ بهینه‌سازی برای طلا و فارکس
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HiguchiChaosHunterV2 : Robot
    {
        #region پارامترهای ورودی (Input Parameters)

        [Parameter("🔧 Window Size (تعداد کندل)", DefaultValue = 50, MinValue = 20, MaxValue = 200)]
        public int WindowSize { get; set; }

        [Parameter("🔧 Max K (رزولوشن)", DefaultValue = 8, MinValue = 3, MaxValue = 15)]
        public int MaxK { get; set; }

        [Parameter("🌪️ Chaos Threshold (آستانه آشوب)", DefaultValue = 0.65, MinValue = 0.3, MaxValue = 1.5, Step = 0.01)]
        public double ChaosThreshold { get; set; }

        [Parameter("💰 Risk Percent (درصد ریسک)", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("🎯 Risk:Reward Ratio (نسبت)", DefaultValue = 4.0, MinValue = 2.0, MaxValue = 10.0)]
        public double RiskRewardRatio { get; set; }

        [Parameter("📏 Max Box Pips (حداکثر ارتفاع باکس)", DefaultValue = 30, MinValue = 10, MaxValue = 100)]
        public int MaxBoxPips { get; set; }

        [Parameter("🎨 Show Graphics (نمایش گرافیک)", DefaultValue = true)]
        public bool ShowGraphics { get; set; }

        [Parameter("📝 Trade Label", DefaultValue = "HCH_v2")]
        public string TradeLabel { get; set; }

        [Parameter("🔐 Enable Lock System", DefaultValue = true)]
        public bool EnableLock { get; set; }

        [Parameter("🔑 Lock Code (کد فعال‌سازی)", DefaultValue = "")]
        public string LockCode { get; set; }

        #endregion

        #region متغیرهای سراسری (Global Variables)

        // ═══════════════════════════════════════════════════════════
        // متغیرهای باکس (Box State Variables)
        // ═══════════════════════════════════════════════════════════
        private double? BoxHigh;
        private double? BoxLow;
        private DateTime BoxStartTime;
        private double MaxHfdSession;
        private bool IsChaosActive;
        private bool IsBoxLocked;
        private bool TradeLocked;

        // ═══════════════════════════════════════════════════════════
        // متغیرهای معامله (Trade Variables)
        // ═══════════════════════════════════════════════════════════
        private Position CurrentPosition;
        private double CurrentRiskAmount;
        private double InitialStopLoss;
        private bool ChaosGuardActivated; // فلگ جدید برای جلوگیری از تکرار

        // ═══════════════════════════════════════════════════════════
        // متغیرهای سیستم لاک
        // ═══════════════════════════════════════════════════════════
        private bool IsSystemLocked = true;
        private const string CorrectLockCode = "HIGUCHI2025";

        // ═══════════════════════════════════════════════════════════
        // ثوابت گرافیکی
        // ═══════════════════════════════════════════════════════════
        private const string BOX_NAME = "HCH_ActiveBox";
        private const string STATUS_NAME = "HCH_Status";
        private const string SL_LINE_NAME = "HCH_SL";
        private const string TP_LINE_NAME = "HCH_TP";

        #endregion

        #region راه‌اندازی (Initialization)

        protected override void OnStart()
        {
            Print("═══════════════════════════════════════════════════════");
            Print("🎯 HIGUCHI CHAOS HUNTER v2.0 (Bug Fixed)");
            Print("═══════════════════════════════════════════════════════");

            // بررسی سیستم لاک
            if (EnableLock)
            {
                if (string.IsNullOrEmpty(LockCode) || LockCode != CorrectLockCode)
                {
                    Print("❌ کد فعال‌سازی نادرست است!");
                    Print("⚠️ ربات قفل است و معامله نخواهد کرد.");
                    Print($"💡 کد صحیح: {CorrectLockCode}");
                    IsSystemLocked = true;
                }
                else
                {
                    Print("✅ کد فعال‌سازی صحیح است.");
                    Print("🔓 ربات فعال شد!");
                    IsSystemLocked = false;
                }
            }
            else
            {
                Print("ℹ️ سیستم لاک غیرفعال است.");
                IsSystemLocked = false;
            }

            // نمایش تنظیمات
            Print($"📊 Symbol: {SymbolName}");
            Print($"📈 Timeframe: {TimeFrame}");
            Print($"🔧 Window Size: {WindowSize}");
            Print($"🔧 Max K: {MaxK}");
            Print($"🌪️ Chaos Threshold: {ChaosThreshold}");
            Print($"💰 Risk per Trade: {RiskPercent}%");
            Print($"🎯 Risk:Reward: 1:{RiskRewardRatio}");
            Print($"📏 Max Box: {MaxBoxPips} pips");
            Print($"🎨 Graphics: {(ShowGraphics ? "ON" : "OFF")}");
            Print("═══════════════════════════════════════════════════════");

            ResetBoxState();
        }

        #endregion

        #region رویداد کندل جدید (OnBar Event)

        protected override void OnBar()
        {
            if (IsSystemLocked)
                return;

            // قانون INDEX 1: فقط کندل بسته شده
            int lastClosedIndex = 1;

            if (Bars.Count < WindowSize + lastClosedIndex)
            {
                Print("⏳ در انتظار داده کافی...");
                return;
            }

            // محاسبه HFD
            double currentHfd = CalculateHiguchiFD(lastClosedIndex);

            if (double.IsNaN(currentHfd))
                return;

            double high = Bars.HighPrices[lastClosedIndex];
            double low = Bars.LowPrices[lastClosedIndex];
            double close = Bars.ClosePrices[lastClosedIndex];

            // به‌روزرسانی وضعیت باکس
            UpdateBoxState(currentHfd, high, low);

            // مدیریت پوزیشن یا ورود جدید
            if (CurrentPosition != null && !CurrentPosition.IsClosed)
            {
                ManagePosition(close, currentHfd);
            }
            else
            {
                CheckEntrySignal(close);
            }

            // به‌روزرسانی گرافیک
            UpdateVisuals();
        }

        #endregion

        #region محاسبه بعد فرکتال هیگوچی (Higuchi Fractal Dimension)

        private double CalculateHiguchiFD(int startIndex)
        {
            try
            {
                // استخراج داده
                List<double> data = new List<double>();
                for (int i = startIndex; i < startIndex + WindowSize; i++)
                {
                    if (i < Bars.ClosePrices.Count)
                        data.Add(Bars.ClosePrices[i]);
                }

                if (data.Count < WindowSize)
                    return double.NaN;

                int N = data.Count;
                List<double> logK = new List<double>();
                List<double> logL = new List<double>();

                // محاسبه طول‌ها برای هر k
                for (int k = 1; k <= MaxK; k++)
                {
                    double Lk = 0;

                    for (int m = 1; m <= k; m++)
                    {
                        double Lmk = 0;
                        int maxI = (int)Math.Floor((double)(N - m) / k);

                        for (int i = 1; i <= maxI; i++)
                        {
                            int idx1 = m + (i - 1) * k - 1;
                            int idx2 = m + i * k - 1;

                            if (idx1 >= 0 && idx2 < N)
                                Lmk += Math.Abs(data[idx2] - data[idx1]);
                        }

                        if (maxI > 0)
                        {
                            Lmk = Lmk * (N - 1) / (maxI * k);
                            Lk += Lmk;
                        }
                    }

                    Lk = Lk / k;

                    if (Lk > 0)
                    {
                        logK.Add(Math.Log(1.0 / k));
                        logL.Add(Math.Log(Lk));
                    }
                }

                // رگرسیون خطی
                if (logK.Count < 2)
                    return double.NaN;

                double n = logK.Count;
                double sumX = logK.Sum();
                double sumY = logL.Sum();
                double sumXY = 0;
                double sumX2 = 0;

                for (int i = 0; i < n; i++)
                {
                    sumXY += logK[i] * logL[i];
                    sumX2 += logK[i] * logK[i];
                }

                double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

                return slope;
            }
            catch (Exception ex)
            {
                Print($"❌ خطا در محاسبه HFD: {ex.Message}");
                return double.NaN;
            }
        }

        #endregion

        #region ماشین وضعیت باکس (Box State Machine)

        private void UpdateBoxState(double currentHfd, double high, double low)
        {
            // وضعیت A: شروع آشوب (سیکل جدید)
            if (currentHfd > ChaosThreshold && !IsChaosActive)
            {
                BoxHigh = high;
                BoxLow = low;
                BoxStartTime = Bars.OpenTimes[1];
                MaxHfdSession = currentHfd;
                IsChaosActive = true;
                IsBoxLocked = false;
                TradeLocked = false;

                Print($"🌪️ شروع آشوب! HFD={currentHfd:F4} | Box=[{BoxLow:F5}, {BoxHigh:F5}]");
                return;
            }

            // وضعیت B: درون فاز آشوب
            if (currentHfd > ChaosThreshold && IsChaosActive)
            {
                // B1: افزایش آشوب (گسترش)
                if (currentHfd >= MaxHfdSession)
                {
                    BoxHigh = Math.Max(BoxHigh.Value, high);
                    BoxLow = Math.Min(BoxLow.Value, low);
                    MaxHfdSession = currentHfd;
                    IsBoxLocked = false;

                    Print($"📈 گسترش باکس | HFD={currentHfd:F4} | Box=[{BoxLow:F5}, {BoxHigh:F5}]");
                }
                // B2: کاهش آشوب (فیکس موقت)
                else
                {
                    IsBoxLocked = true;
                    Print($"🔒 قفل موقت باکس | HFD={currentHfd:F4}");
                }
                return;
            }

            // وضعیت C: خروج از آشوب (فیکس دائم)
            if (currentHfd < ChaosThreshold && IsChaosActive)
            {
                IsBoxLocked = true;
                IsChaosActive = false;

                Print($"✅ خروج از آشوب - باکس فیکس دائم | HFD={currentHfd:F4}");
                return;
            }
        }

        private void ResetBoxState()
        {
            BoxHigh = null;
            BoxLow = null;
            MaxHfdSession = 0;
            IsChaosActive = false;
            IsBoxLocked = false;
            TradeLocked = false;
            ChaosGuardActivated = false;

            // پاک کردن گرافیک
            Chart.RemoveObject(BOX_NAME);
            Chart.RemoveObject(STATUS_NAME);
        }

        #endregion

        #region استراتژی ورود (Entry Strategy with Adaptive SL)

        private void CheckEntrySignal(double closePrice)
        {
            if (!IsBoxLocked || BoxHigh == null || BoxLow == null || TradeLocked)
                return;

            // محاسبه ارتفاع باکس
            double boxHeight = BoxHigh.Value - BoxLow.Value;
            double boxHeightInPips = boxHeight / Symbol.PipSize;

            // ═══════════════════════════════════════════════════════════
            // سیگنال خرید
            // ═══════════════════════════════════════════════════════════
            if (closePrice > BoxHigh.Value)
            {
                double entryPrice = closePrice;
                double stopLoss;

                // استاپ لاس تطبیقی (Adaptive SL)
                if (boxHeightInPips <= MaxBoxPips)
                {
                    // حالت نرمال: استاپ سمت مقابل باکس
                    stopLoss = BoxLow.Value;
                }
                else
                {
                    // حالت غول‌پیکر: استاپ روی وسط باکس
                    stopLoss = BoxHigh.Value - (boxHeight * 0.5);
                    Print($"⚠️ باکس بزرگ ({boxHeightInPips:F1} pips) - استاپ روی وسط");
                }

                double riskAmount = entryPrice - stopLoss;
                double takeProfit = entryPrice + (riskAmount * RiskRewardRatio);
                double volumeInLots = CalculatePositionSize(riskAmount);

                var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInLots,
                    TradeLabel, stopLoss, takeProfit);

                if (result.IsSuccessful)
                {
                    CurrentPosition = result.Position;
                    CurrentRiskAmount = riskAmount;
                    InitialStopLoss = stopLoss;
                    TradeLocked = true;
                    ChaosGuardActivated = false;

                    Print($"🟢 خرید | Entry={entryPrice:F5} | SL={stopLoss:F5} | TP={takeProfit:F5} | Vol={volumeInLots}");
                    Print($"📊 Box Height: {boxHeightInPips:F1} pips | Risk: {riskAmount / Symbol.PipSize:F1} pips");
                }
                else
                {
                    Print($"❌ خطا در خرید: {result.Error}");
                }
            }

            // ═══════════════════════════════════════════════════════════
            // سیگنال فروش
            // ═══════════════════════════════════════════════════════════
            else if (closePrice < BoxLow.Value)
            {
                double entryPrice = closePrice;
                double stopLoss;

                if (boxHeightInPips <= MaxBoxPips)
                {
                    stopLoss = BoxHigh.Value;
                }
                else
                {
                    stopLoss = BoxLow.Value + (boxHeight * 0.5);
                    Print($"⚠️ باکس بزرگ ({boxHeightInPips:F1} pips) - استاپ روی وسط");
                }

                double riskAmount = stopLoss - entryPrice;
                double takeProfit = entryPrice - (riskAmount * RiskRewardRatio);
                double volumeInLots = CalculatePositionSize(riskAmount);

                var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInLots,
                    TradeLabel, stopLoss, takeProfit);

                if (result.IsSuccessful)
                {
                    CurrentPosition = result.Position;
                    CurrentRiskAmount = riskAmount;
                    InitialStopLoss = stopLoss;
                    TradeLocked = true;
                    ChaosGuardActivated = false;

                    Print($"🔴 فروش | Entry={entryPrice:F5} | SL={stopLoss:F5} | TP={takeProfit:F5} | Vol={volumeInLots}");
                    Print($"📊 Box Height: {boxHeightInPips:F1} pips | Risk: {riskAmount / Symbol.PipSize:F1} pips");
                }
                else
                {
                    Print($"❌ خطا در فروش: {result.Error}");
                }
            }
        }

        #endregion

        #region مدیریت پوزیشن (Position Management - Bug Fixed!)

        private void ManagePosition(double currentPrice, double currentHfd)
        {
            if (CurrentPosition == null || CurrentPosition.IsClosed)
                return;

            double pnl = CurrentPosition.TradeType == TradeType.Buy
                ? currentPrice - CurrentPosition.EntryPrice
                : CurrentPosition.EntryPrice - currentPrice;

            double pnlInR = pnl / CurrentRiskAmount;

            // ═══════════════════════════════════════════════════════════
            // واکنش به بازگشت آشوب (FIX: باگ خودزنی رفع شد!)
            // ═══════════════════════════════════════════════════════════
            if (currentHfd > ChaosThreshold && !ChaosGuardActivated)
            {
                // شرط جدید: فقط اگر ریسک باز وجود دارد
                bool isStopInDanger = CurrentPosition.TradeType == TradeType.Buy
                    ? CurrentPosition.StopLoss.Value < CurrentPosition.EntryPrice
                    : CurrentPosition.StopLoss.Value > CurrentPosition.EntryPrice;

                if (pnlInR >= 0.5 && isStopInDanger)
                {
                    ModifyPosition(CurrentPosition, CurrentPosition.EntryPrice,
                        CurrentPosition.TakeProfit);

                    ChaosGuardActivated = true; // فقط یکبار اجرا شود
                    Print($"⚠️ آشوب برگشت! SL به Breakeven منتقل شد (سود: {pnlInR:F2}R)");
                }
            }

            // ═══════════════════════════════════════════════════════════
            // تریلینگ استاپ پله‌ای
            // ═══════════════════════════════════════════════════════════

            // مرحله 1: Breakeven (1R)
            if (pnlInR >= 1.0 && !ChaosGuardActivated)
            {
                double newSL = CurrentPosition.EntryPrice;

                if ((CurrentPosition.TradeType == TradeType.Buy && newSL > CurrentPosition.StopLoss.Value) ||
                    (CurrentPosition.TradeType == TradeType.Sell && newSL < CurrentPosition.StopLoss.Value))
                {
                    ModifyPosition(CurrentPosition, newSL, CurrentPosition.TakeProfit);
                    ChaosGuardActivated = true; // دیگر به عقب نمی‌رود
                    Print($"📍 Breakeven (1R) | New SL={newSL:F5}");
                }
            }

            // مرحله 2: Trail to 1R (عند 2R)
            if (pnlInR >= 2.0)
            {
                double newSL = CurrentPosition.TradeType == TradeType.Buy
                    ? CurrentPosition.EntryPrice + CurrentRiskAmount
                    : CurrentPosition.EntryPrice - CurrentRiskAmount;

                if ((CurrentPosition.TradeType == TradeType.Buy && newSL > CurrentPosition.StopLoss.Value) ||
                    (CurrentPosition.TradeType == TradeType.Sell && newSL < CurrentPosition.StopLoss.Value))
                {
                    ModifyPosition(CurrentPosition, newSL, CurrentPosition.TakeProfit);
                    Print($"📈 Trail to 1R (2R reached) | New SL={newSL:F5}");
                }
            }

            // مرحله 3: Trail to 2R (عند 3R)
            if (pnlInR >= 3.0)
            {
                double newSL = CurrentPosition.TradeType == TradeType.Buy
                    ? CurrentPosition.EntryPrice + (2 * CurrentRiskAmount)
                    : CurrentPosition.EntryPrice - (2 * CurrentRiskAmount);

                if ((CurrentPosition.TradeType == TradeType.Buy && newSL > CurrentPosition.StopLoss.Value) ||
                    (CurrentPosition.TradeType == TradeType.Sell && newSL < CurrentPosition.StopLoss.Value))
                {
                    ModifyPosition(CurrentPosition, newSL, CurrentPosition.TakeProfit);
                    Print($"🚀 Trail to 2R (3R reached) | New SL={newSL:F5}");
                }
            }
        }

        #endregion

        #region موتور گرافیکی (Visual Engine)

        private void UpdateVisuals()
        {
            if (!ShowGraphics || BoxHigh == null || BoxLow == null)
                return;

            // تعیین رنگ و استایل
            Color boxColor = IsBoxLocked ? Color.RoyalBlue : Color.Gray;
            int thickness = IsBoxLocked ? 2 : 1;
            LineStyle lineStyle = IsBoxLocked ? LineStyle.Solid : LineStyle.DotsRare;

            // رسم مستطیل باکس
            DateTime endTime = Server.Time.AddBars(TimeFrame, 5);
            Chart.DrawRectangle(BOX_NAME, BoxStartTime, BoxLow.Value, endTime, BoxHigh.Value,
                boxColor, thickness, lineStyle);

            // نمایش وضعیت
            if (IsBoxLocked)
            {
                string statusText = TradeLocked ? "TRADED" : "READY";
                Color statusColor = TradeLocked ? Color.Orange : Color.LimeGreen;
                Chart.DrawText(STATUS_NAME, statusText, Server.Time, BoxHigh.Value, statusColor);
            }
            else
            {
                Chart.RemoveObject(STATUS_NAME);
            }

            // رسم خطوط SL و TP (اگر معامله باز است)
            if (CurrentPosition != null && !CurrentPosition.IsClosed)
            {
                if (CurrentPosition.StopLoss.HasValue)
                {
                    Chart.DrawHorizontalLine(SL_LINE_NAME, CurrentPosition.StopLoss.Value,
                        Color.Red, 2, LineStyle.Solid);
                }

                if (CurrentPosition.TakeProfit.HasValue)
                {
                    Chart.DrawHorizontalLine(TP_LINE_NAME, CurrentPosition.TakeProfit.Value,
                        Color.Green, 2, LineStyle.Solid);
                }
            }
            else
            {
                Chart.RemoveObject(SL_LINE_NAME);
                Chart.RemoveObject(TP_LINE_NAME);
            }
        }

        #endregion

        #region محاسبات کمکی (Helper Methods)

        private double CalculatePositionSize(double riskAmount)
        {
            double riskDollars = Account.Balance * (RiskPercent / 100.0);
            double pipValue = Symbol.PipValue;
            double riskInPips = riskAmount / Symbol.PipSize;

            double volumeInUnits = riskDollars / (riskInPips * pipValue);
            double volumeInLots = Symbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            if (volumeInLots < Symbol.VolumeInUnitsMin)
                volumeInLots = Symbol.VolumeInUnitsMin;

            if (volumeInLots > Symbol.VolumeInUnitsMax)
                volumeInLots = Symbol.VolumeInUnitsMax;

            return volumeInLots;
        }

        #endregion

        #region رویدادهای پوزیشن (Position Events)

        protected override void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (args.Position.Label == TradeLabel)
            {
                Print($"✅ معامله باز شد: {args.Position.Id}");
                UpdateVisuals();
            }
        }

        protected override void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == TradeLabel)
            {
                Print($"🔚 معامله بسته شد: {args.Position.Id}");
                Print($"💰 سود/زیان: {args.Position.NetProfit:F2} | Reason: {args.Reason}");

                if (CurrentPosition != null && CurrentPosition.Id == args.Position.Id)
                {
                    CurrentPosition = null;
                    CurrentRiskAmount = 0;
                    ChaosGuardActivated = false;

                    // ریست فقط اگر سیکل آشوب جدید شروع نشده
                    // (باگ ریست زودهنگام رفع شد!)
                    if (!IsChaosActive && !IsBoxLocked)
                    {
                        ResetBoxState();
                    }
                }

                UpdateVisuals();
            }
        }

        #endregion

        #region توقف (OnStop)

        protected override void OnStop()
        {
            // پاک کردن تمام گرافیک‌ها
            Chart.RemoveObject(BOX_NAME);
            Chart.RemoveObject(STATUS_NAME);
            Chart.RemoveObject(SL_LINE_NAME);
            Chart.RemoveObject(TP_LINE_NAME);

            Print("═══════════════════════════════════════════════════════");
            Print("🛑 HIGUCHI CHAOS HUNTER v2.0 متوقف شد.");
            Print("═══════════════════════════════════════════════════════");
        }

        #endregion
    }
}
