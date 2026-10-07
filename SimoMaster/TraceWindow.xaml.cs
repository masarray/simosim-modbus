using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SimoMaster
{
    /// <summary>
    /// Interaction logic for TraceWindow.xaml
    /// </summary>
    public partial class TraceWindow : Window
    {
        public TraceWindow()
        {
            InitializeComponent();
        }

        public void UpdateTrace(
            string trace,
            string quality,
            long latency,
            int stability,
            string lastError,
            double avgLatency,
            double jitterPercent,
            double busLoad,
            string networkStatus)
        {
            txtTrace.Text = trace;

            txtLatency.Text = $"{latency} ms";
            txtStability.Text = $"{stability}%";
            txtLastError.Text = lastError;
            txtDiagnosis.Text = networkStatus;

            UpdateQuality(quality);
            UpdateHealthHighlight(avgLatency, jitterPercent, busLoad, networkStatus);
        }

        private void UpdateHealthHighlight(
            double avgLatency,
            double jitterPercent,
            double busLoad,
            string networkStatus)
        {
            // ===== LATENCY COLOR =====
            if (avgLatency < 200)
                txtLatency.Foreground = Brushes.LimeGreen;
            else if (avgLatency < 800)
                txtLatency.Foreground = Brushes.Gold;
            else
                txtLatency.Foreground = Brushes.IndianRed;

            // ===== STABILITY COLOR =====
            if (jitterPercent < 10)
                txtStability.Foreground = Brushes.LimeGreen;
            else if (jitterPercent < 25)
                txtStability.Foreground = Brushes.Gold;
            else
                txtStability.Foreground = Brushes.IndianRed;

            // ===== NETWORK STATUS COLOR =====
            if (networkStatus.Contains("STABLE"))
                txtDiagnosis.Foreground = Brushes.LimeGreen;
            else if (networkStatus.Contains("HIGH"))
                txtDiagnosis.Foreground = Brushes.Gold;
            else
                txtDiagnosis.Foreground = Brushes.IndianRed;
        }

        private void UpdateQuality(string quality)
        {
            txtQuality.Text = quality;

            if (quality == "GOOD")
                bdQuality.Background = Brushes.LimeGreen;
            else if (quality == "WARNING")
                bdQuality.Background = Brushes.Gold;
            else
                bdQuality.Background = Brushes.IndianRed;
        }



    }
}
