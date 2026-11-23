# Bomb Defusal AR–VR

Proyek Unity Android yang menggabungkan marker-based AR dengan simulasi penjinakan bom di ruang virtual 3D. Kamera mengenali marker, menampilkan bom dan virtual button, kemudian pengguna masuk ke lingkungan Bombsite B untuk menyelesaikan simulasi.

## Informasi Proyek

| Informasi | Keterangan |
|---|---|
| Nama proyek | Bomb Defusal AR–VR (`bombAR`) |
| Unity | Unity 6.6 — `6000.6.0f1` |
| Target | Android |
| Render pipeline | Universal Render Pipeline (URP) |
| Teknologi AR | AR Foundation |
| Scene AR | `Assets/Scenes/Scenes/SampleScene.unity` |
| Scene simulasi | `Assets/Scenes/Virtual/VirtualRoom.unity` |
| Lingkungan 3D | Bombsite B |
| Dokumentasi | [Storyboard Bomb Defusal AR–VR](./STORYBOARD%20APLIKASI%20BOMB%20DEFUSAL%20AR%E2%80%93VR.pdf) |

## Alur Proyek Sesuai Storyboard

| Tahap | Bagian | Alur |
|:---:|---|---|
| 1 | Deteksi marker bom | Kamera mengenali dan melacak marker bom. |
| 2 | Bom dan virtual button muncul | Bom 3D dan tombol virtual ditampilkan di atas marker. |
| 3 | Tekan virtual button Mulai | Tombol Mulai memindahkan aplikasi dari AR ke ruang simulasi. |
| 4 | Masuk ke ruang virtual | Aplikasi membuka lingkungan 3D Bombsite B. |
| 5 | Bergerak menggunakan analog | Pengguna menggerakkan karakter dengan analog virtual. |
| 6 | Mencari bom melalui spatial audio | Bunyi beep berubah arah dan volume mengikuti jarak pemain. |
| 7 | Mengambil tang | Pengguna mendekati tang lalu menekan tombol Ambil. |
| 8 | Memilih kabel | Tang diarahkan ke kabel dan kabel target mendapatkan highlight. |
| 9 | Memotong kabel | Tombol Potong menjalankan interaksi pemotongan kabel. |
| 10 | Hasil berhasil atau gagal | Kabel benar menghentikan bom; kabel salah memicu kegagalan. |
| 11 | Ulangi atau kembali ke AR | Pengguna mengulang simulasi atau kembali memindai marker. |

## Status Pengerjaan

| Tahap | Status | Keterangan |
|:---:|:---:|---|
| 1–2 | 🟢 Tersedia | Deteksi marker, bom 3D, dan virtual button tersedia. |
| 3 | 🟢 Tersedia | Tombol Mulai terhubung ke perpindahan scene. |
| 4 | 🟡 Dikerjakan | Scene VirtualRoom dan Bombsite B telah disiapkan. |
| 5–11 | ⚪ Berikutnya | Fitur dibuat bertahap mengikuti storyboard. |

## Struktur Folder Penting

```text
Assets/
├── Ar/ReferenceImages/              Reference Image Library
├── Art/
│   ├── Markers/                     gambar marker
│   ├── Models/Bomb/                 model bom
│   ├── Models/Environment/          model Bombsite B
│   ├── Materials/                   material aset
│   └── Textures/                    tekstur aset
├── Prefabs/AR/                      prefab tampilan AR
├── Scenes/
│   ├── Scenes/SampleScene.unity     scene utama AR
│   └── Virtual/VirtualRoom.unity    scene simulasi 3D
└── Scripts/Runtime/
    ├── AR/                           deteksi virtual button
    ├── Bomb/                         aturan permainan bom
    └── Transition/                   perpindahan scene
```

## Membuka Proyek

1. Instal Unity Editor `6000.6.0f1` melalui Unity Hub.
2. Tambahkan Android Build Support, Android SDK & NDK Tools, dan OpenJDK.
3. Buka folder proyek `bombAR` melalui Unity Hub.
4. Tunggu impor aset dan package selesai.
5. Pastikan Console tidak menampilkan error kompilasi.
6. Buka `Assets/Scenes/Scenes/SampleScene.unity` untuk memulai bagian AR.

## Mengatur Scene List

Buka **File → Build Profiles → Scene List**, lalu aktifkan scene berikut dengan urutan:

| Index | Scene |
|:---:|---|
| 0 | `Assets/Scenes/Scenes/SampleScene.unity` |
| 1 | `Assets/Scenes/Virtual/VirtualRoom.unity` |

Urutan ini diperlukan agar virtual button Mulai dapat membuka ruang virtual.

## Menjalankan Bagian AR

1. Buka `SampleScene`.
2. Pastikan `AR Session`, `XR Origin`, `BombManager`, dan `VirtualButtons` aktif.
3. Hubungkan ponsel Android dan aktifkan USB debugging.
4. Aktifkan profil Android melalui **File → Build Profiles**.
5. Jalankan **Build and Run**.
6. Izinkan akses kamera pada ponsel.
7. Arahkan kamera ke marker `Assets/Art/Markers/Bomb.png`.
8. Tunggu kalibrasi selesai sebelum memasukkan tangan ke area tombol di depan kamera.

Pengujian marker dan kamera dilakukan pada perangkat Android karena Game View Unity Editor tidak menyediakan input kamera AR yang sama dengan perangkat.

## Menggunakan Tombol AR

1. Tampilkan marker `Assets/Art/Markers/bomb_blueprint.jpeg` pada layar lain atau cetak dengan ukuran yang sesuai.
2. Letakkan marker pada permukaan stabil dan arahkan kamera hingga bom serta tombol melayang muncul.
3. Jangan menutupi marker selama proses kalibrasi awal.
4. Setelah kalibrasi selesai, masukkan tangan ke frame kamera dan tutupi bagian tengah satu tombol melayang.
5. Tahan tangan dengan stabil sejenak sampai warna terdeteksi.
6. Hindari menutupi dua tombol sekaligus dan hindari perubahan cahaya mendadak.
7. Tekan tombol **VR** di kanan atas layar untuk membuka scene `VirtualRoom`.

Tombol warna tidak dipilih dengan menyentuh layar HP. Sistem membaca perubahan gambar kamera pada area tombol yang tertutup tangan.

## Menjalankan Ruang Virtual

1. Pastikan `VirtualRoom` berada pada Scene List index 1.
2. Buka scene tersebut secara langsung untuk menata lingkungan tanpa mengulang scan marker.
3. Pastikan Bombsite B menampilkan seluruh material dan tekstur.
4. Atur Player dan Main Camera pada titik awal yang aman di dalam map.
5. Buat dan uji fitur tahap 5–11 secara berurutan.

## Build Android

1. Buka **File → Build Profiles**.
2. Pilih Android dan aktifkan profilnya.
3. Pastikan kedua scene masuk Scene List.
4. Periksa package name dan orientasi layar pada **Player Settings**.
5. Gunakan **Build** untuk menghasilkan APK atau **Build and Run** untuk memasangnya ke ponsel.
6. Simpan hasil build di luar folder `Assets`.

## Pemeriksaan Sebelum Build

- Console tidak memiliki error merah.
- `SampleScene` berada pada index 0 dan `VirtualRoom` pada index 1.
- Marker tercantum dalam Reference Image Library.
- Prefab bom terhubung ke AR Tracked Image Manager.
- Referensi `VirtualButtonDetector` tidak kosong.
- Bombsite B menampilkan material dan tekstur dengan benar.
- Permission kamera Android aktif.
- Aplikasi diuji menggunakan marker cetak atau marker pada layar lain.

## Sumber Aset

Lingkungan Bombsite B menggunakan model **de_dust2 CS map** dari Sketchfab:

- [de_dust2 CS map — Sketchfab](https://sketchfab.com/3d-models/de-dust2-cs-map-056008d59eb849a29c0ab6884c0c3d87)

Model dipangkas sehingga proyek hanya menggunakan bagian Bombsite B. Ketentuan lisensi dan atribusi mengikuti informasi pada halaman aset Sketchfab tersebut.

