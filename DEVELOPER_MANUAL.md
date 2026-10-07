# DEVELOPER MANUAL

## Gambaran Umum

Solution ini adalah aplikasi WPF .NET Framework yang berisi dua executable utama:

- `SimoSim`: Modbus slave simulator berbasis `EasyModbus`.
- `SimoMaster`: Modbus master/HMI test tool berbasis `NModbus4`.

Tujuan arsitekturnya sederhana:

- `SimoSim` mensimulasikan data proses dan status device.
- `SimoMaster` menghubungkan diri ke device/simulator, melakukan polling periodik, menampilkan status, mengirim coil command, dan menganalisis kualitas komunikasi.

## Project Tree Ringkas

```text
C:\CODEX\SimoSim Nmodbus
|-- SimoSim.sln
|-- SimoSim\
|   |-- App.xaml
|   |-- MainWindow.xaml
|   |-- MainWindow.xaml.cs
|   |-- Properties\
|   `-- packages.config
|-- SimoMaster\
|   |-- App.xaml
|   |-- MainWindow.xaml
|   |-- MainWindow.xaml.cs
|   |-- ConnectionWindow.xaml(.cs)
|   |-- RegisterMappingWindow.xaml(.cs)
|   |-- DeviceScanWindow.xaml(.cs)
|   |-- TraceWindow.xaml(.cs)
|   |-- RegisterMap.cs
|   |-- ConnectionConfig.cs
|   |-- Properties\
|   `-- packages.config
|-- SimoMasAri\
|   `-- SimoMasAri.vdproj
`-- packages\
    |-- NModbus4.2.1.0\
    `-- EasyModbusTCP.5.6.0\
```

## Project Map

### `SimoSim`

Peran file utama:

- `App.xaml`, `App.xaml.cs`
  Bootstrapping WPF.

- `MainWindow.xaml`
  UI simulator, kontrol start/stop server, indikator motor/CB, dan tombol manual.

- `MainWindow.xaml.cs`
  Seluruh engine simulator:
  - start/stop Modbus TCP slave,
  - start/stop Modbus RTU slave,
  - simulation loop background thread,
  - pembacaan coil command,
  - pembentukan status word,
  - pembaruan register measurement,
  - update UI.

### `SimoMaster`

Peran file utama:

- `MainWindow.xaml`
  UI utama master/HMI.

- `MainWindow.xaml.cs`
  Core orchestrator aplikasi:
  - koneksi TCP/RTU,
  - polling,
  - parsing status,
  - update HMI,
  - logging,
  - trace output,
  - statistik komunikasi,
  - reconnect,
  - scan device,
  - write coil.

- `ConnectionWindow.xaml.cs`
  Dialog konfigurasi koneksi.

- `ConnectionConfig.cs`
  DTO sederhana untuk parameter koneksi.

- `RegisterMap.cs`
  Central mapping register/coil dan flag polling.

- `RegisterMappingWindow.xaml.cs`
  Dialog edit mapping.

- `DeviceScanWindow.xaml.cs`
  UI hasil scan Unit ID `1..20`.

- `TraceWindow.xaml.cs`
  Viewer untuk trace poll cycle dan indikator health.

### `SimoMasAri`

Installer project (`.vdproj`) untuk packaging/deployment.

## Dependency Map

Library utama:

- `EasyModbus`
  Dipakai oleh `SimoSim` untuk menjalankan `ModbusServer`.

- `NModbus4`
  Dipakai oleh `SimoMaster` untuk `ModbusIpMaster` dan `ModbusSerialMaster`.

- `HMI.Controls`
  Dipakai oleh `SimoMaster` untuk komponen visual motor/CB/HMI.

- `System.Management`
  Dipakai di `SimoMaster` untuk mendeteksi device COM/USB saat reconnect RTU.

## Data Model dan Mapping Penting

### `ConnectionConfig`

Objek runtime untuk konfigurasi koneksi:

- `Mode`
- `IpAddress`
- `UnitId`
- `ComPort`
- `BaudRate`
- `Parity`
- `StopBits`

### `RegisterMap`

Mapping default:

- `StatusWord = 1024`
- `CurrentStart = 2055`
- `VoltageStart = 2060`
- `ActivePower = 2070`
- `ApparentPower = 2071`
- `CoilOpen = 0`
- `CoilClose = 1`
- `CoilStart = 2`
- `CoilStop = 3`
- `CoilFault = 4`
- `CoilReset = 5`

Flag perilaku:

- `PlcBaseAddressing`
- `PollStatusWord`
- `PollCurrent`
- `PollVoltage`
- `PollPower`

### Perbedaan alamat master vs simulator

Simulator menulis pada:

- input register `1025`
- holding register `2056-2058`
- holding register `2061-2063`
- holding register `2071-2072`

Master default membaca:

- `1024`, `2055`, `2060`, `2070`

Perbedaan ini dijembatani oleh method `Normalize()` di `SimoMaster`, tergantung nilai `PlcBaseAddressing`.

## Data Flow

### Flow utama `SimoMaster`

```text
User klik Connect
-> ConnectionWindow kumpulkan input
-> MainWindow.ConnectDevice()
-> buat object master TCP/RTU
-> start pollTimer
-> pollTimer.Tick memanggil PollDevice()
-> ExecutePollCycle() baca register
-> UpdateAfterSuccess() update status/statistik/UI
-> BuildAndSendTrace() bentuk trace text
-> TraceWindow.UpdateTrace() render trace jika window terbuka
```

### Flow error komunikasi

```text
PollDevice()
-> exception
-> HandlePollException()
-> DiagnoseException()
-> AddLog()
-> SendTraceToWindow()
-> jika errorCounter melewati threshold:
   -> StartReconnect() / StartReconnectRTU()
   -> reconnectTimer.Tick
   -> ReconnectTimer_Tick()
```

### Flow command coil

```text
User klik tombol OPEN/CLOSE/START/STOP/FAULT/RESET
-> BtnX_Click()
-> WriteCoilWithTrace()
-> master.WriteSingleCoil()
-> AddLog("Command -> ...")
-> SendTraceToWindow()
```

### Flow simulator `SimoSim`

```text
StartTcpServer() / StartRtuServer()
-> EnsureSimulationRunning()
-> background thread menjalankan SimulationLoop()
-> SimulateLogic()
-> baca coil dari TCP/RTU server
-> update state internal (CB, motor, fault, flags)
-> bentuk statusWord
-> tulis inputRegisters dan holdingRegisters
-> Dispatcher.BeginInvoke(UpdateUI)
```

## Status dan Register Semantik

### Status bit yang dipakai

Baik simulator maupun master mengasumsikan bit berikut:

- bit 1: status off
- bit 2: status on
- bit 5: remote mode
- bit 6: group fault
- bit 7: group warning
- bit 8: service position
- bit 9: CB close
- bit 10: CB open

### Measurement scaling

Di simulator:

- current ditulis sebagai `A * 100`
- voltage ditulis langsung dalam volt
- active power ditulis sebagai `kW * 10`
- apparent power ditulis sebagai `kVA * 10`

Di master:

- current dibaca sebagai `register / 100.0`
- power dibaca sebagai `register / 10.0`

## Method Inti `SimoMaster`

### Koneksi

- `BtnStartSim_Click`
  Entry point user untuk connect/disconnect.

- `ConnectDevice`
  Membuka koneksi TCP atau RTU, menginisialisasi `master`, mengatur timeout/retries, mengubah status UI, dan memulai polling.

- `DisconnectDevice`
  Menghentikan polling/reconnect, dispose master, close socket/serial port, lalu memulihkan status UI.

### Polling

- `PollDevice`
  Handler timer polling. Method ini adalah pusat siklus runtime aplikasi.

- `ExecutePollCycle`
  Menjalankan pembacaan register aktual menggunakan `master.ReadInputRegisters` dan `master.ReadHoldingRegisters`.

- `UpdateAfterSuccess`
  Menangani hasil poll berhasil:
  - parse status,
  - update measurement,
  - simpan latency,
  - hitung statistik,
  - deteksi event komunikasi,
  - deteksi perubahan status word,
  - update counter dan interval polling.

- `BuildAndSendTrace`
  Membuat blok trace string untuk satu poll cycle dan mengirimkannya ke trace window.

- `HandlePollException`
  Menangani error poll:
  - tambah counter,
  - diagnosis error,
  - tulis log,
  - kirim trace error,
  - trigger reconnect bila perlu.

### Statistik komunikasi

- `CalculateStatistics`
  Menghitung:
  - `avgLatency`
  - `jitterMs`
  - `jitterPercent`
  - `busLoadPercent`
  - `networkStatus`

- `AdjustPollingInterval`
  Menentukan interval polling adaptif berdasarkan latency terakhir.

- `UpdateCommLed`
  Memberi warna LED komunikasi berdasar `lastGoodComm` dan `lastLatency`.

### Status dan visualisasi

- `ParseStatus`
  Menerjemahkan status word ke state HMI:
  - CB state,
  - motor state,
  - LED fault/warning/service/remote.

- `UpdateMeasurement`
  Menulis angka current, voltage, power, dan PF ke UI text fields.

- `DecodeStatusWord`
  Membentuk string breakdown status bit untuk trace window.

### Command/control

- `WriteCoilWithTrace`
  Mengirim `FC05 Write Single Coil`, membuat trace command, dan menambah event log.

- `BtnOpen_Click`, `BtnClose_Click`, `BtnStart_Click`, `BtnStop_Click`, `BtnReset_Click`, `BtnFault_Click`
  Wrapper UI yang memanggil `WriteCoilWithTrace`.

### Reconnect

- `StartReconnect`
  Mode reconnect untuk TCP.

- `StartReconnectRTU`
  Mode reconnect untuk RTU.

- `ReconnectTimer_Tick`
  Melakukan percobaan reconnect periodik.

- `GetDeviceInfo`
  Menyimpan `Device ID` Windows dari COM port awal.

- `FindPortByDeviceId`
  Mencari kembali port baru berdasarkan `Device ID` saat reconnect RTU.

### Utility

- `AddLog`
  Menambah item ke event log UI dengan cap maksimal 200 item.

- `DetectBitChange`
  Membandingkan dua status word dan melog bit yang berubah.

- `Normalize`
  Mengubah alamat bila mode PLC base addressing aktif.

- `MenuScanDevices_Click`
  Melakukan scan slave ID `1..20` dan menampilkan hasil di `DeviceScanWindow`.

## Method Inti `SimoSim`

### Server lifecycle

- `StartTcpServer`
  Membuat `ModbusServer`, set `Port=502`, set `UnitIdentifier`, lalu `Listen()`.

- `StopTcpServer`
  Stop listener TCP dan update state/UI.

- `StartRtuServer`
  Membuat `ModbusServer` RTU, set serial settings, set `UnitIdentifier`, lalu `Listen()`.

- `StopRtuServer`
  Stop listener RTU dan update state/UI.

- `EnsureSimulationRunning`
  Memastikan background simulation thread hidup.

- `StopSimulationIfNoServer`
  Menghentikan simulation thread jika TCP dan RTU sama-sama mati.

### Runtime simulator

- `SimulationLoop`
  Loop background dengan jeda `200 ms`.

- `SimulateLogic`
  Pusat engine simulator:
  - baca coil command,
  - reset coil setelah diproses,
  - update state internal,
  - bentuk status word,
  - generate measurement pseudo-random,
  - tulis register ke server,
  - jadwalkan update UI.

- `ReadCoil`
  Helper aman untuk membaca coil dari server.

- `UpdateUI`
  Sinkronisasi state internal ke tampilan simulator.

### Manual UI control

- `BtnClose_Click`
- `BtnOpen_Click`
- `BtnStart_Click`
- `BtnStop_Click`
- `BtnFault_Click`
- `BtnReset_Click`
- `BtnRemote_Click`
- `BtnWarning_Click`
- `BtnService_Click`

Method-method ini mengubah state simulator langsung tanpa transaksi Modbus.

## Hubungan Antar Method Penting

### Hubungan polling sukses

```text
PollDevice
-> ExecutePollCycle
-> UpdateAfterSuccess
   -> ParseStatus
   -> UpdateMeasurement
   -> CalculateStatistics
   -> DetectBitChange
   -> AdjustPollingInterval
-> BuildAndSendTrace
-> SendTraceToWindow
-> TraceWindow.UpdateTrace
```

### Hubungan polling gagal

```text
PollDevice
-> HandlePollException
   -> DiagnoseException
   -> AddLog
   -> SendTraceToWindow
   -> StartReconnect / StartReconnectRTU
-> reconnectTimer
-> ReconnectTimer_Tick
```

### Hubungan command coil

```text
BtnStart_Click / BtnStop_Click / dll
-> WriteCoilWithTrace
-> master.WriteSingleCoil
-> AddLog
-> SendTraceToWindow
```

### Hubungan simulator

```text
StartTcpServer / StartRtuServer
-> EnsureSimulationRunning
-> SimulationLoop
-> SimulateLogic
   -> ReadCoil
   -> tulis statusWord/register
   -> UpdateUI
```

## Event Log dan Diagnostik Internal

Log di `SimoMaster` berasal dari `AddLog()`. Event penting yang diproduksi kode:

- koneksi: `Connecting...`, `TCP Connected`, `RTU Connected`, `Connected Successfully`, `Disconnected`
- komunikasi: `Polling Started`, `Status Word Changed`, `Bit X -> ON/OFF`
- timing: `CRITICAL LATENCY`, `Jitter spike`, `BUS LOAD HIGH`
- recovery: `Starting Auto Reconnect...`, `Attempting Reconnect...`, `Reconnected Successfully`
- command: `Command -> ...`, `Coil Error -> ...`

Diagnosis error berasal dari `DiagnoseException()`:

- `timeout` -> timeoutCount++
- `illegal` -> exceptionCount++
- `crc` -> crcSuspectCount++
- `port` -> exceptionCount++
- selain itu -> exceptionCount++

## Threshold dan Heuristik Runtime

### Timeout dan reconnect

- `master.Transport.ReadTimeout = 2000`
- connect TCP timeout awal: `3000 ms`
- reconnect TCP timeout: `2000 ms`
- threshold reconnect TCP: `errorCounter >= 3`
- threshold reconnect RTU: `errorCounter >= 5`

### Adaptive polling

- `normalInterval = 500`
- `slowInterval = 1000`
- `criticalInterval = 2000`
- initial `currentInterval = 800`

Logika:

- `lastLatency > 800` -> interval critical
- `lastLatency > 400` -> interval slow
- `lastLatency < 250` -> interval normal

### Latency/jitter/load alarms

- `latency > 1000 ms` -> `CRITICAL LATENCY`
- `latency < 600 ms` setelah high state -> `Latency back to normal`
- `avgLatency > 20 && jitterPercent > 40` -> `Jitter spike`
- `jitterPercent < 25` setelah high state -> `Jitter stabilized`
- `busLoadPercent > 80` -> `BUS LOAD HIGH`
- `busLoadPercent < 60` setelah high state -> `Bus load normalized`

### Network classification

Di `CalculateStatistics()`:

- `avgLatency < 50` -> `LOCAL LOOPBACK`, `jitterPercent = 0`
- `jitterPercent < 10 && busLoadPercent < 70` -> `STABLE`
- `jitterPercent < 25 && busLoadPercent < 85` -> `STABLE - HIGH LOAD`
- `jitterPercent >= 25 && busLoadPercent < 85` -> `UNSTABLE BUS`
- selain itu -> `NEAR SATURATION`

## UI Window Responsibilities

### `ConnectionWindow`

- memvalidasi `Unit ID` (`0..247`),
- switch UI TCP vs RTU,
- menyimpan history IP/Unit ID,
- menyimpan last-used settings.

### `RegisterMappingWindow`

- mengedit alamat register/coil,
- memilih `PLC base addressing`.

### `DeviceScanWindow`

- menampung `ObservableCollection<ScanResult>`,
- mewarnai row berdasarkan `Indicator`.

### `TraceWindow`

- render raw trace text,
- menampilkan latency/stability/error,
- mengubah warna indikator berdasarkan kualitas komunikasi.

## State Runtime Penting

### `SimoMaster`

Field yang mengendalikan runtime:

- `connected`
- `reconnecting`
- `currentConfig`
- `master`, `client`, `serialPort`
- `pollTimer`, `reconnectTimer`, `ledTimer`
- `txCounter`, `rxCounter`, `errorCounter`
- `timeoutCount`, `exceptionCount`, `crcSuspectCount`
- `latencyHistory`, `avgLatency`, `jitterPercent`, `busLoadPercent`
- `previousStatusWord`, `firstPoll`, `pollingStartedLogged`

### `SimoSim`

Field state simulator:

- `tcpServer`, `rtuServer`
- `tcpRunning`, `rtuRunning`
- `simulationThread`, `simulationRunning`
- `cbClosed`
- `motorRunning`
- `faultActive`
- `remoteActive`
- `warningActive`
- `servicePosActive`
- measurement fields dan `statusWord`

## Catatan Teknis Penting

### 1. Threading

`SimoSim` menjalankan engine di background `Thread`, lalu update UI via `Dispatcher.BeginInvoke`.

`SimoMaster` memakai `DispatcherTimer`, jadi kebanyakan flow polling berjalan di UI thread. Ini sederhana, tetapi berarti pekerjaan berat di polling bisa memengaruhi respons UI.

### 2. Error handling

Error handling berbasis string matching pada `Exception.Message`. Ini cepat diterapkan, tetapi rapuh terhadap perubahan message library/dependency.

### 3. Address normalization

`Normalize()` sangat penting untuk menghindari off-by-one antara alamat PLC-style dan zero-based Modbus addressing.

### 4. Persisted settings

Kedua project menyimpan setting terakhir di `Properties.Settings`.

### 5. Trace as formatted text

Trace saat ini bukan objek model terstruktur, melainkan string multiline yang dibentuk di `BuildAndSendTrace()` dan `WriteCoilWithTrace()`.

## Area yang Paling Sering Diubah Saat Maintenance

- `SimoMaster/MainWindow.xaml.cs`
  Jika mengubah flow polling, log, reconnect, atau command.

- `SimoMaster/RegisterMap.cs`
  Jika mapping register default berubah.

- `SimoSim/MainWindow.xaml.cs`
  Jika perilaku simulator, status bit, atau data pengukuran berubah.

- `SimoMaster/TraceWindow.xaml.cs`
  Jika ingin mengubah interpretasi warna/health display.

## Strategi Onboarding Developer Baru

Urutan baca yang disarankan:

1. `SimoMaster/MainWindow.xaml.cs`
2. `SimoMaster/RegisterMap.cs`
3. `SimoMaster/ConnectionWindow.xaml.cs`
4. `SimoSim/MainWindow.xaml.cs`
5. `SimoMaster/TraceWindow.xaml.cs`

Tujuannya:

- pahami dulu orchestration master,
- lalu pahami kontrak data/register,
- baru pahami perilaku simulator.

## Ringkasan Singkat Arsitektur

Secara desain, ini adalah arsitektur event-driven sederhana:

- UI menjadi pengendali utama,
- timer memicu polling,
- hasil polling mengalir ke parser/status/statistik/log,
- simulator menyediakan model device minimal yang cukup untuk test command dan quality monitoring,
- semua orchestration besar saat ini tersentralisasi di `MainWindow.xaml.cs` masing-masing project.
