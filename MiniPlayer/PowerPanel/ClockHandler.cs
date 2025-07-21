using System.Windows.Controls;  // 需要這個來使用 TextBlock
using System.Windows.Threading; // 需要這個來使用 DispatcherTimer

namespace MiniPlayer
{

    // 你可以創建一個單獨的類來處理時鐘邏輯，而不是直接放在 MainWindow 的部分類中
    // 這樣更符合模組化設計
    public class ClockHandler
    {
        private DispatcherTimer? _timer;
        private TextBlock _clockDisplay; // 假設 PowerPanelClock 是一個 TextBlock
        private Dictionary<DayOfWeek, String> japaneseWeekdays = new Dictionary<DayOfWeek, string>
                {
                    { DayOfWeek.Sunday,    "日" },
                    { DayOfWeek.Monday,    "月" },
                    { DayOfWeek.Tuesday,   "火" },
                    { DayOfWeek.Wednesday, "水" },
                    { DayOfWeek.Thursday,  "木" },
                    { DayOfWeek.Friday,    "金" },
                    { DayOfWeek.Saturday,  "土" },
                };


        // 建構函式，接收一個 TextBlock 來顯示時間
        public ClockHandler(TextBlock clockDisplay)
        {
            _clockDisplay = clockDisplay;
            InitializeClock();
        }

        private void InitializeClock()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(60); // 每分鐘更新一次
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // 初始化時立即更新一次時間，避免等待一分鐘才顯示
            _clockDisplay.Text = $"{japaneseWeekdays[DateTime.Now.DayOfWeek]} {DateTime.Now.ToString("HH:mm")}";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _clockDisplay.Text = $"{japaneseWeekdays[DateTime.Now.DayOfWeek]} {DateTime.Now.ToString("HH:mm")}";
        }

        // 當需要停止時鐘時呼叫此方法
        public void StopClock()
        {
            if (_timer != null && _timer.IsEnabled)
            {
                _timer?.Stop();
            }
        }
    }
}
