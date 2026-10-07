using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.IO.Ports;
using EasyModbus;
using System.Windows.Threading;

namespace SimoSim
{
    public partial class MainWindow : Window
    {
        // ================= SERVERS =================
        private ModbusServer tcpServer;
        private ModbusServer rtuServer;

        // ================= SERVER STATE =================
        private bool tcpRunning = false;
        private bool rtuRunning = false;

        // ================= Simulation Engine =================
        private Thread simulationThread;
        private bool simulationRunning = false;

        // ================= State =================
        private bool cbClosed = false;
        private bool motorRunning = false;
        private bool faultActive = false;
        // ===== Manual Status Flags =====
        private bool remoteActive = true;     // default SIMOCODE biasanya remote
        private bool warningActive = false;
        private bool servicePosActive = false;

        private Random rnd = new Random();

        // ===== Measurement Per Phase =====
        private double currentR = 0;
        private double currentS = 0;
        private double currentT = 0;

        private double voltageRS = 400;
        private double voltageST = 400;
        private double voltageTR = 400;

        // ===== Power =====
        private double apparentPower = 0;
        private double activePower = 0;
        private double powerFactor = 0.85;

        private ushort statusWord = 0;


        public MainWindow()
        {
            InitializeComponent();
            LoadComPorts();

            cmbComPort.Text = Properties.Settings.Default.LastComPort;
            cmbBaud.Text = Properties.Settings.Default.LastBaud;
            cmbParity.Text = Properties.Settings.Default.LastParity;
            cmbDataBits.Text = Properties.Settings.Default.LastDataBits;
            cmbStopBits.Text = Properties.Settings.Default.LastStopBits;
        }

        private void EnsureSimulationRunning()
        {
            if (!simulationRunning)
            {
                simulationRunning = true;
                simulationThread = new Thread(SimulationLoop);
                simulationThread.IsBackground = true;
                simulationThread.Start();
            }
        }

        private void StopSimulationIfNoServer()
        {
            if (!rtuRunning && !tcpRunning)
            {
                simulationRunning = false;

                if (simulationThread != null &&
                    simulationThread.IsAlive &&
                    Thread.CurrentThread != simulationThread)
                {
                    simulationThread.Join(300);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            simulationRunning = false;

            tcpServer?.StopListening();
            rtuServer?.StopListening();

            base.OnClosed(e);
        }

        // =====================================================
        // ================= TCP SERVER ========================
        // =====================================================

        private void BtnStartTcp_Click(object sender, RoutedEventArgs e)
        {
            if (!tcpRunning)
            {
                StartTcpServer();
                btnStartTcp.Content = "Stop Modbus TCP";
                btnStartTcp.Background = Brushes.LightGreen;
            }
            else
            {
                StopTcpServer();
                btnStartTcp.Content = "Start Modbus TCP";
                btnStartTcp.Background = Brushes.LightCoral;
            }
        }

        private void StartTcpServer()
        {
            try
            {
                tcpServer = new ModbusServer();

                tcpServer.Port = 502;
                tcpServer.UnitIdentifier = byte.Parse(txtUnitID.Text);

                tcpServer.Listen();

                tcpRunning = true;
                EnsureSimulationRunning();

                txtStatus.Text = "🟢 TCP Slave Running";
                txtStatus.Foreground = Brushes.LimeGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void StopTcpServer()
        {
            tcpServer?.StopListening();
            tcpServer = null;

            tcpRunning = false;
            StopSimulationIfNoServer();

            txtStatus.Text = "🔴 TCP Slave Stopped";
            txtStatus.Foreground = Brushes.Red;
        }

        // =====================================================
        // ================= RTU SERVER ========================
        // =====================================================

        private void BtnStartRtu_Click(object sender, RoutedEventArgs e)
        {
            if (!rtuRunning)
            {
                StartRtuServer();

                if (rtuRunning)
                {
                    btnStartRtu.Content = "Stop Modbus RTU";
                    btnStartRtu.Background = Brushes.LightGreen;
                }
            }
            else
            {
                StopRtuServer();

                btnStartRtu.Content = "Start Modbus RTU";
                btnStartRtu.Background = Brushes.LightCoral;
            }
        }

        private void StartRtuServer()
        {
            try
            {
                rtuServer = new ModbusServer();

                rtuServer.SerialPort = cmbComPort.Text;
                rtuServer.Baudrate = int.Parse(cmbBaud.Text);
                rtuServer.Parity =
                    (Parity)Enum.Parse(typeof(Parity), cmbParity.Text);
                rtuServer.StopBits =
                    (StopBits)Enum.Parse(typeof(StopBits), cmbStopBits.Text);

                rtuServer.UnitIdentifier =
                    byte.Parse(txtUnitID.Text);

                rtuServer.Listen();

                rtuRunning = true;
                EnsureSimulationRunning();

                txtStatus.Text = "🟢 RTU Slave Running";
                txtStatus.Foreground = Brushes.LimeGreen;

                Properties.Settings.Default.LastComPort = cmbComPort.Text;
                Properties.Settings.Default.LastBaud = cmbBaud.Text;
                Properties.Settings.Default.LastParity = cmbParity.Text;
                Properties.Settings.Default.LastStopBits = cmbStopBits.Text;
                Properties.Settings.Default.LastDataBits = cmbDataBits.Text;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void StopRtuServer()
        {
            rtuServer?.StopListening();
            rtuServer = null;

            rtuRunning = false;
            StopSimulationIfNoServer();

            txtStatus.Text = "🔴 RTU Slave Stopped";
            txtStatus.Foreground = Brushes.Red;
        }

        // =====================================================
        // ================= SIMULATION ========================
        // =====================================================

        private void SimulationLoop()
        {
            while (simulationRunning)
            {
                SimulateLogic();
                Thread.Sleep(200);
            }
        }

        // ================= READ COIL SIMOCODE =================
        private bool ReadCoil(ModbusServer server, int index)
        {
            if (server == null)
                return false;

            try
            {
                return server.coils[index];
            }
            catch
            {
                return false;
            }
        }

        private void SimulateLogic()
        {
            if (rtuServer == null && tcpServer == null)
                return;

            bool cmdClose = false;
            bool cmdOpen = false;
            bool cmdStart = false;
            bool cmdStop = false;
            bool cmdFault = false;
            bool cmdReset = false;


            cmdOpen = ReadCoil(rtuServer, 1) || ReadCoil(tcpServer, 1);
            cmdClose = ReadCoil(rtuServer, 2) || ReadCoil(tcpServer, 2);
            cmdStart = ReadCoil(rtuServer, 3) || ReadCoil(tcpServer, 3);
            cmdStop = ReadCoil(rtuServer, 4) || ReadCoil(tcpServer, 4);
            cmdReset = ReadCoil(rtuServer, 6) || ReadCoil(tcpServer, 6);
            cmdFault = ReadCoil(rtuServer, 5) || ReadCoil(tcpServer, 5);

            // ================= PROCESS COMMAND =================

            if (cmdClose && !faultActive)
            {
                cbClosed = true;

                if (rtuServer != null) rtuServer.coils[2] = false;
                if (tcpServer != null) tcpServer.coils[2] = false;
            }

            if (cmdOpen)
            {
                cbClosed = false;
                motorRunning = false;

                if (rtuServer != null) rtuServer.coils[1] = false;
                if (tcpServer != null) tcpServer.coils[1] = false;
            }

            if (cmdStart && cbClosed && !faultActive)
            {
                motorRunning = true;

                if (rtuServer != null) rtuServer.coils[3] = false;
                if (tcpServer != null) tcpServer.coils[3] = false;
            }

            if (cmdStop)
            {
                motorRunning = false;

                if (rtuServer != null) rtuServer.coils[4] = false;
                if (tcpServer != null) tcpServer.coils[4] = false;
            }

            if (cmdFault)
            {
                faultActive = true;
                motorRunning = false;
                cbClosed = false;

                if (rtuServer != null) rtuServer.coils[5] = false;
                if (tcpServer != null) tcpServer.coils[5] = false;
            }

            if (cmdReset)
            {
                faultActive = false;

                if (rtuServer != null) rtuServer.coils[6] = false;
                if (tcpServer != null) tcpServer.coils[6] = false;
            }

            // ================= STATUS WORD =================
            statusWord = 0;

            // ================= BYTE 0 =================

            // 0.1 -> Status Off
            if (!motorRunning)
                statusWord |= (1 << 1);

            // 0.2 -> Status On
            if (motorRunning)
                statusWord |= (1 << 2);

            // 0.5 -> Remote Mode (manual)
            if (remoteActive)
                statusWord |= (1 << 5);

            // 0.6 -> Group Fault
            if (faultActive)
                statusWord |= (1 << 6);

            // 0.7 -> Group Warning (manual)
            if (warningActive)
                statusWord |= (1 << 7);


            // ================= BYTE 1 =================

            // 1.0 -> Service Position (manual)
            if (servicePosActive)
                statusWord |= (1 << 8);

            // 1.1 -> CB Close
            if (cbClosed)
                statusWord |= (1 << 9);

            // 1.2 -> CB Open
            if (!cbClosed)
                statusWord |= (1 << 10);

            if (faultActive)
                statusWord |= (1 << 6);   // Group Fault bit


            const int REG_STATUS_WORD = 1025;

            if (rtuServer != null)
                rtuServer.inputRegisters[REG_STATUS_WORD] = (short)statusWord;

            if (tcpServer != null)
                tcpServer.inputRegisters[REG_STATUS_WORD] = (short)statusWord;
            // ================= MEASUREMENT =================
            if (cbClosed)
            {
                if (motorRunning)
                {
                    currentR = 40 + rnd.NextDouble() * 5;
                    currentS = 42 + rnd.NextDouble() * 5;
                    currentT = 44 + rnd.NextDouble() * 5;
                }
                else
                {
                    currentR = 3 + rnd.NextDouble();
                    currentS = 3.5 + rnd.NextDouble();
                    currentT = 4 + rnd.NextDouble();
                }
            }
            else
            {
                currentR = 0;
                currentS = 0;
                currentT = 0;
            }

            voltageRS = 395 + rnd.NextDouble() * 5;
            voltageST = 398 + rnd.NextDouble() * 5;
            voltageTR = 401 + rnd.NextDouble() * 5;

            double avgCurrent = (currentR + currentS + currentT) / 3;
            double avgVoltage = (voltageRS + voltageST + voltageTR) / 3;

            apparentPower = avgVoltage * avgCurrent * 1.732 / 1000;
            activePower = apparentPower * powerFactor;

            // ================= WRITE REGISTER KE KEDUANYA =================

            void WriteRegisters(ModbusServer server)
            {
                server.holdingRegisters[2056] = (short)(currentR * 100);
                server.holdingRegisters[2057] = (short)(currentS * 100);
                server.holdingRegisters[2058] = (short)(currentT * 100);

                server.holdingRegisters[2061] = (short)voltageRS;
                server.holdingRegisters[2062] = (short)voltageST;
                server.holdingRegisters[2063] = (short)voltageTR;

                server.holdingRegisters[2071] = (short)(activePower * 10);
                server.holdingRegisters[2072] = (short)(apparentPower * 10);
            }

            if (rtuServer != null) WriteRegisters(rtuServer);
            if (tcpServer != null) WriteRegisters(tcpServer);

            Dispatcher.BeginInvoke(new Action(UpdateUI));
        }

        // =====================================================
        // ================= UI UPDATE =========================
        // =====================================================

        private void UpdateUI()
        {
            CB1.IsClosed = cbClosed;

            ellipseMotor.Fill = motorRunning ? Brushes.Red :
                                cbClosed ? Brushes.LimeGreen :
                                Brushes.Gray;

            ledRun.Fill = motorRunning ? Brushes.LimeGreen : Brushes.Gray;
            ledStop.Fill = !motorRunning ? Brushes.Orange : Brushes.Gray;
            // FAULT
            ledFault.Fill = faultActive ? Brushes.Red : Brushes.Gray;

            // WARNING
            ledWarning.Fill = warningActive ? Brushes.Orange : Brushes.Gray;

            // REMOTE
            ledRemote.Fill = remoteActive ? Brushes.DeepSkyBlue : Brushes.Gray;

            // SERVICE
            ledService.Fill = servicePosActive ? Brushes.Cyan : Brushes.Gray;

            // CB STATUS
            ledCbClose.Fill = cbClosed ? Brushes.LimeGreen : Brushes.Gray;
            ledCbOpen.Fill = !cbClosed ? Brushes.Red : Brushes.Gray;


            txtR.Text = $"{currentR:F1} A";
            txtS.Text = $"{currentS:F1} A";
            txtT.Text = $"{currentT:F1} A";

            txtVRS.Text = $"{voltageRS:F0} V";
            txtVST.Text = $"{voltageST:F0} V";
            txtVTR.Text = $"{voltageTR:F0} V";

            txtP.Text = $"{activePower:F1} kW";
            txtSApp.Text = $"{apparentPower:F1} kVA";
            txtPF.Text = $"{powerFactor:F2}";
        }

        // =====================================================
        // ================= UTIL ==============================
        // =====================================================

        private void LoadComPorts()
        {
            cmbComPort.ItemsSource = SerialPort.GetPortNames();
            if (cmbComPort.Items.Count > 0)
                cmbComPort.SelectedIndex = 0;
        }

        // =====================================================
        // ================= MANUAL BUTTON =====================
        // =====================================================

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (!faultActive)
                cbClosed = true;

            txtStatus.Text = "CB CLOSED (Manual)";
            txtStatus.Foreground = Brushes.Yellow;
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            cbClosed = false;
            motorRunning = false;

            txtStatus.Text = "CB OPEN (Manual)";
            txtStatus.Foreground = Brushes.Yellow;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (cbClosed && !faultActive)
            {
                motorRunning = true;
                txtStatus.Text = "Motor STARTED";
                txtStatus.Foreground = Brushes.LightGreen;
            }
            else
            {
                txtStatus.Text = "Cannot Start";
                txtStatus.Foreground = Brushes.OrangeRed;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            motorRunning = false;

            txtStatus.Text = "Motor STOPPED";
            txtStatus.Foreground = Brushes.Orange;
        }

        private void BtnFault_Click(object sender, RoutedEventArgs e)
        {
            faultActive = true;
            motorRunning = false;
            cbClosed = false;

            txtStatus.Text = "FAULT INJECTED";
            txtStatus.Foreground = Brushes.Red;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            faultActive = false;

            txtStatus.Text = "FAULT CLEARED";
            txtStatus.Foreground = Brushes.LightGreen;
        }

        private void BtnRemote_Click(object sender, RoutedEventArgs e)
        {
            remoteActive = !remoteActive;

            if (remoteActive)
            {
                btnRemote.Content = "REMOTE ON";
                btnRemote.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#1565C0"));

                ledRemote.Fill = Brushes.DeepSkyBlue;
            }
            else
            {
                btnRemote.Content = "REMOTE OFF";
                btnRemote.Background = Brushes.DimGray;

                ledRemote.Fill = Brushes.Gray;
            }
        }

        private void BtnWarning_Click(object sender, RoutedEventArgs e)
        {
            warningActive = !warningActive;

            if (warningActive)
            {
                btnWarning.Content = "WARNING ON";
                btnWarning.Background = Brushes.OrangeRed;
            }
            else
            {
                btnWarning.Content = "WARNING";
                btnWarning.Background = Brushes.DimGray;
            }

            UpdateUI();   // ⚠ WAJIB ADA
        }

        private void BtnService_Click(object sender, RoutedEventArgs e)
        {
            servicePosActive = !servicePosActive;

            if (servicePosActive)
            {
                btnService.Content = "SERVICE ON";
                btnService.Background = Brushes.CadetBlue;

                ledService.Fill = Brushes.Cyan;
            }
            else
            {
                btnService.Content = "SERVICE";
                btnService.Background = Brushes.DimGray;

                ledService.Fill = Brushes.Gray;
            }
        }


    }
}