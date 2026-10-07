# USER MANUAL

## Tujuan Project

Project ini terdiri dari dua aplikasi desktop WPF:

- `SimoSim`: simulator device Modbus yang bertindak sebagai slave/server.
- `SimoMaster`: aplikasi master/client untuk connect, polling data, mengirim command coil, melihat trace, dan memonitor kualitas komunikasi.

Secara praktis, project ini dipakai untuk simulasi dan pengujian komunikasi Modbus TCP atau Modbus RTU dengan perilaku yang menyerupai perangkat motor/CB/SIMOCODE sederhana.

## Aplikasi yang Dipakai User

### 1. `SimoSim`

Dipakai untuk menjalankan simulator device.

Fungsi utama:

- Menyediakan Modbus TCP slave pada port `502`.
- Menyediakan Modbus RTU slave pada COM port yang dipilih.
- Menghasilkan `status word`, nilai arus, tegangan, daya aktif, dan daya semu.
- Menerima command coil seperti `OPEN`, `CLOSE`, `START`, `STOP`, `FAULT`, dan `RESET`.

### 2. `SimoMaster`

Dipakai untuk menghubungkan ke device/simulator dan memonitor komunikasi.

Fungsi utama:

- Connect ke device lewat `Modbus TCP` atau `Modbus RTU`.
- Poll `status word`, current, voltage, dan power.
- Menampilkan status motor/CB dan indikator LED.
- Mengirim command coil.
- Menampilkan event log dan trace komunikasi.
- Menghitung kualitas komunikasi seperti `latency`, `jitter`, `bus load`, dan stabilitas.

## Cara Menjalankan

### Skenario paling umum: test lokal di satu PC

1. Jalankan `SimoSim.exe`.
2. Isi `Unit ID`.
3. Klik `Start Modbus TCP` atau `Start Modbus RTU`.
4. Jalankan `SimoMaster.exe`.
5. Klik `Connect`.
6. Pilih mode yang sama dengan simulator:
   - `Modbus TCP`: isi IP, biasanya `127.0.0.1`.
   - `Modbus RTU`: pilih COM port, baudrate, parity, stop bits.
7. Isi `Unit ID` yang sama dengan simulator.
8. Klik `OK`.
9. Jika berhasil, status berubah menjadi `Connected` dan polling otomatis berjalan.

## Cara Penggunaan `SimoSim`

### Menjalankan simulator TCP

1. Isi `Unit ID`.
2. Klik `Start Modbus TCP`.
3. Status akan berubah menjadi `TCP Slave Running`.

### Menjalankan simulator RTU

1. Pilih `COM Port`, `Baud`, `Parity`, `Data Bits`, dan `Stop Bits`.
2. Isi `Unit ID`.
3. Klik `Start Modbus RTU`.
4. Status akan berubah menjadi `RTU Slave Running`.

### Tombol manual pada simulator

- `OPEN`: membuka CB, motor berhenti.
- `CLOSE`: menutup CB jika tidak fault.
- `START`: menjalankan motor jika CB sudah close dan tidak fault.
- `STOP`: menghentikan motor.
- `FAULT`: memaksa fault aktif, motor stop, CB open.
- `RESET`: menghapus fault.
- `REMOTE`: toggle bit remote mode.
- `WARNING`: toggle warning.
- `SERVICE`: toggle service position.

### Data yang disimulasikan

Simulator menulis data Modbus berikut:

- `Status Word` di input register `1025` pada sisi simulator.
- Arus tiga fasa di holding register `2056-2058`.
- Tegangan antar fasa di holding register `2061-2063`.
- Daya aktif dan daya semu di holding register `2071-2072`.

Catatan:

- Di `SimoMaster`, default mapping memakai alamat `1024`, `2055`, `2060`, `2070` karena ada opsi normalisasi alamat untuk kebutuhan zero-based vs PLC-based addressing.

## Cara Penggunaan `SimoMaster`

### Connect ke device

1. Klik `Connect`.
2. Pilih `Modbus TCP` atau `Modbus RTU`.
3. Isi parameter koneksi.
4. Klik `OK`.

Jika berhasil:

- Tombol berubah menjadi `Disconnect`.
- Status menjadi `Connected`.
- Polling mulai otomatis.
- Event log menampilkan `Connected Successfully`.

### Mengirim command

Tombol command utama:

- `OPEN`
- `CLOSE`
- `START`
- `STOP`
- `FAULT`
- `RESET`

Setiap command akan mengirim `FC05 Write Single Coil`.

### Register Mapping

Menu `Register Mapping` dipakai jika alamat register/coil device berbeda dari default.

Gunakan menu ini jika:

- device memakai alamat PLC-based,
- device memakai offset register berbeda,
- coil command tidak merespons karena alamat salah.

### Modbus Frame Trace

Menu `Modbus Frame Trace` menampilkan:

- isi poll cycle,
- register yang dibaca,
- nilai hasil pembacaan,
- diagnosis komunikasi,
- ringkasan reliability/timing/load.

### Scan Devices

Menu `Scan Devices` mencoba Unit ID `1` sampai `20`.

Hasil scan:

- `ONLINE`: device merespons.
- `NO RESPONSE`: tidak ada respons.
- indikator `✔`: respons normal.
- indikator `⚠`: respons lambat.
- indikator `✖`: tidak respons / error.

## Arti Status Word dan Bit Penting

`SimoMaster` dan `SimoSim` sama-sama memakai bit status berikut:

- `Bit 1`: `Status OFF`
- `Bit 2`: `Status ON`
- `Bit 5`: `Remote Mode`
- `Bit 6`: `Group Fault`
- `Bit 7`: `Group Warning`
- `Bit 8`: `Service Position`
- `Bit 9`: `CB Close`
- `Bit 10`: `CB Open`

Interpretasi umumnya:

- `Status ON`: motor dianggap running.
- `Status OFF`: motor dianggap tidak running.
- `Remote Mode`: device sedang pada mode remote.
- `Group Fault`: fault aktif.
- `Group Warning`: warning aktif.
- `Service Position`: device/service mode aktif.
- `CB Close`: CB tertutup.
- `CB Open`: CB terbuka.

## Arti Event Log

Di bawah ini adalah event-event yang benar-benar muncul di event log `SimoMaster`.

### Event koneksi

- `Connecting...`
  Arti: aplikasi sedang mencoba membangun koneksi ke device.

- `TCP Connected -> IP:502`
  Arti: koneksi TCP berhasil.

- `Attempt RTU Connect -> COMx`
  Arti: aplikasi sedang mencoba membuka port serial.

- `RTU Connected -> COMx`
  Arti: koneksi RTU berhasil.

- `Connected Successfully`
  Arti: koneksi selesai dan polling siap berjalan.

- `Connection Failed: ...`
  Arti: koneksi gagal. Penyebabnya ada pada teks setelah titik dua.

- `Disconnected`
  Arti: user melakukan disconnect atau koneksi ditutup normal.

- `Disconnect Error: ...`
  Arti: ada masalah saat proses disconnect.

### Event polling dan komunikasi

- `Polling Started`
  Arti: timer polling mulai aktif.

- `Status Word Changed 0xAAAA -> 0xBBBB`
  Arti: nilai status word berubah dibanding pembacaan sebelumnya.

- `Bit X -> ON/OFF`
  Arti: bit tertentu dalam status word baru saja berubah.

### Event kualitas komunikasi

- `CRITICAL LATENCY N ms`
  Arti: satu siklus polling membutuhkan waktu sangat lama, lebih dari `1000 ms`.

- `Latency back to normal`
  Arti: latency yang sebelumnya tinggi sudah turun di bawah ambang normalisasi internal.

- `Jitter spike N%`
  Arti: variasi latency antar poll sedang tinggi. Respons device tidak konsisten.

- `Jitter stabilized`
  Arti: variasi latency sudah kembali lebih stabil.

- `BUS LOAD HIGH N%`
  Arti: estimasi beban kanal komunikasi sedang tinggi.

- `Bus load normalized`
  Arti: estimasi beban kanal sudah turun lagi.

### Event command

- `Command -> OPEN`
- `Command -> CLOSE`
- `Command -> START`
- `Command -> STOP`
- `Command -> FAULT RESET`
- `Command -> FAULT TRIGGER`

Arti: command coil berhasil dikirim ke slave.

- `Coil Error -> ...`
  Arti: pengiriman coil gagal.

### Event error dan recovery

- `Modbus Exception: ...`
  Arti: terjadi error saat polling register.

- `Starting Auto Reconnect...`
  Arti: pada mode TCP, aplikasi mulai mode reconnect otomatis.

- `RTU Reconnect Triggered...`
  Arti: pada mode RTU, aplikasi mulai mencoba sambung ulang port serial.

- `Attempting Reconnect...`
  Arti: satu percobaan reconnect sedang dijalankan.

- `RTU Reconnected on COMx`
  Arti: device serial ditemukan lagi pada port tersebut.

- `Reconnected Successfully`
  Arti: reconnect berhasil dan polling dimulai lagi.

### Event utility

- `Log Cleared`
  Arti: log dibersihkan oleh user.

- `Counters Reset`
  Arti: counter komunikasi di-reset.

- `Register Mapping Updated`
  Arti: mapping register dan coil diganti oleh user.

## Penjelasan Istilah Penting di Event Log

### Latency

`Latency` adalah waktu yang dibutuhkan satu transaksi/poll dari master sampai menerima jawaban dari slave.

Dalam project ini:

- latency diukur dalam `ms`,
- nilai diambil dari total waktu pembacaan status, current, voltage, dan power dalam satu poll cycle.

Interpretasi praktis:

- kecil = respons cepat,
- besar = respons lambat,
- sangat besar = indikasi jaringan padat, device lambat, port serial bermasalah, atau timeout mendekati gagal.

Ambang internal project:

- `> 600 ms`: dianggap lambat,
- `> 1000 ms`: dianggap critical latency.

### Jitter

`Jitter` adalah besarnya perubahan/ketidakstabilan latency dari satu poll ke poll berikutnya.

Sederhananya:

- kalau latency selalu mirip, jitter rendah,
- kalau latency naik turun liar, jitter tinggi.

Dalam project ini:

- dihitung dari riwayat 20 latency terakhir,
- menggunakan penyebaran terhadap rata-rata latency,
- ditampilkan sebagai `ms` dan `%`.

Interpretasi:

- `jitter` tinggi berarti komunikasi tidak konsisten,
- walaupun belum timeout, jitter tinggi sering menjadi tanda awal masalah kabel, USB serial, noise, beban CPU, atau jaringan tidak stabil.

Perilaku internal:

- jika `avgLatency < 50 ms`, sistem menandai `LOCAL LOOPBACK` dan `jitterPercent` dipaksa `0` agar tidak muncul alarm palsu.

### Bus Load

`Bus Load` adalah estimasi seberapa besar waktu polling menghabiskan interval komunikasi yang tersedia.

Dalam project ini:

- rumus pendeknya: `avgLatency / currentInterval * 100`.

Contoh:

- jika interval polling `500 ms` dan rata-rata latency `100 ms`, bus load sekitar `20%`.
- jika interval `500 ms` dan rata-rata latency `450 ms`, bus load sangat tinggi.

Interpretasi:

- rendah = komunikasi lega,
- tinggi = polling hampir memenuhi seluruh slot waktu,
- terlalu tinggi = rawan delay, jitter, dan timeout.

### Stability

`Stability` adalah perkiraan persentase keberhasilan paket/poll.

Dalam project ini dihitung dari:

- total poll,
- dikurangi timeout,
- dikurangi exception.

Semakin kecil nilainya, semakin sering komunikasi gagal.

### Network Status / Classification

Klasifikasi kualitas komunikasi di trace window:

- `LOCAL LOOPBACK`
  Biasanya komunikasi lokal dengan latency sangat kecil. Alarm jitter dinonaktifkan agar tidak menyesatkan.

- `STABLE`
  Jitter rendah dan bus load masih aman.

- `STABLE - HIGH LOAD`
  Masih stabil, tetapi beban komunikasi mulai tinggi.

- `UNSTABLE BUS`
  Variasi delay cukup besar. Ada gejala ketidakstabilan.

- `NEAR SATURATION`
  Kanal hampir jenuh. Respons rawan lambat dan error.

## Arti Error yang Umum

### Timeout

Jika pesan error mengandung `timeout`, project mengartikan ini sebagai:

- slave tidak merespons dalam batas waktu,
- slave ID salah,
- device mati,
- kabel serial/TCP putus,
- wiring salah,
- IP salah,
- COM port salah,
- baud/parity/stop bits tidak cocok.

Di internal aplikasi, timeout menaikkan counter `timeoutCount`.

### Illegal Address

Jika pesan error mengandung `illegal`, artinya master membaca alamat register yang tidak valid bagi slave.

Penyebab umum:

- register mapping salah,
- beda zero-based vs PLC-based addressing,
- alamat input register/holding register tidak sesuai device.

Solusi utama:

- cek menu `Register Mapping`,
- cek opsi `PLC Base Addressing`,
- cocokkan dengan map register device sebenarnya.

### CRC Error

Jika pesan mengandung `crc`, aplikasi menganggap ada kemungkinan gangguan integritas data pada serial line.

Penyebab umum:

- noise,
- grounding jelek,
- kabel serial buruk,
- termination tidak benar,
- converter USB-RS485 tidak stabil.

### COM Port Problem

Jika pesan mengandung `port`, aplikasi menganggap ada masalah pada port serial.

Penyebab umum:

- COM port dipakai aplikasi lain,
- USB serial tercabut,
- driver bermasalah,
- nomor COM berubah.

### Unknown Modbus Error

Ini berarti error tidak cocok dengan kategori internal timeout/illegal/crc/port.

Yang perlu dicek:

- parameter koneksi,
- mapping register,
- kondisi device,
- log Windows atau driver serial,
- trace komunikasi.

## Auto Reconnect

Auto reconnect bekerja otomatis saat error polling berulang:

- mode `Modbus TCP`: mulai reconnect setelah `3` error beruntun,
- mode `Modbus RTU`: mulai reconnect setelah `5` error beruntun.

Pada RTU, aplikasi mencoba mencari kembali device berdasarkan `Device ID` USB-serial yang sebelumnya tersimpan.

## Tips Troubleshooting

### Tidak bisa connect TCP

Periksa:

- IP address benar,
- port `502` terbuka,
- simulator/slave sudah running,
- `Unit ID` sesuai.

### Tidak bisa connect RTU

Periksa:

- COM port benar,
- baudrate cocok,
- parity cocok,
- stop bits cocok,
- kabel serial/USB converter sehat.

### Connected tapi data tidak masuk

Periksa:

- register mapping benar,
- opsi `PLC Base Addressing` sesuai,
- slave ID benar,
- simulator memang menulis register yang dipoll.

### Sering muncul timeout

Periksa:

- kualitas kabel,
- kestabilan USB serial,
- device overload,
- polling terlalu agresif,
- alamat device salah sehingga slave tidak menjawab.

### Sering muncul jitter spike

Periksa:

- PC sedang berat,
- converter serial kurang stabil,
- jaringan TCP sibuk,
- ada delay acak dari simulator/device,
- bus load terlalu tinggi.

## Ringkasan Alur Test yang Direkomendasikan

1. Jalankan `SimoSim`.
2. Aktifkan `Modbus TCP` dengan `Unit ID` tertentu.
3. Jalankan `SimoMaster`.
4. Connect ke `127.0.0.1` dengan `Unit ID` yang sama.
5. Pastikan polling berjalan dan current/voltage/power muncul.
6. Coba tombol `CLOSE`, lalu `START`.
7. Amati perubahan `Status Word Changed` dan bit-bit terkait.
8. Coba `FAULT`, lalu `RESET`.
9. Buka `Modbus Frame Trace` untuk melihat detail transaksi.

## Batasan yang Perlu Diketahui

- Event log adalah interpretasi aplikasi, bukan packet analyzer mentah.
- Threshold seperti latency dan reconnect menggunakan angka tetap dari kode.
- Mapping default bisa berbeda dengan device asli di lapangan.
- Pada loopback/localhost, pembacaan jitter sengaja disederhanakan supaya tidak menghasilkan alarm palsu.
