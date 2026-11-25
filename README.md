# Bomb Defusal AR–VR

Aplikasi Android berbasis Unity yang menggabungkan marker AR dengan simulasi penjinakan bom di ruang virtual 3D. Kamera HP mengenali marker, lalu bom 3D dan empat tombol warna muncul di atasnya. Pengguna menjawab teka-teki warna dengan menutupi tombol memakai tangan, kemudian masuk ke lingkungan Bombsite B untuk mencari bom lewat suara dan memotong kabel yang benar.

## Informasi Proyek

| Informasi | Keterangan |
|---|---|
| Nama proyek | Bomb Defusal AR–VR (`bombAR`) |
| Unity | Unity 6.6 (`6000.6.0f1`) |
| Render pipeline | Universal Render Pipeline (URP) 17.6.0 |
| AR | AR Foundation 6.6.2, Google ARCore XR Plugin 6.6.2 |
| Input | Input System 1.20.0 (Enhanced Touch) |
| Target | Android ARM64, IL2CPP, OpenGLES3, minimum API 26 |
| Scene AR | `Assets/Scenes/Scenes/SampleScene.unity` (index 0) |
| Scene VR | `Assets/Scenes/Virtual/VirtualRoom.unity` (index 1) |
| Dokumentasi | [Storyboard Bomb Defusal AR–VR](./STORYBOARD%20APLIKASI%20BOMB%20DEFUSAL%20AR%E2%80%93VR.pdf) |

## Status Pengerjaan

Seluruh 11 tahap storyboard sudah diimplementasikan.

| Tahap | Bagian | Status |
|:---:|---|:---:|
| 1 | Deteksi marker bom | ✅ |
| 2 | Bom dan virtual button muncul | ✅ |
| 3 | Masuk ke ruang virtual lewat tombol VR | ✅ |
| 4 | Ruang virtual Bombsite B | ✅ |
| 5 | Bergerak dengan analog virtual | ✅ |
| 6 | Mencari bom lewat spatial audio | ✅ |
| 7 | Mengambil tang | ✅ |
| 8 | Memilih kabel (highlight) | ✅ |
| 9 | Memotong kabel berbasis fisika | ✅ |
| 10 | Hasil berhasil atau gagal | ✅ |
| 11 | Ulangi simulasi atau kembali ke AR | ✅ |

## Cara Kerja

### Fase AR (`SampleScene`)

1. Kamera memindai marker `bomb_blueprint` (ukuran fisik 21 × 12 cm).
2. `ARTrackedImageManager` memunculkan `Bomb_Prefab` di atas marker: model bom, teks soal, timer, dan empat tombol warna (Merah, Biru, Hijau, Kuning).
3. `BombManager` menampilkan teka-teki warna, misalnya "Pilih kabel yang warnanya seperti langit cerah!". Batas waktunya 50 detik, dan pengguna menang di AR setelah 5 jawaban benar.
4. Tombol dipilih tanpa menyentuh layar. `VirtualButtonDetector` mengkalibrasi gambar kamera di area tiap tombol, lalu mendeteksi tombol yang tertutup tangan dari perubahan piksel.
5. Setiap kali soal baru muncul, jawabannya disimpan ke `PlayerPrefs` dengan kunci `BombCorrectColor`.
6. Tombol **VR** di kanan atas layar membuka scene `VirtualRoom`.

### Fase VR (`VirtualRoom`)

1. Pemain bergerak dengan analog virtual di kiri bawah dan menggeser layar untuk melihat sekeliling.
2. Bunyi beep berasal dari posisi bom. Suaranya memakai audio 3D (jarak 0,8–12 m), jadi arah dan volumenya berubah sesuai posisi pemain.
3. Saat pemain cukup dekat dengan tang (2,2 m), tombol **AMBIL TANG** muncul.
4. Pemain membidik salah satu dari empat kabel. Kabel yang dibidik diberi highlight, lalu tombol **POTONG** muncul.
5. Kabel harus dipotong berurutan sesuai clue di bom. Clue pertama berasal dari jawaban soal AR terakhir; sisanya mengikuti urutan Merah, Biru, Hijau, Kuning. Kabel yang dipotong jatuh secara fisik (Rigidbody).
6. Semua kabel terpotong dengan benar menampilkan **BOMB DEFUSED**. Kabel salah, atau hitung mundur yang habis sekitar 34 detik setelah masuk, menampilkan **YOU DIED**.
7. Panel akhir menyediakan tombol **AR** untuk kembali memindai marker dan **VR** untuk mengulang simulasi.

Seluruh logika VR ada di `VirtualRoomController`, yang terpasang pada objek `Player`.

## Struktur Folder

```text
Assets/
├── Ar/ReferenceImages/              BombImageLibrary (marker bomb_blueprint)
├── Art/
│   ├── Audio/Bomb/                  bombSound.mp3 (beep dan ledakan)
│   ├── Markers/                     gambar marker
│   ├── Materials/Bomb/              material bom dan tombol warna
│   ├── Models/Bomb/                 model bom (sss.fbx)
│   ├── Models/Environment/          model Bombsite B dan teksturnya
│   ├── Models/VirtualRoom/          mesh empat kabel
│   └── Videos/                      render texture transisi kalah
├── Materials/VirtualRoom/           material kabel dan tang
├── Prefabs/
│   ├── AR/Bomb_Prefab.prefab        bom AR, dipakai ulang di VR
│   └── UI/WorldSpace/               teks soal dan timer 3D
├── Scenes/
│   ├── Scenes/SampleScene.unity     scene AR
│   └── Virtual/VirtualRoom.unity    scene VR
└── Scripts/
    ├── Editor/
    │   ├── AR/SetupVirtualButtons.cs
    │   └── Build/ArCoreTempPathWorkaround.cs
    └── Runtime/
        ├── AR/VirtualButtonDetector.cs
        ├── Bomb/BombManager.cs
        ├── Transition/ARToVirtualTransition.cs
        └── VirtualRoomController.cs
```

| Skrip | Fungsi |
|---|---|
| `VirtualButtonDetector` | Kalibrasi dan deteksi tombol warna yang ditutup tangan |
| `BombManager` | Teka-teki warna, timer, penilaian AR, dan menyimpan jawaban untuk VR |
| `ARToVirtualTransition` | Pindah scene AR ↔ VR (`EnterVirtualRoom`, `ReturnToAR`) |
| `VirtualRoomController` | Gerak, spatial audio, ambil tang, pilih dan potong kabel, hasil akhir |
| `ArCoreTempPathWorkaround` | Perbaikan build ARCore di Windows (lihat Kendala) |

## Membuka Proyek

1. Instal Unity Editor `6000.6.0f1` melalui Unity Hub, beserta Android Build Support, Android SDK & NDK Tools, dan OpenJDK.
2. Buka folder `bombAR` melalui Unity Hub dan tunggu impor selesai.
3. Pastikan Console tidak menampilkan error kompilasi.
4. Buka **File → Build Profiles → Scene List** dan pastikan urutannya `SampleScene` (0) lalu `VirtualRoom` (1). Urutan ini dipakai oleh tombol VR dan AR.

## Menjalankan dan Menguji

### Fase AR (harus di HP)

Kamera AR tidak tersedia di Game View, jadi fase AR diuji langsung di ponsel Android.

1. Hubungkan ponsel dengan USB debugging aktif, pilih profil Android, lalu jalankan **Build and Run**.
2. Izinkan akses kamera.
3. Tampilkan `Assets/Art/Markers/bomb_blueprint.jpeg` di layar lain atau cetak, lalu arahkan kamera sampai bom muncul.
4. Jangan menutupi marker selama kalibrasi awal.
5. Setelah kalibrasi selesai, tutupi bagian tengah satu tombol warna dengan tangan dan tahan sebentar. Hindari menutup dua tombol sekaligus atau perubahan cahaya mendadak.
6. Tekan tombol **VR** untuk masuk ke ruang virtual.

### Fase VR (bisa di Editor)

Buka `VirtualRoom.unity` lalu tekan Play. Tanpa lewat AR, kabel pertama otomatis Merah.

| Aksi | Di Editor | Di HP |
|---|---|---|
| Bergerak | W A S D | Analog kiri bawah |
| Melihat sekeliling | Klik kanan + geser mouse | Geser layar |
| Ambil tang | E | Tombol AMBIL TANG |
| Potong kabel | F | Tombol POTONG |

## Pengaturan yang Sering Diubah

| Yang diubah | Lokasi |
|---|---|
| Kecepatan jalan, sensitivitas pandang | Inspector `Player` → Move Speed, Look Speed |
| Batas waktu di VR | Inspector `Player` → Bomb Audio Start Seconds dan Bomb Explosion Time Seconds |
| Jarak ambil tang dan bidik kabel | Inspector `Player` → Pickup Distance, Cable Aim Distance |
| Batas waktu dan jumlah soal di AR | Inspector `BombManager` di `SampleScene` → Time Limit, Questions To Win |
| Kepekaan tombol tangan | Inspector `VirtualButtons` → Press Threshold, Release Threshold |
| Daftar soal AR | `BombManager.cs`, bagian `puzzleLibrary` |
| Teks clue dan urutan kabel VR | `VirtualRoomController.cs`, fungsi `GetBombClue` dan daftar `remainingCableColors` |

Nama objek kabel (`Cable_Red`, `Cable_Blue`, `Cable_Green`, `Cable_Yellow`) dipakai kode untuk mengenali warna, jadi jangan diganti.

## Kendala yang Pernah Terjadi

- **Build ARCore error di Windows.** ARCore 6.6.2 memanggil `arcoreimg` dengan path folder sementara tanpa tanda kutip, sehingga gagal jika path folder pengguna mengandung spasi. `ArCoreTempPathWorkaround` memindahkan folder sementara ke path tanpa spasi selama build, lalu mengembalikannya.
- **Virtual button peka cahaya.** Deteksi bergantung pada perubahan gambar kamera. Lakukan kalibrasi tanpa menutupi marker dan uji di cahaya yang stabil.
- **Ukuran map.** Model de_dust2 dipotong sehingga hanya bagian Bombsite B yang dipakai (1.395 segitiga, 16 material).

## Checklist Sebelum Build

- Console tidak memiliki error merah.
- `SampleScene` di index 0 dan `VirtualRoom` di index 1.
- `BombImageLibrary` berisi marker `bomb_blueprint`.
- `XR Origin` → Tracked Image Prefab berisi `Bomb_Prefab`.
- Semua referensi di Inspector `Player` dan `VirtualButtons` terisi.
- Hasil build disimpan di luar folder `Assets`.

## Sumber Aset

- Lingkungan Bombsite B: [de_dust2 - CS map](https://sketchfab.com/3d-models/de-dust2-cs-map-056008d59eb849a29c0ab6884c0c3d87) oleh pancakesbassoondonut (@vrchris) di Sketchfab, lisensi [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/). Model dipotong sehingga hanya bagian Bombsite B yang dipakai.
- Tang disusun dari objek primitif Unity, dan mesh empat kabel dibuat di dalam proyek.
