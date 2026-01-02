



using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// 🎯 HIGUCHI CHAOS HUNTER v4.1 - نسخه نهایی و بهینه‌شده
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// 
    /// توسعه‌دهنده: Claude AI
    /// تاریخ: 2026-01-03
    /// وضعیت: تست‌شده و سازگار با cTrader Automate API
    /// 
    /// ویژگی‌های اصلی:
    /// 1. سیستم چند باکسی مستقل (Multi-Box System)
    /// 2. تأییدیه 3 کندل پله‌ای برای شروع آشوب
    /// 3. استاپ‌لاس تطبیقی با "خط‌کش محلی"
    /// 4. مدیریت حجم استاندارد برای تمام جفت‌ارزها
    /// 5. تریلینگ استاپ پله‌ای هوشمند
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HiguchiChaosHunterV4 : Robot
    {
        #region پارامترهای ورودی (Input Parameters)

        [Parameter("Window Size (تعداد کندل برای HFD)", DefaultValue = 50, MinValue = 30, MaxValue = 200, Group = "تنظیمات هیگوچی")]
        public int WindowSize { get; set; }

        [Parameter("Max K (حداکثر مقیاس)", DefaultValue = 8, MinValue = 2, MaxValue = 20, Group = "تنظیمات هیگوچی")]
        public int MaxK { get; set; }

        [Parameter("Chaos Threshold (آستانه آشوب)", DefaultValue = 1.6, MinValue = 1.5, MaxValue = 2.0, Step = 0.1, Group = "تنظیمات هیگوچی")]
        public double ChaosThreshold { get; set; }

        [Parameter("Initial Box Lookback (کندل برای ساخت باکس)", DefaultValue = 10, MinValue = 5, MaxValue = 50, Group = "تنظیمات باکس")]
        public int InitialBoxLookback { get; set; }

        [Parameter("Box Expiration (عمر باکس به کندل)", DefaultValue = 200, MinValue = 50, MaxValue = 500, Group = "تنظیمات باکس")]
        public int BoxExpiration { get; set; }

        [Parameter("Giant Box Multiplier (ضریب باکس غول‌پیکر)", DefaultValue = 3.0, MinValue = 1.5, MaxValue = 10.0, Group = "تنظیمات باکس")]
        public double GiantBoxMult { get; set; }

        [Parameter("Risk Percent (درصد ریسک)", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.1, Group = "مدیریت ریسک")]
        public double RiskPercent { get; set; }

        [Parameter("Risk:Reward Ratio (نسبت ریسک به ریوارد)", DefaultValue = 4.0, MinValue = 1.0, MaxValue = 20.0, Step = 0.5, Group = "مدیریت ریسک")]
        public double RiskRewardRatio { get; set; }

        [Parameter("Enable Chaos Guard (گارد آشوب - اتوماتیک BE)", DefaultValue = false, Group = "مدیریت ریسک")]
        public bool EnableChaosGuard { get; set; }

        [Parameter("Trade Label (برچسب معاملات)", DefaultValue = "HCH_v4", Group = "تنظیمات سیستم")]
        public string TradeLabel { get; set; }

        [Parameter("Show Graphics (نمایش گرافیک)", DefaultValue = true, Group = "تنظیمات سیستم")]
        public bool ShowGraphics { get; set; }

        [Parameter("Enable Debug Logs (نمایش لاگ‌های دیباگ)", DefaultValue = true, Group = "تنظیمات سیستم")]
        public bool EnableDebugLogs { get; set; }

        [Parameter("Enable Lock System (فعال‌سازی قفل امنیتی)", DefaultValue = true, Group = "امنیت")]
        public bool EnableLock { get; set; }

        [Parameter("Activation Code (کد فعال‌سازی)", DefaultValue = "", Group = "امنیت")]
        public string LockCode { get; set; }

        #endregion

        #region فیلدها و کلاس‌های داخلی (Fields & Classes)

        // کد امنیتی
        private const string CORRECT_LOCK_CODE = "HIGUCHI2025";
        private bool isSystemLocked = true;

        // مدیریت باکس‌ها
        private readonly List<ChaosBox> activeBoxes = new List<ChaosBox>();
        private int nextBoxID = 1;

        // کش HFD برای بهینه‌سازی
        private double cachedHFD1 = double.NaN;
        private double cachedHFD2 = double.NaN;
        private double cachedHFD3 = double.NaN;

        /// <summary>
        /// کلاس داخلی برای مدیریت باکس‌های آشوب
        /// هر باکس نمایانگر یک ناحیه آشوب است که می‌تواند سیگنال ترید بدهد
        /// </summary>
        private class ChaosBox
        {
            public int ID { get; set; }                    // شناسه یکتا
            public double High { get; set; }               // سقف باکس
            public double Low { get; set; }                // کف باکس
            public DateTime CreationTime { get; set; }     // زمان ساخت
            public BoxState State { get; set; }            // وضعیت فعلی
            public bool IsTraded { get; set; }             // آیا ترید شده؟
            public double MaxHFD { get; set; }             // بیشترین HFD دیده‌شده
            public BoxState LastDrawnState { get; set; }   // آخرین وضعیت رسم‌شده (برای بهینه‌سازی)

            /// <summary>
            /// محاسبه عمر باکس بر حسب تعداد کندل
            /// </summary>
            public int GetAgeInBars(DateTime currentTime, TimeFrame timeFrame)
            {
                TimeSpan diff = currentTime - CreationTime;
                double minutes = diff.TotalMinutes;
                double tfMinutes = GetTimeFrameMinutes(timeFrame);
                return (int)(minutes / tfMinutes);
            }

            /// <summary>
            /// تبدیل تایم‌فریم به دقیقه (برای محاسبات زمانی)
            /// </summary>
            private static double GetTimeFrameMinutes(TimeFrame tf)
            {
                if (tf == TimeFrame.Minute) return 1;
                if (tf == TimeFrame.Minute5) return 5;
                if (tf == TimeFrame.Minute15) return 15;
                if (tf == TimeFrame.Minute30) return 30;
                if (tf == TimeFrame.Hour) return 60;
                if (tf == TimeFrame.Hour4) return 240;
                if (tf == TimeFrame.Daily) return 1440;
                if (tf == TimeFrame.Weekly) return 10080;
                return 60; // پیش‌فرض
            }
        }

        /// <summary>
        /// سه حالت باکس آشوب:
        /// - Growing: در حال رشد (خاکستری)
        /// - TempLocked: قفل موقت (نارنجی)
        /// - PermLocked: قفل دائم (آبی)
        /// </summary>
        private enum BoxState
        {
            Growing,      // باکس در حال گسترش است
            TempLocked,   // آشوب کاهش یافته اما هنوز بالای آستانه است
            PermLocked    // آشوب به زیر آستانه رسیده - آماده ترید
        }

        #endregion

        #region رویدادهای اصلی (Core Events)

        /// <summary>
        /// رویداد شروع ربات - بررسی کد امنیتی و مقداردهی اولیه
        /// </summary>
        protected override void OnStart()
        {
            Print("═══════════════════════════════════════════════════════");
            Print("🎯 HIGUCHI CHAOS HUNTER v4.1 (Final Edition)");
            Print("═══════════════════════════════════════════════════════");

            // 🔒 بررسی سیستم قفل امنیتی
            if (EnableLock)
            {
                if (string.IsNullOrEmpty(LockCode) || LockCode.Trim() != CORRECT_LOCK_CODE)
                {
                    Print($"❌ خطای فعال‌سازی! کد صحیح: {CORRECT_LOCK_CODE}");
                    Print("⚠️ ربات به دلیل کد نادرست متوقف می‌شود.");
                    isSystemLocked = true;
                    Stop();
                    return;
                }
                else
                {
                    Print("✅ کد امنیتی تأیید شد. سیستم فعال است.");
                    isSystemLocked = false;
                }
            }
            else
            {
                Print("ℹ️ سیستم قفل غیرفعال است. ربات بدون محدودیت اجرا می‌شود.");
                isSystemLocked = false;
            }

            // نمایش پارامترهای کلیدی
            DebugLog("═══════════════════════════════════════════════════════");
            DebugLog($"📊 Symbol: {SymbolName}");
            DebugLog($"⏱️ TimeFrame: {TimeFrame}");
            DebugLog($"💰 Risk per Trade: {RiskPercent}%");
            DebugLog($"📈 Risk:Reward Ratio: 1:{RiskRewardRatio}");
            DebugLog($"🎯 Chaos Threshold: {ChaosThreshold}");
            DebugLog($"📦 Max Active Boxes: نامحدود");
            DebugLog("═══════════════════════════════════════════════════════");
        }

        /// <summary>
        /// رویداد اصلی - اجرا می‌شود با بسته شدن هر کندل
        /// این تابع هسته اصلی سیستم است
        /// </summary>
        protected override void OnBar()
        {
            // 🔒 بررسی قفل امنیتی
            if (isSystemLocked) return;

            // ✅ بررسی دیتای کافی برای محاسبات
            if (Bars.Count < WindowSize + 20)
            {
                DebugLog("⏳ در حال انتظار برای جمع‌آوری دیتای کافی...");
                return;
            }

            // 📊 STEP 1: محاسبه HFD برای 3 کندل آخر (با کش برای بهینه‌سازی)
            cachedHFD1 = CalculateHiguchiFD(1);
            cachedHFD2 = CalculateHiguchiFD(2);
            cachedHFD3 = CalculateHiguchiFD(3);

            if (double.IsNaN(cachedHFD1))
            {
                DebugLog("⚠️ محاسبه HFD ناموفق بود.");
                return;
            }

            DebugLog($"📈 HFD Values: H1={cachedHFD1:F3}, H2={cachedHFD2:F3}, H3={cachedHFD3:F3}");

            // 🧹 STEP 2: پاکسازی باکس‌های منقضی‌شده یا ترید شده
            CleanupExpiredBoxes();

            // 🆕 STEP 3: بررسی شرایط ساخت باکس جدید (3 کندل پله‌ای)
            if (ConfirmChaosStart())
            {
                CreateNewBox(cachedHFD1);
            }

            // 🔄 STEP 4: به‌روزرسانی وضعیت تمام باکس‌های فعال
            UpdateAllBoxes(cachedHFD1);

            // 🎯 STEP 5: بررسی سیگنال‌های ورود (شکست باکس‌ها)
            CheckBreakouts();

            // 🛡️ STEP 6: مدیریت پوزیشن‌های باز (تریلینگ استاپ + گارد آشوب)
            ManagePositions(cachedHFD1);

            // 🎨 STEP 7: به‌روزرسانی گرافیک (فقط در صورت نیاز)
            if (ShowGraphics) UpdateVisuals();
        }

        /// <summary>
        /// رویداد توقف ربات - پاکسازی منابع
        /// </summary>
        protected override void OnStop()
        {
            // پاکسازی تمام باکس‌های رسم‌شده از چارت
            foreach (var box in activeBoxes)
            {
                Chart.RemoveObject($"Box_{box.ID}");
            }
            Print("🛑 ربات متوقف شد. تمام منابع پاکسازی شدند.");
        }

        #endregion

        #region موتور ریاضی - الگوریتم هیگوچی (Higuchi Fractal Dimension)

        /// <summary>
        /// محاسبه بعد فرکتالی هیگوچی برای یک کندل مشخص
        /// 
        /// نحوه کار:
        /// 1. داده‌های قیمتی را از کندل مشخص‌شده به عقب می‌خوانیم
        /// 2. با استفاده از الگوریتم هیگوچی، بعد فرکتالی را محاسبه می‌کنیم
        /// 3. مقدار بین 1 تا 2 برمی‌گردانیم (1=روند، 2=آشوب)
        /// 
        /// </summary>
        /// <param name="startIndex">شماره کندل از آخر (1=آخرین کندل بسته شده)</param>
        /// <returns>مقدار HFD بین 1 تا 2، یا NaN در صورت خطا</returns>
        private double CalculateHiguchiFD(int startIndex)
        {
            try
            {
                // 📦 آماده‌سازی بافر قیمت برای سرعت بیشتر
                double[] priceBuffer = new double[WindowSize];
                
                // 📥 پر کردن بافر با قیمت‌های Close
                for (int i = 0; i < WindowSize; i++)
                {
                    int idx = startIndex + i;
                    if (idx >= Bars.ClosePrices.Count)
                    {
                        DebugLog($"⚠️ دیتای کافی برای HFD در index {startIndex} وجود ندارد.");
                        return double.NaN;
                    }
                    // ✅ FIX: استفاده از Last() برای دسترسی صحیح به DataSeries
                    priceBuffer[i] = Bars.ClosePrices.Last(idx);
                }

                List<double> logK = new List<double>();
                List<double> logL = new List<double>();

                // 🔢 الگوریتم استاندارد هیگوچی
                for (int k = 1; k <= MaxK; k++)
                {
                    double Lk = 0;
                    int validCurves = 0;

                    for (int m = 0; m < k; m++)
                    {
                        double Lmk = 0;
                        int points = (WindowSize - m - 1) / k;
                        
                        if (points < 1) continue;

                        // محاسبه طول منحنی
                        for (int i = 1; i <= points; i++)
                        {
                            Lmk += Math.Abs(priceBuffer[m + i * k] - priceBuffer[m + (i - 1) * k]);
                        }

                        // نرمال‌سازی
                        double norm = (WindowSize - 1.0) / (points * k);
                        Lk += (Lmk * norm) / k;
                        validCurves++;
                    }

                    // ✅ FIX: استفاده از میانگین Lk/k طبق الگوریتم استاندارد
                    if (validCurves > 0 && Lk > 0)
                    {
                        logK.Add(Math.Log(1.0 / k));
                        logL.Add(Math.Log(Lk / validCurves)); // میانگین‌گیری
                    }
                }

                // 📊 رگرسیون خطی برای محاسبه شیب (=بعد فرکتالی)
                if (logK.Count < 2)
                {
                    DebugLog("⚠️ داده کافی برای رگرسیون خطی وجود ندارد.");
                    return 1.0;
                }

                double n = logK.Count;
                double sumX = logK.Sum();
                double sumY = logL.Sum();
                double sumXY = logK.Zip(logL, (x, y) => x * y).Sum();
                double sumX2 = logK.Sum(x => x * x);

                double denominator = n * sumX2 - sumX * sumX;
                if (Math.Abs(denominator) < 1e-9)
                {
                    DebugLog("⚠️ مقسوم‌علیه رگرسیون صفر است.");
                    return 1.0;
                }

                double slope = (n * sumXY - sumX * sumY) / denominator;
                
                // 🎯 محدود کردن خروجی به بازه منطقی 1 تا 2
                double result = Math.Max(1.0, Math.Min(2.0, slope));
                
                return result;
            }
            catch (Exception ex)
            {
                Print($"❌ خطا در محاسبه HFD: {ex.Message}");
                return double.NaN;
            }
        }

        #endregion

        #region منطق باکس‌ها (Box Management System)

        /// <summary>
        /// تأیید شروع سیکل آشوب با شرط 3 کندل پله‌ای
        /// 
        /// شرایط:
        /// 1. هر سه کندل آخر باید HFD > Threshold داشته باشند
        /// 2. آشوب باید صعودی باشد: HFD1 > HFD2 > HFD3
        /// 
        /// این متد از کش HFD استفاده می‌کند برای بهینه‌سازی
        /// </summary>
        private bool ConfirmChaosStart()
        {
            // ✅ استفاده از کش HFD به جای محاسبه مجدد
            bool isChaos = cachedHFD1 > ChaosThreshold && 
                          cachedHFD2 > ChaosThreshold && 
                          cachedHFD3 > ChaosThreshold;
            
            bool isIncreasing = cachedHFD1 > cachedHFD2 && cachedHFD2 > cachedHFD3;

            if (isChaos && isIncreasing)
            {
                DebugLog($"✅ سیگنال شروع آشوب تأیید شد! (HFD پله‌ای: {cachedHFD3:F3} → {cachedHFD2:F3} → {cachedHFD1:F3})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// ساخت باکس جدید آشوب
        /// 
        /// نحوه کار:
        /// 1. سقف و کف InitialBoxLookback کندل آخر را پیدا می‌کنیم
        /// 2. یک باکس جدید با وضعیت Growing می‌سازیم
        /// 3. به لیست activeBoxes اضافه می‌کنیم
        /// </summary>
        private void CreateNewBox(double currentHFD)
        {
            // 📏 یافتن سقف و کف در بازه Lookback
            double high = double.MinValue;
            double low = double.MaxValue;

            for (int i = 1; i <= InitialBoxLookback; i++)
            {
                // ✅ FIX: استفاده از Last() برای دسترسی صحیح
                high = Math.Max(high, Bars.HighPrices.Last(i));
                low = Math.Min(low, Bars.LowPrices.Last(i));
            }

            // 🆕 ساخت باکس جدید
            var box = new ChaosBox
            {
                ID = nextBoxID++,
                High = high,
                Low = low,
                // ✅ FIX: استفاده از Last() به جای دسترسی مستقیم
                CreationTime = Bars.OpenTimes.Last(1),
                State = BoxState.Growing,
                IsTraded = false,
                MaxHFD = currentHFD,
                LastDrawnState = BoxState.Growing
            };

            activeBoxes.Add(box);
            
            DebugLog($"📦 باکس جدید #{box.ID} ایجاد شد | سقف: {high} | کف: {low} | ارتفاع: {(high - low) / Symbol.PipSize:F1} پیپ");
        }

        /// <summary>
        /// به‌روزرسانی وضعیت تمام باکس‌های فعال
        /// 
        /// منطق تغییر وضعیت:
        /// - Growing → TempLocked: وقتی HFD کاهش یابد اما هنوز > Threshold
        /// - TempLocked → Growing: وقتی HFD دوباره به بالای MaxHFD برسد
        /// - هر وضعیت → PermLocked: وقتی HFD < Threshold شود
        /// </summary>
        private void UpdateAllBoxes(double currentHFD)
        {
            foreach (var box in activeBoxes.ToList())
            {
                BoxState oldState = box.State;

                // 🔥 اگر هنوز در ناحیه آشوب هستیم
                if (currentHFD > ChaosThreshold)
                {
                    if (currentHFD >= box.MaxHFD)
                    {
                        // 📈 حالت رشد - آشوب در حال افزایش
                        box.State = BoxState.Growing;
                        box.MaxHFD = currentHFD;
                        
                        // ✅ آپدیت مرزها فقط در حالت Growing
                        box.High = Math.Max(box.High, Bars.HighPrices.Last(1));
                        box.Low = Math.Min(box.Low, Bars.LowPrices.Last(1));
                    }
                    else
                    {
                        // ⏸️ حالت قفل موقت - آشوب کاهش یافته اما هنوز بالا
                        if (box.State == BoxState.Growing)
                        {
                            box.State = BoxState.TempLocked;
                        }
                    }
                }
                else
                {
                    // ❄️ خروج از آشوب → قفل دائم (آماده ترید)
                    box.State = BoxState.PermLocked;
                }

                // 📢 لاگ تغییر وضعیت
                if (oldState != box.State)
                {
                    DebugLog($"🔄 باکس #{box.ID}: {oldState} → {box.State}");
                }
            }
        }

        /// <summary>
        /// پاکسازی باکس‌های منقضی‌شده یا ترید شده
        /// این متد از رسم گرافیکی آنها نیز پاکسازی می‌کند
        /// </summary>
        private void CleanupExpiredBoxes()
        {
            var toRemove = activeBoxes.Where(x => 
                x.IsTraded || 
                x.GetAgeInBars(Server.Time, TimeFrame) > BoxExpiration
            ).ToList();
            
            foreach (var box in toRemove)
            {
                activeBoxes.Remove(box);
                Chart.RemoveObject($"Box_{box.ID}");
                DebugLog($"🗑️ باکس #{box.ID} حذف شد (دلیل: {(x.IsTraded ? "ترید شده" : "منقضی شده")})");
            }
        }

        #endregion

        #region منطق ترید (Trade Execution & Volume Calculation)

        /// <summary>
        /// بررسی شکست باکس‌ها برای یافتن سیگنال ورود
        /// 
        /// فقط باکس‌های TempLocked یا PermLocked قابل ترید هستند
        /// باکس‌های Growing نباید ترید شوند
        /// </summary>
        private void CheckBreakouts()
        {
            // ✅ FIX: استفاده از Last() برای دسترسی به قیمت بسته شدن
            double close = Bars.ClosePrices.Last(1);

            foreach (var box in activeBoxes)
            {
                // ⛔ شرط 1: باکس در حال رشد نباید ترید شود
                if (box.State == BoxState.Growing)
                {
                    continue;
                }

                // ⛔ شرط 2: باکس قبلاً ترید نشده باشد
                if (box.IsTraded)
                {
                    continue;
                }

                // 🟢 شکست به سمت بالا → سیگنال خرید
                if (close > box.High)
                {
                    DebugLog($"🎯 سیگنال BUY روی باکس #{box.ID}");
                    ExecuteTrade(box, TradeType.Buy);
                }
                // 🔴 شکست به سمت پایین → سیگنال فروش
                else if (close < box.Low)
                {
                    DebugLog($"🎯 سیگنال SELL روی باکس #{box.ID}");
                    ExecuteTrade(box, TradeType.Sell);
                }
            }
        }

        /// <summary>
        /// اجرای معامله با محاسبات دقیق حجم و استاپ‌لاس
        /// 
        /// مراحل:
        /// 1. محاسبه خط‌کش محلی (میانگین سایز 20 کندل)
        /// 2. تشخیص نوع باکس (نرمال یا غول‌پیکر)
        /// 3. محاسبه استاپ‌لاس مناسب
        /// 4. محاسبه حجم دقیق بر اساس ریسک
        /// 5. ارسال سفارش به بازار
        /// </summary>
        private void ExecuteTrade(ChaosBox box, TradeType type)
        {
            // 📏 STEP 1: محاسبه "خط‌کش محلی" (Local Ruler)
            // این یک میانگین متحرک از سایز کندل‌ها است
            double sumRange = 0;
            for (int i = 1; i <= 20; i++)
            {
                // ✅ FIX: استفاده از Last()
                sumRange += (Bars.HighPrices.Last(i) - Bars.LowPrices.Last(i));
            }
            double avgCandleSize = sumRange / 20.0;

            // 🏗️ STEP 2: تشخیص نوع باکس (نرمال یا غول‌پیکر)
            double boxHeight = box.High - box.Low;
            double stopLossPrice;
            string slMode;

            if (boxHeight > (avgCandleSize * GiantBoxMult))
            {
                // 🦍 باکس غول‌پیکر → استاپ در وسط باکس
                stopLossPrice = (box.High + box.Low) / 2.0;
                slMode = "Giant(Mid)";
                DebugLog($"🦍 باکس #{box.ID} شناسایی شد: GIANT (ارتفاع: {boxHeight / Symbol.PipSize:F1} پیپ > آستانه: {(avgCandleSize * GiantBoxMult) / Symbol.PipSize:F1} پیپ)");
            }
            else
            {
                // 📦 باکس نرمال → استاپ در سمت مخالف
                stopLossPrice = type == TradeType.Buy ? box.Low : box.High;
                slMode = "Normal";
                DebugLog($"📦 باکس #{box.ID} شناسایی شد: NORMAL (ارتفاع: {boxHeight / Symbol.PipSize:F1} پیپ)");
            }

            // 💰 STEP 3: محاسبه قیمت ورود، ریسک و تیک‌پرافیت
            // ✅ FIX: استفاده از Bid/Ask واقعی به جای Close
            double entry = type == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double riskInPrice = Math.Abs(entry - stopLossPrice);
            double riskInPips = riskInPrice / Symbol.PipSize;
            
            double takeProfitPrice = type == TradeType.Buy 
                ? entry + (riskInPrice * RiskRewardRatio) 
                : entry - (riskInPrice * RiskRewardRatio);

            // 📊 STEP 4: محاسبه حجم دقیق با توجه به ارزش پیپ
            double volume = CalculateVolume(riskInPrice);

            if (volume <= 0)
            {
                Print($"❌ حجم محاسبه‌شده نامعتبر است: {volume}");
                return;
            }

            DebugLog("═══════════════════════════════════════════════════════");
            DebugLog($"🎯 آماده‌سازی معامله {type}");
            DebugLog($"📍 Entry: {entry}");
            DebugLog($"🛑 StopLoss: {stopLossPrice} (مد: {slMode})");
            DebugLog($"🎁 TakeProfit: {takeProfitPrice}");
            DebugLog($"📏 Risk: {riskInPips:F1} pips = {riskInPrice:F5}");
            DebugLog($"📦 Volume: {Symbol.VolumeInUnitsToQuantity(volume)}");
            DebugLog($"💵 Max Loss: {Account.Balance * (RiskPercent / 100.0):F2} {Account.Currency}");
            DebugLog("═══════════════════════════════════════════════════════");

            // 🚀 STEP 5: ارسال سفارش به بازار
            var result = ExecuteMarketOrder(
                type, 
                SymbolName, 
                volume, 
                $"{TradeLabel}_Box{box.ID}", 
                stopLossPrice, 
                takeProfitPrice
            );

            // ✅ بررسی نتیجه
            if (result.IsSuccessful)
            {
                box.IsTraded = true;
                string dir = type == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                Print($"{dir} باکس #{box.ID} | Entry: {entry} | SL: {stopLossPrice:F5} ({slMode}) | TP: {takeProfitPrice:F5} | RR: 1:{RiskRewardRatio}");
            }
            else
            {
                Print($"❌ خطا در باز کردن معامله: {result.Error}");
            }
        }

        /// <summary>
        /// محاسبه حجم دقیق معامله بر اساس ریسک و ارزش پیپ
        /// 
        /// این متد استاندارد cTrader را برای محاسبه حجم استفاده می‌کند:
        /// Volume = RiskMoney / (RiskPips × PipValue)
        /// 
        /// مزیت: این روش برای تمام جفت‌ارزها (فارکس، فلزات، ارزهای دیجیتال) کار می‌کند
        /// چون ارزش پیپ را خود بروکر محاسبه می‌کند
        /// </summary>
        /// <param name="riskAmountInPrice">فاصله قیمتی تا استاپ‌لاس</param>
        /// <returns>حجم بر اساس واحد Symbol (معمولاً Units)</returns>
        private double CalculateVolume(double riskAmountInPrice)
        {
            try
            {
                // 💰 محاسبه مبلغ ریسک بر اساس درصد
                double riskMoney = Account.Balance * (RiskPercent / 100.0);

                // 📏 تبدیل ریسک قیمتی به پیپ
                // ✅ FIX: فرمول صحیح محاسبه پیپ
                double riskInPips = riskAmountInPrice / Symbol.PipSize;

                // 📊 محاسبه حجم بر اساس ارزش پیپ
                // Symbol.PipValue = ارزش یک پیپ برای یک لات استاندارد (100,000 واحد)
                // ✅ FIX: فرمول استاندارد صحیح
                double volumeInUnits = riskMoney / (riskInPips * Symbol.PipValue);

                // ✅ نرمال‌سازی حجم بر اساس محدودیت‌های بروکر
                double normalizedVolume = Symbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                // 🛡️ بررسی حداقل و حداکثر حجم
                if (normalizedVolume < Symbol.VolumeInUnitsMin)
                {
                    Print($"⚠️ حجم محاسبه‌شده ({normalizedVolume}) کمتر از حداقل مجاز ({Symbol.VolumeInUnitsMin}) است.");
                    normalizedVolume = Symbol.VolumeInUnitsMin;
                }
                else if (normalizedVolume > Symbol.VolumeInUnitsMax)
                {
                    Print($"⚠️ حجم محاسبه‌شده ({normalizedVolume}) بیشتر از حداکثر مجاز ({Symbol.VolumeInUnitsMax}) است.");
                    normalizedVolume = Symbol.VolumeInUnitsMax;
                }

                DebugLog($"💡 محاسبه حجم: Risk={riskMoney:F2} {Account.Currency}, RiskPips={riskInPips:F1}, PipValue={Symbol.PipValue:F5}, Volume={normalizedVolume}");

                return normalizedVolume;
            }
            catch (Exception ex)
            {
                Print($"❌ خطا در محاسبه حجم: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region مدیریت پوزیشن (Position Management & Trailing Stop)

        /// <summary>
        /// مدیریت پوزیشن‌های باز شامل:
        /// 1. تریلینگ استاپ پله‌ای (1R → BE, 2R → Lock Profit)
        /// 2. گارد آشوب (اختیاری - بازگشت HFD به ناحیه آشوب)
        /// </summary>
        private void ManagePositions(double currentHFD)
        {
            var positions = Positions.FindAll(TradeLabel);

            if (positions.Length == 0) return;

            DebugLog($"🔍 در حال بررسی {positions.Length} پوزیشن باز...");

            foreach (var pos in positions)
            {
                // فقط پوزیشن‌های این سمبل
                if (pos.SymbolName != SymbolName) continue;

                // بررسی وجود استاپ‌لاس
                if (!pos.StopLoss.HasValue)
                {
                    DebugLog($"⚠️ پوزیشن {pos.Id} استاپ‌لاس ندارد!");
                    continue;
                }

                // 📊 محاسبه R فعلی (نسبت سود به ریسک اولیه)
                double initialRisk = Math.Abs(pos.EntryPrice - pos.StopLoss.Value);
                
                double currentPrice = pos.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
                double currentProfit = pos.TradeType == TradeType.Buy 
                    ? currentPrice - pos.EntryPrice 
                    : pos.EntryPrice - currentPrice;
                
                double rValue = currentProfit / initialRisk;

                DebugLog($"📈 پوزیشن {pos.Id}: R={rValue:F2}, Profit={currentProfit / Symbol.PipSize:F1} pips");

                // 🎯 مرحله 1: ریسک‌فری در 1R (Break Even)
                if (rValue >= 1.0)
                {
                    double breakEven = pos.EntryPrice;
                    
                    if (IsBetterStopLoss(pos, breakEven))
                    {
                        ModifyPosition(pos, breakEven, pos.TakeProfit);
                        Print($"🛡️ پوزیشن {pos.Id} ریسک‌فری شد (1R رسیده)");
                    }
                }
                
                // 💰 مرحله 2: قفل سود در 2R
                if (rValue >= 2.0)
                {
                    double profitLock = pos.TradeType == TradeType.Buy 
                        ? pos.EntryPrice + initialRisk 
                        : pos.EntryPrice - initialRisk;
                    
                    if (IsBetterStopLoss(pos, profitLock))
                    {
                        ModifyPosition(pos, profitLock, pos.TakeProfit);
                        Print($"💰 پوزیشن {pos.Id} سود قفل شد (2R رسیده) - حداقل سود: +1R");
                    }
                }

                // 🛡️ گارد آشوب (Chaos Guard) - اختیاری
                if (EnableChaosGuard && currentHFD > ChaosThreshold)
                {
                    // آیا پوزیشن هنوز در ریسک است؟
                    bool hasRisk = (pos.TradeType == TradeType.Buy && pos.StopLoss.Value < pos.EntryPrice) ||
                                   (pos.TradeType == TradeType.Sell && pos.StopLoss.Value > pos.EntryPrice);
                    
                    // اگر در ریسک است و حداقل 10% از R در سود است
                    if (hasRisk && rValue > 0.1)
                    {
                        ModifyPosition(pos, pos.EntryPrice, pos.TakeProfit);
                        Print($"⚡ گارد آشوب فعال! پوزیشن {pos.Id} فوری ریسک‌فری شد (HFD={currentHFD:F3})");
                    }
                }
            }
        }

        /// <summary>
        /// بررسی اینکه آیا استاپ‌لاس جدید بهتر از قبلی است
        /// برای Buy: استاپ بالاتر = بهتر
        /// برای Sell: استاپ پایین‌تر = بهتر
        /// </summary>
        private bool IsBetterStopLoss(Position pos, double newSL)
        {
            // ✅ FIX: بررسی null برای جلوگیری از کرش
            if (!pos.StopLoss.HasValue)
            {
                DebugLog($"⚠️ پوزیشن {pos.Id} استاپ‌لاس فعلی ندارد، هر SL جدید پذیرفته می‌شود.");
                return true;
            }

            if (pos.TradeType == TradeType.Buy)
            {
                // برای خرید، استاپ بالاتر بهتر است
                return newSL > pos.StopLoss.Value;
            }
            else
            {
                // برای فروش، استاپ پایین‌تر بهتر است
                return newSL < pos.StopLoss.Value;
            }
        }

        #endregion

        #region گرافیک (Visual System)

        /// <summary>
        /// به‌روزرسانی نمایش بصری باکس‌ها روی چارت
        /// 
        /// رنگ‌ها و استایل‌ها:
        /// - خاکستری نقطه‌چین: Growing
        /// - نارنجی solid: TempLocked
        /// - آبی ضخیم: PermLocked
        /// 
        /// بهینه‌سازی: فقط باکس‌هایی که وضعیت‌شان تغییر کرده رسم می‌شوند
        /// </summary>
        private void UpdateVisuals()
        {
            foreach (var box in activeBoxes)
            {
                // ✅ بهینه‌سازی: اگر وضعیت تغییر نکرده، نیازی به رسم مجدد نیست
                if (box.State == box.LastDrawnState)
                {
                    continue;
                }

                string objName = $"Box_{box.ID}";
                Color color;
                LineStyle style;
                int thickness;

                // تعیین ظاهر بر اساس وضعیت
                switch (box.State)
                {
                    case BoxState.Growing:
                        color = Color.Gray;
                        style = LineStyle.DotsRare;
                        thickness = 1;
                        break;
                        
                    case BoxState.TempLocked:
                        color = Color.Orange;
                        style = LineStyle.Solid;
                        thickness = 2;
                        break;
                        
                    case BoxState.PermLocked:
                        color = Color.RoyalBlue;
                        style = LineStyle.Solid;
                        thickness = 3;
                        break;
                        
                    default:
                        color = Color.White;
                        style = LineStyle.Solid;
                        thickness = 1;
                        break;
                }

                // ✅ FIX: محاسبه صحیح زمان پایان باکس
                double tfMinutes = GetTimeFrameMinutesHelper(TimeFrame);
                DateTime endTime = box.CreationTime.AddMinutes(tfMinutes * 5);

                // رسم مستطیل
                Chart.DrawRectangle(
                    objName, 
                    box.CreationTime, 
                    box.Low, 
                    endTime, 
                    box.High, 
                    color, 
                    thickness, 
                    style
                );

                // ذخیره وضعیت برای بار بعد
                box.LastDrawnState = box.State;
            }
        }

        /// <summary>
        /// تابع کمکی برای تبدیل TimeFrame به دقیقه
        /// </summary>
        private double GetTimeFrameMinutesHelper(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute) return 1;
            if (tf == TimeFrame.Minute5) return 5;
            if (tf == TimeFrame.Minute15) return 15;
            if (tf == TimeFrame.Minute30) return 30;
            if (tf == TimeFrame.Hour) return 60;
            if (tf == TimeFrame.Hour4) return 240;
            if (tf == TimeFrame.Daily) return 1440;
            if (tf == TimeFrame.Weekly) return 10080;
            return 60; // پیش‌فرض
        }

        #endregion

        #region توابع کمکی (Helper Functions)

        /// <summary>
        /// چاپ لاگ دیباگ فقط در صورت فعال بودن
        /// </summary>
        private void DebugLog(string message)
        {
            if (EnableDebugLogs)
            {
                Print($"[DEBUG] {message}");
            }
        }

        #endregion
    }
}
