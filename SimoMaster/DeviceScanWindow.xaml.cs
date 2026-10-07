using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;   // untuk DataGridRowEventArgs
using System.Windows.Media;      // untuk Brushes

namespace SimoMaster
{
    public partial class DeviceScanWindow : Window
    {
        public ObservableCollection<ScanResult> Results { get; set; }

        public DeviceScanWindow()
        {
            InitializeComponent();

            Results = new ObservableCollection<ScanResult>();
            dgScan.ItemsSource = Results;
        }

        public void SetMode(string mode)
        {
            txtMode.Text = $"Mode: {mode}";
        }

        public void AddResult(ScanResult result)
        {
            Dispatcher.Invoke(() =>
            {
                Results.Add(result);
            });
        }

        public void SetSummary(string summary)
        {
            Dispatcher.Invoke(() =>
            {
                txtSummary.Text = summary;
            });
        }

        private void dgScan_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var item = e.Row.Item as ScanResult;
            if (item == null) return;

            if (item.Indicator == "✖")
            {
                e.Row.Foreground = Brushes.DarkRed;
                e.Row.FontWeight = FontWeights.SemiBold;
            }
            else if (item.Indicator == "⚠")
            {
                e.Row.Foreground = Brushes.DarkOrange;
                e.Row.FontWeight = FontWeights.SemiBold;
            }
            else if (item.Indicator == "✔")
            {
                e.Row.Foreground = Brushes.DarkGreen;
                e.Row.FontWeight = FontWeights.Normal;
            }
        }

        // ================= TITLE BAR =================

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ================= DATA MODEL =================

    public class ScanResult
    {
        public string Indicator { get; set; }   // 🟢 🟠 🔴
        public int Id { get; set; }
        public string Status { get; set; }
        public long Latency { get; set; }
        public string Detail { get; set; }
    }
}