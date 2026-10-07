using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;

namespace SimoMaster
{
    public partial class ConnectionWindow : Window
    {
        public ConnectionConfig Config { get; private set; }

        public ConnectionWindow()
        {
            InitializeComponent();
            EnsureDefaultSettings();

            cmbMode.SelectionChanged += CmbMode_SelectionChanged;

            // ===== Items Source =====
            cmbMode.SelectedIndex = 0;

            cmbComPort.ItemsSource = SerialPort.GetPortNames();

            cmbBaudRate.ItemsSource = new int[]
            {
                9600, 19200, 38400, 57600, 115200
            };

            cmbParity.ItemsSource = Enum.GetValues(typeof(Parity));
            cmbStopBits.ItemsSource = Enum.GetValues(typeof(StopBits));

            LoadHistory();
            LoadLastSettings();

            // Sinkronisasi awal UI
            CmbMode_SelectionChanged(null, null);
        }

        private void EnsureDefaultSettings()
        {
            var s = Properties.Settings.Default;

            if (string.IsNullOrEmpty(s.LastMode))
                s.LastMode = "Modbus TCP";

            if (string.IsNullOrEmpty(s.LastIP))
                s.LastIP = "127.0.0.1";

            if (string.IsNullOrEmpty(s.LastUnitID))
                s.LastUnitID = "1";

            if (string.IsNullOrEmpty(s.LastCom))
            {
                var ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                    s.LastCom = ports[0];
            }

            if (s.LastBaud == 0)
                s.LastBaud = 9600;

            if (string.IsNullOrEmpty(s.LastParity))
                s.LastParity = "None";

            if (string.IsNullOrEmpty(s.LastStopBits))
                s.LastStopBits = "One";

            s.Save();
        }

        // =========================================================
        // MODE SWITCH
        // =========================================================
        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isTcp = cmbMode.SelectedIndex == 0;

            grpTcp.Visibility = isTcp
                ? Visibility.Visible
                : Visibility.Collapsed;

            grpRtu.Visibility = isTcp
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // =========================================================
        // LOAD LAST SETTINGS
        // =========================================================
        private void LoadLastSettings()
        {
            // ===== Mode =====
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastMode))
            {
                foreach (ComboBoxItem item in cmbMode.Items)
                {
                    if (item.Content.ToString() ==
                        Properties.Settings.Default.LastMode)
                    {
                        cmbMode.SelectedItem = item;
                        break;
                    }
                }
            }

            // ===== TCP =====
            txtIP.Text = Properties.Settings.Default.LastIP;
            txtUnitID.Text = Properties.Settings.Default.LastUnitID;

            // ===== RTU =====
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastCom))
                cmbComPort.SelectedItem =
                    Properties.Settings.Default.LastCom;

            if (Properties.Settings.Default.LastBaud > 0)
                cmbBaudRate.SelectedItem =
                    Properties.Settings.Default.LastBaud;

            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastParity))
            {
                if (Enum.TryParse(
                    Properties.Settings.Default.LastParity,
                    out Parity p))
                    cmbParity.SelectedItem = p;
            }

            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastStopBits))
            {
                if (Enum.TryParse(
                    Properties.Settings.Default.LastStopBits,
                    out StopBits s))
                    cmbStopBits.SelectedItem = s;
            }
        }

        // =========================================================
        // HISTORY LOAD
        // =========================================================
        private void LoadHistory()
        {
            if (Properties.Settings.Default.IPHistory != null)
                foreach (string ip in Properties.Settings.Default.IPHistory)
                    txtIP.Items.Add(ip);

            if (Properties.Settings.Default.IDHistory != null)
                foreach (string id in Properties.Settings.Default.IDHistory)
                    txtUnitID.Items.Add(id);
        }

        // =========================================================
        // OK BUTTON
        // =========================================================
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Config = new ConnectionConfig();

            Config.Mode =
                (cmbMode.SelectedItem as ComboBoxItem)?.Content.ToString();

            // ===== UNIT ID =====
            if (!byte.TryParse(txtUnitID.Text.Trim(), out byte id))
            {
                MessageBox.Show("Unit ID harus angka 0 - 247",
                                "Invalid Unit ID",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            if (id > 247)
            {
                MessageBox.Show("Unit ID maksimal 247");
                return;
            }

            Config.UnitId = id;

            // ===== MODE SPECIFIC =====
            if (Config.Mode == "Modbus TCP")
            {
                if (string.IsNullOrWhiteSpace(txtIP.Text))
                {
                    MessageBox.Show("IP Address kosong");
                    return;
                }

                Config.IpAddress = txtIP.Text.Trim();
            }
            else
            {
                if (cmbComPort.SelectedItem == null)
                {
                    MessageBox.Show("Pilih COM Port");
                    return;
                }

                Config.ComPort = cmbComPort.SelectedItem.ToString();
                Config.BaudRate = (int)cmbBaudRate.SelectedItem;
                Config.Parity = (Parity)cmbParity.SelectedItem;
                Config.StopBits = (StopBits)cmbStopBits.SelectedItem;
            }

            // ===== SAVE HISTORY =====
            SaveHistory();

            // ===== SAVE LAST SETTINGS =====
            Properties.Settings.Default.LastMode = Config.Mode;
            Properties.Settings.Default.LastIP = txtIP.Text.Trim();
            Properties.Settings.Default.LastUnitID = txtUnitID.Text.Trim();
            Properties.Settings.Default.LastCom = cmbComPort.Text;

            if (cmbBaudRate.SelectedItem != null)
                Properties.Settings.Default.LastBaud =
                    (int)cmbBaudRate.SelectedItem;

            if (cmbParity.SelectedItem != null)
                Properties.Settings.Default.LastParity =
                    cmbParity.SelectedItem.ToString();

            if (cmbStopBits.SelectedItem != null)
                Properties.Settings.Default.LastStopBits =
                    cmbStopBits.SelectedItem.ToString();

            Properties.Settings.Default.Save();

            DialogResult = true;   // cukup ini saja
        }

        // =========================================================
        // CANCEL
        // =========================================================
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // =========================================================
        // SAVE HISTORY
        // =========================================================
        private void SaveHistory()
        {
            Properties.Settings.Default.IPHistory =
                AddToHistory(Properties.Settings.Default.IPHistory,
                             txtIP.Text);

            Properties.Settings.Default.IDHistory =
                AddToHistory(Properties.Settings.Default.IDHistory,
                             txtUnitID.Text);

            Properties.Settings.Default.Save();
        }

        private StringCollection AddToHistory(
            StringCollection history,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return history;

            if (history == null)
                history = new StringCollection();

            if (history.Contains(value))
                history.Remove(value);

            history.Insert(0, value);

            if (history.Count > 10)
                history.RemoveAt(10);

            return history;
        }
    }
}