using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Modbus.Device;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Management;
using System.IO;
using HMI.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SimoMaster
{
    public partial class MainWindow : Window
    {
        private ConnectionConfig currentConfig;

        private TcpClient client;
        private IModbusMaster master;
        private SerialPort serialPort;

        private DispatcherTimer pollTimer;
        private DispatcherTimer ledTimer;

        private RegisterMap registerMap = new RegisterMap();

        private bool connected = false;

        private int txCounter = 0;
        private int rxCounter = 0;
        private int errorCounter = 0;
        private long lastLatency = 0;

        private int successStreak = 0;
        private ushort previousStatusWord = 0;
        private bool commLostState = false;
        private bool firstPoll = true;
        private bool pollingStartedLogged = false;

        private int txPulse = 0;
        private int rxPulse = 0;

        private DateTime lastGoodComm = DateTime.MinValue;

        private int normalInterval = 500;
        private int slowInterval = 1000;
        private int criticalInterval = 2000;
        private int currentInterval = 800;

        private DispatcherTimer reconnectTimer;
        private bool reconnecting = false;

        private string savedDeviceId = null;

        private byte slaveId => currentConfig != null ? currentConfig.UnitId : (byte)1;

        private TraceWindow traceWindow;
        private int totalPoll = 0;
        private int totalError = 0;

        private int timeoutCount = 0;
        private int exceptionCount = 0;
        private int slowCount = 0;
        private int crcSuspectCount = 0;
        private int coilWriteCount = 0;
        private DateTime lastErrorTime = DateTime.MinValue;

        private int slowWarningCount = 0;
        private int criticalSlowCount = 0;

        private const int LatencyBufferSize = 20;
        private Queue<long> latencyHistory = new Queue<long>();
        // ===== Engineering Statistics =====
        private double avgLatency = 0;
        private double jitterMs = 0;
        private double jitterPercent = 0;
        private double busLoadPercent = 0;
        private string networkStatus = "UNKNOWN";

        private bool jitterHighState = false;
        private bool busLoadHighState = false;
        private bool latencyHighState = false;

        private DateTime lastJitterLogTime = DateTime.MinValue;
        private DateTime lastBusLogTime = DateTime.MinValue;
        private DateTime lastLatencyLogTime = DateTime.MinValue;

        private const int LogCooldownSeconds = 5;

        private double lastLoggedJitter = 0;
        private const double JitterChangeThreshold = 25; // %

        public MainWindow()
        {
            InitializeComponent();
            LoadHistory();

            pollTimer = new DispatcherTimer();
            pollTimer.Interval = TimeSpan.FromMilliseconds(currentInterval);
            pollTimer.Tick += PollDevice;

            ledTimer = new DispatcherTimer();
            ledTimer.Interval = TimeSpan.FromMilliseconds(100);
            ledTimer.Tick += LedDecay;
            ledTimer.Start();

            reconnectTimer = new DispatcherTimer();
            reconnectTimer.Interval = TimeSpan.FromSeconds(3);
            reconnectTimer.Tick += ReconnectTimer_Tick;

            Motor1.State = MotorState.Idle;
        }

        // ================= CONNECT =================

        private void BtnStartSim_Click(object sender, RoutedEventArgs e)
        {
            if (!connected)
            {
                var dlg = new ConnectionWindow();
                dlg.Owner = this;

                if (dlg.ShowDialog() == true)
                {
                    currentConfig = dlg.Config;
                    ConnectDevice();
                }
            }
            else
            {
                DisconnectDevice();
            }
        }

        private async void ConnectDevice()
        {
            try
            {
                AddLog("Connecting...", Brushes.LightGray);

                // ===================== MODBUS TCP =====================
                if (currentConfig.Mode == "Modbus TCP")
                {
                    client = new TcpClient();

                    var connectTask = client.ConnectAsync(
                        currentConfig.IpAddress,
                        502);

                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                        throw new TimeoutException(
                            $"Connection timeout ke {currentConfig.IpAddress}:502");

                    if (!client.Connected)
                        throw new SocketException();

                    master = ModbusIpMaster.CreateIp(client);

                    AddLog($"TCP Connected → {currentConfig.IpAddress}:502",
                           Brushes.LightGray);
                }

                // ===================== MODBUS RTU =====================
                else
                {
                    var ports = SerialPort.GetPortNames();

                    if (!ports.Contains(currentConfig.ComPort))
                        throw new InvalidOperationException(
                            $"COM Port {currentConfig.ComPort} tidak tersedia");

                    AddLog($"Attempt RTU Connect → {currentConfig.ComPort}",
                           Brushes.LightGray);

                    // beri waktu Windows release handle
                    await Task.Delay(300);

                    if (serialPort != null)
                    {
                        try
                        {
                            if (serialPort.IsOpen)
                                serialPort.Close();

                            serialPort.Dispose();
                        }
                        catch { }

                        serialPort = null;
                    }

                    serialPort = new SerialPort(
                        currentConfig.ComPort,
                        currentConfig.BaudRate,
                        currentConfig.Parity,
                        8,
                        currentConfig.StopBits);

                    serialPort.ReadTimeout = 1000;
                    serialPort.WriteTimeout = 1000;

                    int retry = 0;
                    bool opened = false;

                    while (!opened && retry < 3)
                    {
                        try
                        {
                            serialPort.Open();
                            opened = true;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            retry++;
                            await Task.Delay(200);
                        }
                    }

                    if (!opened)
                        throw new Exception($"COM {currentConfig.ComPort} tidak bisa dibuka.");

                    master = ModbusSerialMaster.CreateRtu(serialPort);

                    AddLog($"RTU Connected → {currentConfig.ComPort}",
                           Brushes.LightGray);

                    var deviceInfo = GetDeviceInfo(currentConfig.ComPort);
                    savedDeviceId = deviceInfo.DeviceId;
                }

                // ===================== COMMON SETUP =====================
                master.Transport.ReadTimeout = 2000;
                master.Transport.Retries = 0;

                connected = true;
                pollTimer.Start();

                Connect.Content = "Disconnect";
                Connect.Background = Brushes.LightGreen;

                txtStatus.Text = "🟢 Connected";
                txtStatus.Foreground = Brushes.LightGreen;

                SaveHistory();
                UpdateWindowTitle();

                AddLog("Connected Successfully",
                       Brushes.White,
                       Brushes.DarkGreen);

                AddLog($"Mode:{currentConfig.Mode}  Address:{GetConnectionAddress()}  UnitID:{slaveId}",
                       Brushes.LightGray);
                if (currentConfig.Mode == "Modbus RTU")
                {
                    menuScanDevices.IsEnabled = false;
                }
                else
                {
                    menuScanDevices.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                connected = false;

                AddLog($"Connection Failed: {ex.Message}",
                       Brushes.White,
                       Brushes.IndianRed);

                ledComm.Fill = Brushes.Red;

                MessageBox.Show(ex.Message,
                                "Connection Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
        private string GetConnectionAddress()
        {
            if (currentConfig == null) return "";

            return currentConfig.Mode == "Modbus TCP"
                ? currentConfig.IpAddress
                : currentConfig.ComPort;
        }

        private void DisconnectDevice()
        {
            try
            {
                // 🔴 Block reconnect & polling dulu
                connected = false;
                reconnecting = false;

                pollTimer.Stop();
                reconnectTimer.Stop();

                // 🔴 Hentikan event sementara
                pollTimer.Tick -= PollDevice;
                reconnectTimer.Tick -= ReconnectTimer_Tick;

                // 🔴 Dispose master
                if (master != null)
                {
                    master.Dispose();
                    master = null;
                }

                // 🔴 Close TCP
                if (client != null)
                {
                    client.Close();
                    client = null;
                }

                // 🔴 Close Serial
                if (serialPort != null)
                {
                    try
                    {
                        if (serialPort.IsOpen)
                            serialPort.Close();
                    }
                    catch { }

                    serialPort.Dispose();
                    serialPort = null;
                }

                // 🔴 Re-attach timer events
                pollTimer.Tick += PollDevice;
                reconnectTimer.Tick += ReconnectTimer_Tick;

                Connect.Content = "Connect";
                Connect.Background = Brushes.LightCoral;

                txtStatus.Text = "🔴 Disconnected";
                txtStatus.Foreground = Brushes.Red;

                this.Title = "Simocode Modbus Master";

                AddLog("Disconnected", Brushes.White, Brushes.OrangeRed);
            }
            catch (Exception ex)
            {
                AddLog($"Disconnect Error: {ex.Message}",
                       Brushes.White,
                       Brushes.IndianRed);
            }
        }
        // ================= POLLING =================

        private void PollDevice(object sender, EventArgs e)
        {
            if (!connected)
                return;

            if (!pollingStartedLogged)
            {
                AddLog("Polling Started", Brushes.LightGray);
                pollingStartedLogged = true;
            }

            totalPoll++;

            try
            {
                var result = ExecutePollCycle();

                UpdateAfterSuccess(result);

                BuildAndSendTrace(result);

                UpdateCommLed(true);
            }
            catch (Exception ex)
            {
                HandlePollException(ex);

                UpdateCommLed(false);
            }
        }

        private class PollResult
        {
            public ushort StatusWord;
            public ushort[] Current;
            public ushort[] Voltage;
            public ushort[] Power;
            public long Latency;
        }

        private PollResult ExecutePollCycle()
        {
            var sw = Stopwatch.StartNew();

            txPulse = 3;
            txCounter++;

            ushort status = 0;
            ushort[] current = null;
            ushort[] voltage = null;
            ushort[] power = null;

            if (registerMap.PollStatusWord)
            {
                var result = master.ReadInputRegisters(
                    slaveId,
                    Normalize(registerMap.StatusWord),
                    1);

                status = result[0];
            }

            if (registerMap.PollCurrent)
            {
                current = master.ReadHoldingRegisters(
                    slaveId,
                    Normalize(registerMap.CurrentStart),
                    3);
            }

            if (registerMap.PollVoltage)
            {
                voltage = master.ReadHoldingRegisters(
                    slaveId,
                    Normalize(registerMap.VoltageStart),
                    3);
            }

            if (registerMap.PollPower)
            {
                power = master.ReadHoldingRegisters(
                    slaveId,
                    Normalize(registerMap.ActivePower),
                    2);
            }

            sw.Stop();

            return new PollResult
            {
                StatusWord = status,
                Current = current,
                Voltage = voltage,
                Power = power,
                Latency = sw.ElapsedMilliseconds
            };
        }

        private void UpdateAfterSuccess(PollResult r)
        {
            if (registerMap.PollStatusWord)
                ParseStatus(r.StatusWord);

            if (registerMap.PollCurrent && r.Current != null)
                UpdateMeasurement(r.Current, r.Voltage, r.Power);

            lastLatency = r.Latency;

            latencyHistory.Enqueue(lastLatency);
            if (latencyHistory.Count > LatencyBufferSize)
                latencyHistory.Dequeue();

            CalculateStatistics();

            // ===== SMART EVENT DETECTION =====

            // ===== LATENCY =====
            if (r.Latency > 1000)
            {
                if (!latencyHighState ||
                    (DateTime.Now - lastLatencyLogTime).TotalSeconds > LogCooldownSeconds)
                {
                    AddLog($"CRITICAL LATENCY {r.Latency} ms",
                        Brushes.White,
                        Brushes.DarkRed);

                    lastLatencyLogTime = DateTime.Now;
                }

                latencyHighState = true;
            }
            else if (r.Latency < 600 && latencyHighState)
            {
                AddLog("Latency back to normal",
                    Brushes.LightGreen);

                latencyHighState = false;
            }

            // ===== JITTER (SIGNIFICANT CHANGE MODE) =====

            if (avgLatency > 20 && jitterPercent > 40) // baseline alarm zone
            {
                double diff = Math.Abs(jitterPercent - lastLoggedJitter);

                if (!jitterHighState || diff >= JitterChangeThreshold)
                {
                    AddLog($"Jitter spike {jitterPercent:F1}%",
                        Brushes.White,
                        Brushes.DarkOrange);

                    lastLoggedJitter = jitterPercent;
                    jitterHighState = true;
                }
            }
            else if (jitterPercent < 25 && jitterHighState)
            {
                AddLog("Jitter stabilized",
                    Brushes.LightGreen);

                jitterHighState = false;
                lastLoggedJitter = jitterPercent;
            }

            // ===== BUS LOAD =====
            if (busLoadPercent > 80)
            {
                if (!busLoadHighState ||
                    (DateTime.Now - lastBusLogTime).TotalSeconds > LogCooldownSeconds)
                {
                    AddLog($"BUS LOAD HIGH {busLoadPercent:F0}%",
                        Brushes.White,
                        Brushes.DarkRed);

                    lastBusLogTime = DateTime.Now;
                }

                busLoadHighState = true;
            }
            else if (busLoadPercent < 60 && busLoadHighState)
            {
                AddLog("Bus load normalized",
                    Brushes.LightGreen);

                busLoadHighState = false;
            }


            rxPulse = 3;
            rxCounter++;

            if (!firstPoll && r.StatusWord != previousStatusWord)
            {
                AddLog($"Status Word Changed 0x{previousStatusWord:X4} → 0x{r.StatusWord:X4}",
                    Brushes.LightBlue);

                DetectBitChange(previousStatusWord, r.StatusWord);
            }

            previousStatusWord = r.StatusWord;
            firstPoll = false;

            UpdateMeasurement(r.Current, r.Voltage, r.Power);

            txtCommInfo.Text =
                $"TX:{txCounter} RX:{rxCounter} {lastLatency}ms ERR:{errorCounter}";

            lastGoodComm = DateTime.Now;
            errorCounter = 0;
            successStreak++;

            AdjustPollingInterval();
        }

        private void BuildAndSendTrace(PollResult r)
        {
            string quality = "GOOD";
            string lastError = "None";
            string diagnosis = "System Normal";

            if (r.Latency > 1000)
            {
                quality = "CRITICAL";
                diagnosis = "Very high latency";
            }
            else if (r.Latency > 600)
            {
                quality = "SLOW";
                diagnosis = "High response time";
            }

            string statusBits = DecodeStatusWord(r.StatusWord);

            if ((r.StatusWord & (1 << 6)) != 0)
            {
                diagnosis = "DEVICE FAULT ACTIVE";
                // JANGAN ubah quality komunikasi
            }
            else if ((r.StatusWord & (1 << 7)) != 0)
            {
                diagnosis = "GROUP WARNING ACTIVE";
            }

            string trace = $@"
==================== POLL CYCLE ====================
Timestamp : {DateTime.Now:HH:mm:ss.fff}
Mode      : {currentConfig.Mode}
Slave ID  : {slaveId}
Latency   : {r.Latency} ms
----------------------------------------------------

[TX] FC04 Read Input Register {registerMap.StatusWord}
[RX] Status Word : {r.StatusWord} (0x{r.StatusWord:X4})

--- Bit Breakdown ---
{statusBits}

----------------------------------------------------
[TX] FC03 Current {registerMap.CurrentStart}
[RX] IR/IS/IT : {r.Current[0] / 100.0:F1} /
                {r.Current[1] / 100.0:F1} /
                {r.Current[2] / 100.0:F1} A

----------------------------------------------------
[TX] FC03 Voltage {registerMap.VoltageStart}
[RX] VRS/VST/VTR : {r.Voltage[0]} /
                   {r.Voltage[1]} /
                   {r.Voltage[2]} V

----------------------------------------------------
[TX] FC03 Power {registerMap.ActivePower}
[RX] P/S : {r.Power[0] / 10.0:F1} kW /
           {r.Power[1] / 10.0:F1} kVA

----------------------------------------------------
RELIABILITY / TIMING / TRAFFIC LAYER
{GenerateLayerReport()}
====================================================";

            SendTraceToWindow(trace,
                              quality,
                              r.Latency,
                              lastError,
                              diagnosis);
        }

        private void HandlePollException(Exception ex)
        {
            totalError++;
            errorCounter++;
            successStreak = 0;

            string diag = DiagnoseException(ex);

            AddLog($"Modbus Exception: {ex.Message}",
                Brushes.White,
                Brushes.IndianRed);

            SendTraceToWindow(
                $"ERROR: {ex.Message}",
                "EXCEPTION",
                0,
                ex.Message,
                diag);

            if (!reconnecting)
            {
                if (currentConfig.Mode == "Modbus TCP" && errorCounter >= 3)
                    StartReconnect();

                if (currentConfig.Mode == "Modbus RTU" && errorCounter >= 5)
                    StartReconnectRTU();
            }
        }

        private void UpdateCommLed(bool commOk)
        {
            if (!commOk || (DateTime.Now - lastGoodComm).TotalSeconds > 2)
            {
                ledComm.Fill = Brushes.Red;
                return;
            }

            if (lastLatency > 1000)
                ledComm.Fill = Brushes.Red;
            else if (lastLatency > 300)
                ledComm.Fill = Brushes.Orange;
            else
                ledComm.Fill = Brushes.LimeGreen;
        }

        private void CalculateStatistics()
        {
            if (latencyHistory.Count < 5)
                return;

            avgLatency = latencyHistory.Average();

            double sum = 0;

            foreach (long v in latencyHistory)
                sum += Math.Pow(v - avgLatency, 2);

            jitterMs = Math.Sqrt(sum / latencyHistory.Count);

            // =========================
            // FIX FALSE JITTER ISSUE
            // =========================

            // Kalau latency sangat kecil (loopback / localhost),
            // jangan pakai relative jitter %
            if (avgLatency < 50)
            {
                jitterPercent = 0;
                networkStatus = "LOCAL LOOPBACK";
            }
            else
            {
                jitterPercent = (jitterMs / avgLatency) * 100;

                // ===== NETWORK CLASSIFICATION =====

                if (jitterPercent < 10 && busLoadPercent < 70)
                    networkStatus = "STABLE";

                else if (jitterPercent < 25 && busLoadPercent < 85)
                    networkStatus = "STABLE - HIGH LOAD";

                else if (jitterPercent >= 25 && busLoadPercent < 85)
                    networkStatus = "UNSTABLE BUS";

                else
                    networkStatus = "NEAR SATURATION";
            }

            // Bus load estimation
            busLoadPercent = (avgLatency / currentInterval) * 100;
        }

        private void StartReconnect()
        {
            if (!connected)
                return;
            AddLog("Starting Auto Reconnect...",
                   Brushes.White,
                   Brushes.DarkOrange);

            reconnecting = true;

            pollTimer.Stop();

            try
            {
                master?.Dispose();
                client?.Close();
            }
            catch { }

            reconnectTimer.Start();
        }

        private void StartReconnectRTU()
        {
            if (!connected)
                return;
            AddLog("RTU Reconnect Triggered...",
                   Brushes.White,
                   Brushes.DarkOrange);

            reconnecting = true;
            pollTimer.Stop();

            try
            {
                master?.Dispose();

                if (serialPort != null)
                {
                    if (serialPort.IsOpen)
                        serialPort.Close();

                    serialPort.Dispose();
                }
            }
            catch { }

            reconnectTimer.Start();
        }

        private string FindPortByDeviceId(string deviceId)
        {
            using (var searcher =
                new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (var device in searcher.Get())
                {
                    string id = device["DeviceID"]?.ToString();
                    string name = device["Name"]?.ToString();

                    if (id == deviceId)
                    {
                        int start = name.LastIndexOf("(COM");
                        int end = name.LastIndexOf(")");

                        if (start >= 0 && end > start)
                            return name.Substring(start + 1, end - start - 1);
                    }
                }
            }

            return null;
        }
        private async void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            AddLog("Attempting Reconnect...",
                   Brushes.LightGray);

            try
            {
                if (currentConfig.Mode == "Modbus TCP")
                {
                    client = new TcpClient();

                    var connectTask = client.ConnectAsync(
                        currentConfig.IpAddress,
                        502);

                    if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
                        throw new TimeoutException();

                    master = ModbusIpMaster.CreateIp(client);
                }
                else // RTU
                {
                    string newPort = FindPortByDeviceId(savedDeviceId);

                    if (newPort == null)
                        throw new Exception("Original USB-Serial not found");

                    currentConfig.ComPort = newPort;

                    serialPort = new SerialPort(
                        newPort,
                        currentConfig.BaudRate,
                        currentConfig.Parity,
                        8,
                        currentConfig.StopBits);

                    serialPort.ReadTimeout = 1000;
                    serialPort.WriteTimeout = 1000;

                    serialPort.Open();

                    master = ModbusSerialMaster.CreateRtu(serialPort);

                    AddLog($"RTU Reconnected on {newPort}",
                           Brushes.White,
                           Brushes.DarkGreen);
                }

                master.Transport.ReadTimeout = 2000;
                master.Transport.Retries = 0;

                reconnectTimer.Stop();
                reconnecting = false;
                errorCounter = 0;

                pollTimer.Start();

                AddLog("Reconnected Successfully",
                       Brushes.White,
                       Brushes.DarkGreen);

                txtStatus.Text = "🟢 Connected";
                txtStatus.Foreground = Brushes.LightGreen;
            }
            catch
            {
                // retry lagi 3 detik
            }
        }

        private string FindAvailableComPort()
        {
            var ports = SerialPort.GetPortNames();

            if (ports.Contains(currentConfig.ComPort))
                return currentConfig.ComPort;

            // fallback → ambil port pertama
            if (ports.Length > 0)
                return ports.First();

            return null;
        }
        private void LedDecay(object sender, EventArgs e)
        {
            if (txPulse > 0)
            {
                ledTx.Fill = Brushes.Yellow;
                txPulse--;
            }
            else
                ledTx.Fill = Brushes.Gray;

            if (rxPulse > 0)
            {
                ledRx.Fill = Brushes.LimeGreen;
                rxPulse--;
            }
            else
                ledRx.Fill = Brushes.Gray;
        }

        // ================= STATUS =================

        private void ParseStatus(ushort sw)
        {
            bool statusOff = (sw & (1 << 1)) != 0;
            bool statusOn = (sw & (1 << 2)) != 0;
            bool remoteMode = (sw & (1 << 5)) != 0;
            bool groupFault = (sw & (1 << 6)) != 0;
            bool groupWarning = (sw & (1 << 7)) != 0;
            bool servicePos = (sw & (1 << 8)) != 0;
            bool cbClose = (sw & (1 << 9)) != 0;
            bool cbOpen = (sw & (1 << 10)) != 0;

            // 🔥 INI YANG KURANG
            if (cbClose)
            {
                CB1.State = CbProtState.Closed;
            }
            else
            { 
                CB1.State = CbProtState.Open;
            }
            // 🔥 PRIORITY LOGIC
            if (groupFault)
            {
                CBM.State = CbProtState.Trip;
                Motor1.State = MotorState.Trip;
            }
            else if (statusOn && !groupFault)
            {
                CBM.State = CbProtState.Closed;
                Motor1.State = MotorState.Run;
            }
            else if (cbClose)
            {
                CBM.State = CbProtState.Open;
                Motor1.State = MotorState.Idle;
            }
            else
            {
                CBM.State = CbProtState.Open;
                Motor1.State = MotorState.Inactive;
            }

            ledCbOpen.Fill = cbOpen ? Brushes.LimeGreen : Brushes.Gray;
            ledCbClose.Fill = cbClose ? Brushes.Red : Brushes.Gray;
            ledFault.Fill = groupFault ? Brushes.Red : Brushes.Gray;
            ledWarning.Fill = groupWarning ? Brushes.Gold : Brushes.Gray;
            ledService.Fill = servicePos ? Brushes.Blue : Brushes.Gray;
            ledRemote.Fill = remoteMode ? Brushes.Green : Brushes.Gray;

           // if (groupFault)
           //     Motor1.State = MotorState.Trip;
           // else if (statusOn)
           //     Motor1.State = MotorState.Run;
           // else if (cbClose)
           //     Motor1.State = MotorState.Idle;
           // else
           //     Motor1.State = MotorState.Inactive;
        }

        // ================= MEASUREMENT =================
        private void UpdateMeasurement(
            ushort[] current,
            ushort[] voltage,
            ushort[] power)
        {
            double iR = current[0] / 100.0;
            double iS = current[1] / 100.0;
            double iT = current[2] / 100.0;

            double vRS = voltage[0];
            double vST = voltage[1];
            double vTR = voltage[2];

            double p = power[0] / 10.0;
            double s = power[1] / 10.0;

            txtR.Text = $"{iR:F1} A";
            txtS.Text = $"{iS:F1} A";
            txtT.Text = $"{iT:F1} A";

            txtVRS.Text = $"{vRS:F0} V";
            txtVST.Text = $"{vST:F0} V";
            txtVTR.Text = $"{vTR:F0} V";

            txtP.Text = $"{p:F1} kW";
            txtSApp.Text = $"{s:F1} kVA";
            txtPF.Text = s != 0 ? $"{p / s:F2}" : "0.00";
        }

        private void WriteCoilWithTrace(ushort coilAddress, string description)
        {
            if (!connected) return;

            try
            {
                var sw = Stopwatch.StartNew();

                master.WriteSingleCoil(
                    slaveId,
                    Normalize(coilAddress),
                    true);

                sw.Stop();

                coilWriteCount++;

string trace = $@"
================ COIL WRITE =================
Timestamp : {DateTime.Now:HH:mm:ss.fff}

[TX] FC05 Write Coil {coilAddress}
Meaning : {description}
Value   : ON

Latency : {sw.ElapsedMilliseconds} ms
============================================";

                SendTraceToWindow(trace,
                                  "GOOD",
                                  sw.ElapsedMilliseconds,
                                  "100%",
                                  "No Error");

                AddLog($"Command → {description}",
                       Brushes.White,
                       Brushes.DarkBlue);
            }
            catch (Exception ex)
            {
                string diag = DiagnoseException(ex);

                SendTraceToWindow(
                    $"Coil Write ERROR → {ex.Message}",
                    "ERROR",
                    0,
                    "0%",
                    diag);

                AddLog($"Coil Error → {ex.Message}",
                       Brushes.White,
                       Brushes.IndianRed);
            }
        }
        // ================= CONTROL =================

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(registerMap.CoilOpen, "OPEN");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(registerMap.CoilClose, "CLOSE");
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(registerMap.CoilStart, "START");
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(registerMap.CoilStop, "STOP");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(registerMap.CoilReset, "FAULT RESET");
        }


        // ================= HISTORY =================

        private void LoadHistory()
        {
            if (Properties.Settings.Default.FeederHistory != null)
                foreach (string feeder in Properties.Settings.Default.FeederHistory)
                    txtFeeder.Items.Add(feeder);
        }

        private void SaveHistory()
        {
            Properties.Settings.Default.FeederHistory =
                AddToHistory(Properties.Settings.Default.FeederHistory, txtFeeder.Text);

            Properties.Settings.Default.Save();
        }

        private StringCollection AddToHistory(StringCollection history, string value)
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

        // ================= LOG =================

        private void AddLog(string msg, Brush fg, Brush bg = null)
        {
            var item = new ListBoxItem();
            item.Content = $"{DateTime.Now:HH:mm:ss}  {msg}";
            item.Foreground = fg;
            if (bg != null)
                item.Background = bg;

            lstLog.Items.Insert(0, item);

            if (lstLog.Items.Count > 200)
                lstLog.Items.RemoveAt(200);
        }

        private void DetectBitChange(ushort oldVal, ushort newVal)
        {
            for (int i = 0; i < 16; i++)
            {
                bool oldBit = (oldVal & (1 << i)) != 0;
                bool newBit = (newVal & (1 << i)) != 0;

                if (oldBit != newBit)
                {
                    AddLog($"Bit {i} → {(newBit ? "ON" : "OFF")}",
                        Brushes.LightSkyBlue);
                }
            }
        }

        private void AdjustPollingInterval()
        {
            if (lastLatency > 800)
                currentInterval = criticalInterval;
            else if (lastLatency > 400)
                currentInterval = slowInterval;
            else if (lastLatency < 250)
                currentInterval = normalInterval;

            pollTimer.Interval = TimeSpan.FromMilliseconds(currentInterval);
        }

        private void UpdateWindowTitle()
        {
            if (currentConfig == null) return;

            string address =
                currentConfig.Mode == "Modbus TCP"
                ? currentConfig.IpAddress
                : currentConfig.ComPort;

            this.Title =
                $"{txtFeeder.Text} | {address} | ID:{currentConfig.UnitId:D2}";
        }

        private void MenuConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!connected)
                BtnStartSim_Click(sender, e);
        }

        private void MenuDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (connected)
                DisconnectDevice();
        }

        private void MenuClearLog_Click(object sender, RoutedEventArgs e)
        {
            lstLog.Items.Clear();
            AddLog("Log Cleared", Brushes.LightGray);
        }

        private void MenuResetCounters_Click(object sender, RoutedEventArgs e)
        {
            txCounter = 0;
            rxCounter = 0;
            errorCounter = 0;

            AddLog("Counters Reset", Brushes.LightGray);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Simocode Modbus Master\nMas Ari Engineering Tool\n2026",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void MenuScanDevices_Click(object sender, RoutedEventArgs e)
        {
            if (!connected || master == null)
            {
                MessageBox.Show("Connect dulu ke device");
                return;
            }

            // Pause polling
            pollTimer.Stop();

            var scanWindow = new DeviceScanWindow();
            scanWindow.Owner = this;
            scanWindow.SetMode(currentConfig.Mode);
            scanWindow.Show();

            int originalTimeout = master.Transport.ReadTimeout;
            master.Transport.ReadTimeout = 600;

            int onlineCount = 0;
            int totalScan = 20;

            try
            {
                for (byte id = 1; id <= totalScan; id++)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();

                        await Task.Run(() =>
                        {
                            master.ReadInputRegisters(
                                id,
                                Normalize(registerMap.StatusWord),
                                1);
                        });

                        sw.Stop();
                        long latency = sw.ElapsedMilliseconds;

                        string indicator;
                        string detail = "Response OK";

                        if (latency > 800)
                        {
                            indicator = "⚠";   // slow
                            detail = "Slow Response";
                        }
                        else
                        {
                            indicator = "✔";   // ok
                        }

                        scanWindow.AddResult(new ScanResult
                        {
                            Indicator = indicator,
                            Id = id,
                            Status = "ONLINE",
                            Latency = latency,
                            Detail = detail
                        });

                        onlineCount++;
                    }
                    catch (Exception ex)
                    {
                        scanWindow.AddResult(new ScanResult
                        {
                            Indicator = "✖",
                            Id = id,
                            Status = "NO RESPONSE",
                            Latency = 0,
                            Detail = ex.Message
                        });
                    }

                    await Task.Delay(80); // slight spacing
                }

                scanWindow.SetSummary($"Online Devices: {onlineCount} / {totalScan}");
            }
            finally
            {
                master.Transport.ReadTimeout = originalTimeout;
                pollTimer.Start();
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private (string Port, string DeviceId, string Name) GetDeviceInfo(string portName)
        {
            using (var searcher =
                new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (var device in searcher.Get())
                {
                    string name = device["Name"]?.ToString();
                    if (name != null && name.Contains(portName))
                    {
                        return (
                            portName,
                            device["DeviceID"]?.ToString(),
                            name
                        );
                    }
                }
            }

            return (null, null, null);
        }

        private void MenuRegisterMapping_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RegisterMappingWindow(registerMap);
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                registerMap = dlg.Map;
                AddLog("Register Mapping Updated", Brushes.LightGreen);
            }
        }

        private ushort Normalize(ushort address)
        {
            if (registerMap.PlcBaseAddressing)
                return (ushort)(address - 1);

            return address;
        }

        private void MenuTrace_Click(object sender, RoutedEventArgs e)
        {
            if (traceWindow == null || !traceWindow.IsVisible)
            {
                traceWindow = new TraceWindow();
                traceWindow.Owner = this;
                traceWindow.Show();
            }
            else
            {
                traceWindow.Activate();
            }
        }

        private void SendTraceToWindow(
            string trace,
            string quality,
            long latency,
            string lastError,
            string diagnosis)
        {
            if (traceWindow == null || !traceWindow.IsVisible)
                return;

            string mode = currentConfig.Mode;

            string endpoint =
                currentConfig.Mode == "Modbus TCP"
                ? $"{currentConfig.IpAddress}:502"
                : $"{currentConfig.ComPort}  {currentConfig.BaudRate}";

            int stability = totalPoll == 0
                ? 100
                : (int)(((double)(totalPoll - timeoutCount - exceptionCount) / totalPoll) * 100);

            Brush healthBrush = Brushes.LightGreen;

            if (networkStatus.Contains("UNSTABLE"))
                healthBrush = Brushes.Gold;

            if (networkStatus.Contains("NEAR"))
                healthBrush = Brushes.IndianRed;

            traceWindow.UpdateTrace(
                trace,
                quality,
                latency,
                stability,
                lastError,
                avgLatency,
                jitterPercent,
                busLoadPercent,
                networkStatus);
        }

        private string DecodeStatusWord(ushort sw)
        {
            bool statusOff = (sw & (1 << 1)) != 0;
            bool statusOn = (sw & (1 << 2)) != 0;
            bool remoteMode = (sw & (1 << 5)) != 0;
            bool groupFault = (sw & (1 << 6)) != 0;
            bool groupWarning = (sw & (1 << 7)) != 0;
            bool servicePos = (sw & (1 << 8)) != 0;
            bool cbClose = (sw & (1 << 9)) != 0;
            bool cbOpen = (sw & (1 << 10)) != 0;

            string binary = Convert.ToString(sw, 2).PadLeft(16, '0');

            return
$@"Binary : {binary}

[BIT 1 ] Status OFF        : {statusOff}
[BIT 2 ] Status ON         : {statusOn}
[BIT 5 ] Remote Mode       : {remoteMode}
[BIT 6 ] Group Fault       : {groupFault}
[BIT 7 ] Group Warning     : {groupWarning}
[BIT 8 ] Service Position  : {servicePos}
[BIT 9 ] CB Close          : {cbClose}
[BIT 10] CB Open           : {cbOpen}";
        }
        private string DecodeCoil(int address)
        {
            switch (address)
            {
                case 1: return "OPEN";
                case 2: return "CLOSE";
                case 3: return "START";
                case 4: return "STOP";
                case 5: return "FAULT RESET";
                case 6: return "LOCAL/REMOTE SELECT";
                default: return "UNKNOWN COIL";
            }
        }

        private string DiagnoseException(Exception ex)
        {
            string msg = ex.Message.ToLower();

            if (msg.Contains("timeout"))
            {
                timeoutCount++;
                return "Timeout - Check slave ID, wiring, or device power";
            }

            if (msg.Contains("illegal"))
            {
                exceptionCount++;
                return "Illegal Address - Check register mapping / PLC Base offset";
            }

            if (msg.Contains("crc"))
            {
                crcSuspectCount++;
                return "CRC Error - Possible cable noise / termination / grounding issue";
            }

            if (msg.Contains("port"))
            {
                exceptionCount++;
                return "COM Port problem - USB unstable or driver issue";
            }

            exceptionCount++;
            return "Unknown Modbus error - Check configuration";
        }

        private string GenerateHealthReport()
        {
            return
        $@"AVG Latency   : {avgLatency:F0} ms
        Jitter         : {jitterMs:F0} ms ({jitterPercent:F1}%)
        Bus Load       : {busLoadPercent:F0} %
        Network Status : {networkStatus}

        ----------------------------------
        Total Poll     : {totalPoll}
        Timeout        : {timeoutCount}
        Exception      : {exceptionCount}
        CRC Suspect    : {crcSuspectCount}";
        }

        private double CalculateJitter()
        {
            if (latencyHistory.Count < 2)
                return 0;

            double avg = latencyHistory.Average();

            double sum = 0;

            foreach (long v in latencyHistory)
            {
                sum += Math.Pow(v - avg, 2);
            }

            return Math.Sqrt(sum / latencyHistory.Count);
        }

        private void MenuAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = menuAlwaysOnTop.IsChecked;
        }

        private string GenerateLayerReport()
        {
            int stability = totalPoll == 0
                ? 100
                : (int)(((double)(totalPoll - timeoutCount - exceptionCount) / totalPoll) * 100);

            return
$@"RELIABILITY
  Stability (Packet Success) : {stability} %
  Timeout                    : {timeoutCount}
  Exception                  : {exceptionCount}
  CRC Suspect                : {crcSuspectCount}

TIMING QUALITY
  Avg Latency                : {avgLatency:F0} ms
  Jitter                     : {jitterMs:F0} ms ({jitterPercent:F1}%)
  Classification             : {networkStatus}

TRAFFIC LOAD
  Bus Load Estimation        : {busLoadPercent:F0} %
  Poll Interval              : {currentInterval} ms";
        }

        private void BtnFault_Click(object sender, RoutedEventArgs e)
        {
            WriteCoilWithTrace(4, "FAULT TRIGGER");
        }
    }
}