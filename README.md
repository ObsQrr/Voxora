# Voxora

Windows ses efekti pedalı (soundboard) uygulaması. Ses efektlerini mikrofon yönlendirmesi üzerinden Discord, Valorant ve benzeri uygulamalara aktarmak için tasarlanmıştır.

## Sürüm

**v1.1.0** — .NET 8 / WPF / NAudio

| Öğe | Değer |
|-----|-------|
| Uygulama | `Voxora.exe` |
| Kurulum paketi | `Voxora-Setup.exe` |
| Kurulum SHA-256 | `41EE72D82A1426C5584E657D2F21DD3E98292973B3D6F21D3F5BD817C41BB1A2` |
| EXE SHA-256 | `96D657AE8637F2A77ECD5CA1983259B9378DE7A237785E1D536C5E117FFE2BE4` |
| Testler | 43/43 geçti (.NET 8, Release) |

---

## Ne İşe Yarar?

Voxora, bilgisayarınızda kayıtlı ses dosyalarını ("kartlar") tek tusta oynatır ve sesini sanal bir mikrofon üzerinden karşı tarafa iletir. Örnek kullanım senaryoları:

- Discord'da ses efekti çalmak
- Oyun içinde ses klibi kullanmak
- Stream sırasında ses jingli eklemek

Ses yönlendirme için **VB-CABLE** (VB-Audio) sanal ses sürücüsü kullanılır. Voxora,VB-CABLE'nin iki uç bağlantısını algılar ve kullanıcıya şu görünen adlarla tanıtır:

| Voxora'da görünen ad | Windows'taki gerçek cihaz | İşlevi |
|----------------------|---------------------------|--------|
| **Voxora In Audio** | `CABLE Input (VB-Audio Virtual Cable)` | Voxora'nın efekt sesinin gittiği sanal oynatma (render) aygıtı. Discord/oyun burayı **seçmez**; ses buradan `CABLE Output` üzerinden akar. |
| **Voxora Out Audio** | `CABLE Output (VB-Audio Virtual Cable)` | Sanal kablodan çıkan sesi mikrofon gibi okuyan sanal kayıt (capture) aygıtı. Discord/oyun burayı **Giriş Aygıtı** olarak seçer. |

Gerçek cihaz kimlikleri (MMDevice ID) dahili olarak korunur; yalnızca ekranda görünen etiket Voxora markalıdır.

---

## Kurulum

### 1. VB-CABLE sürücüsü (gerekli)

Voxora'nın ses yönlendirmesi çalışması için VB-CABLE sürücüsü yüklü olmalıdır:

1. [https://vb-audio.com/Cable/](https://vb-audio.com/Cable/) adresine gidin.
2. "Download" → "VB-CABLE Driver" indirin.
3. Arşivi çıkartın, `VBCABLE_Driver64.exe` dosyasını **yönetici olarak** çalıştırın.
4. Bilgisayarı yeniden başlatın.

Kurulum sonrası Windows Ses Ayarlarında iki yeni sanal cihaz görünür: `CABLE Input` ve `CABLE Output`.

### 2. Voxora'yı kurun

`dist\Voxora-Setup.exe` dosyasını çalıştırın. Kurulum:

- Kullanıcı bazında (`%LOCALAPPDATA%\Programs\Voxora`) — yönetici hakları **gerekmez**
- Başlat Menüsü kısayolu oluşturur
- Masaüstü kısayolu isteğe bağlıdır
- "Windows açılışında başlat" seçeneği işaretlenirse `HKCU\...\Run` anahtarına yazılır
- Kaldırma işlemi **`%APPDATA%\KayoneZSoundpad`** klasöründeki kullanıcı verilerini (ayarlar, ses kitaplığı) **silmez**

### 3. İlk çalıştırma ve ayarlar

1. Voxora'yı başlatın.
2. **Ayarlar** penceresini açın → **Ses Yönlendirme** bölümünde:
   - **Çıkış (Discord/Oyun):** `Voxora In Audio` seçin
   - **Mikrofon:** Gerçek mikrofonunuzu seçin (Voxora Out Audio **değil**)
   - **Lokal Monitör:** Kulaklık/hoparlörünüzü seçin
3. **Oto Ayarla** butonuna basın — Voxora sanal aygıtları otomatik yapılandırılır.
4. Discord (veya oyun) ayarlarında Giriş Aygıt'ı olarak **`Voxora Out Audio`** seçin.
5. Discord'da Gürültü Bastırma, Yankı İptali ve Otomatik Kazancı **KAPALI** tutun (Krisp açıkken efektler bozulur).

---

## Yapılandırma Dosyaları

Kullanıcı verileri şu konumda saklanır:

```
%APPDATA%\KayoneZSoundpad\
├── settings.json   # Uygulama ayarları (tema, cihaz ID'leri, ses seviyeleri, kısayollar)
├── library.json    # Ses kitaplığı (kartlar, kategoriler, sıralama)
└── Sounds/         # İçe aktarılan ses dosyalarının kopyaları
```

Ayar dosyası BOM'suz UTF-8 olarak yazılır. El ile düzenlemek yerine Ayarlar penceresini kullanın.

---

## Derleme (Kaynak Koddan)

### Gereksinimler

- Windows 10 / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.427 veya üzeri)
- WPF masaüstü geliştirme bileşenleri (Visual Studio 2022 yükleme ile gelir)

### Komutlar

```powershell
# Depoyu klonlayın (veya dizine gidin)
cd KayoneZSoundpad

# Paketleri geri yükleyin
dotnet restore

# Release derlemesi
dotnet build -c Release

# Testleri çalıştırın
dotnet test -c Release

# Self-contained win-x64 yayınla
dotnet publish src/KayoneZSoundpad\KayoneZSoundpad.csproj -c Release -r win-x64 --self-contained true -o dist\Voxora
```

### Kurulum paketi derlemesi

[Inno Setup 6](https://jrsoftware.org/is6.php) kurulu olduğunda:

```powershell
ISCC.exe installer\Voxora.iss
```

Çıktı: `dist\Voxora-Setup.exe`

---

## Proje Yapısı

```
KayoneZSoundpad/
├── README.md
├── .gitignore
├── KayoneZSoundpad.sln
├── installer/
│   └── Voxora.iss          # Inno Setup 6 kurulum betiği
├── src/
│   └── KayoneZSoundpad/
│       ├── App.xaml(.cs)           # Uygulama giriş noktası, tek-örnek mutex
│       ├── app.manifest            # Uygulama bildirimi (asInvoker, DPI bilinçli)
│       ├── Assets/
│       │   ├── app.ico             # Uygulama simgesi
│       │   └── app.png             # Uygulama ikonu (kaynak)
│       ├── Audio/
│       │   ├── AudioEngine.cs      # NAudio/WASAPI karıştırma motoru
│       │   ├── DeviceService.cs    # Windows ses aygıtı listesi + VB-CABLE tespiti
│       │   └── SoundDecoder.cs     # Ses dosyası kod çözme (boyut sınırlı)
│       ├── Hotkeys/
│       │   └── GlobalHotkeyService.cs  # Sistem genelinde kısayol kancası
│       ├── Models/
│       │   ├── AppSettings.cs      # Kullanıcı ayarları modeli
│       │   ├── HotkeyGesture.cs    # Kısayol tuşu modeli
│       │   └── SoundItem.cs        # Ses kartı modeli
│       ├── Services/
│       │   ├── AppPaths.cs         # %APPDATA%\KayoneZSoundpad yolu
│       │   ├── AutoStartService.cs # Windows başlangıç kayıt defteri
│       │   ├── ImportExportService.cs  # ZIP içe/dışa aktarma (dizin traversal korumalı)
│       │   ├── JsonStore.cs        # Güvenli JSON serileştirme (derinlik sınırlı)
│       │   ├── LibraryService.cs   # Ses kitaplığı yönetimi (yol beyaz listesi)
│       │   └── SettingsService.cs  # Ayar yükleme/kaydetme
│       ├── Themes/
│       │   ├── Theme.Dark.xaml     # Koyu tema kaynakları
│       │   └── Theme.Light.xaml    # Açık tema kaynakları
│       ├── ViewModels/
│       │   └── MainViewModel.cs    # Ana görünüm modeli (cable ayarı, cihaz seçimi)
│       └── Views/
│           ├── MainWindow.xaml(.cs)        # Ana pencere (ses kartı ızgarası, misir barı)
│           ├── SettingsWindow.xaml(.cs)    # Ayarlar penceresi
│           ├── CableGuideWindow.xaml(.cs)  # VB-CABLE kurulum rehberi
│           ├── EditSoundDialog.xaml(.cs)   # Ses düzenleme iletişim kutusu
│           └── HotkeyBox.xaml.cs           # Kısayol girişi kontrolü
├── tests/
│   └── KayoneZSoundpad.Tests/
│       ├── AudioEngineIntegrationTests.cs
│       ├── AudioTests.cs
│       ├── FormatDecodeTests.cs
│       ├── HotkeyGestureTests.cs
│       ├── ImportExportTests.cs
│       ├── JsonStoreTests.cs
│       ├── LibraryServiceTests.cs
│       ├── PlaybackManagerTests.cs
│       └── TestConfig.cs
└── dist/                       # Derleme çıktısı (git'e dahil edilmez)
```

---

## Güvenlik

Aşağıdaki güvenlik korumaları uygulanmıştır:

| Alan | Koruma |
|------|--------|
| ZIP içe aktarma | Dizin traversal (zip-slip) koruması; her girişin hedef yolunun geçici dizin içinde olduğunun kontrolü |
| JSON serileştirme | `MaxDepth=64`, `PropertyNameCaseInsensitive=false`, trailing comma yorum dışı, yorum dışı, varsayılan encoder |
| Ses dosyası çözme | `maxBytes` sınırı; sınır aşılırsa erken iptal (DoS koruması) |
| Ses kitaplığı ekleme | Kaynak yolu beyaz listesi kontrolü |
| Ses motoru | Karıştırma iş parçacığında kilit kapsamı genişletildi |
| Global kısayollar | Modül tanıtıcısı doğrulaması, `CallNextHookEx` sonucu iade edilir |
| Tek örnek | `"Voxora_SingleInstance"` mutex ile çift çalıştırma engellenir |

---

## Lisans

MIT Lisansı — Telif sahibi: ObsQrr (2026). [LICENSE](LICENSE) dosyasına bakın.

Kısa özet: Yazıyı özgürce kullanabilir, kopyalayabilir, değiştirebilir, dağıtabilir ve satabilirsiniz. Üst bildirim lisans metniyle birlikte tüm kopyalarda bulunmalı. Yazılım "olduğu gibi" sağlanır, hiçbir garanti verilmez.

---

## Bilinen Sınırlamalar

- VB-CABLE sürücüsü kurulu olmadan ses yönlendirme çalışmaz.
- Sadece Windows 10/11 (WPF/NAudio bağımlılığı).
- Global kısayollar sistem genelinde çalışır; bazı oyunlar kendi kısayollarını yakalayabilir.
- Mikrofon izni Windows Gizlilik ayarlarından kontrol edilir; Voxora bunu otomatik olarak istemez.

---

## Destek

Hata bildirimi veya özellik isteği için GitHub Issues bölümünü kullanın.

---