# Voxora

Windows ses efekti pedalı (soundboard) uygulaması. Ses efektlerini mikrofon yönlendirmesi üzerinden Discord, Valorant ve benzeri uygulamalara aktarmak için tasarlanmıştır.

## Sürüm

**v1.3.0** — .NET 8 / WPF / NAudio

| Öğe | Değer |
|-----|-------|
| Uygulama | `Voxora.exe` |
| Kurulum paketi | `Voxora-Setup.exe` |
| Arayüz dilleri | Türkçe (varsayılan), İngilizce |
| Testler | 70/70 geçti (.NET 8, Release) |
| Durum | v1.3.0 GitHub sürümü |

> Sürüm paketlerinin SHA-256 değerleri GitHub Release sayfasında yer alır.

### v1.3.0 Yenilikleri

- **Sesler** düğmesi: kütüphanedeki ve `Sounds` klasöründeki tüm kişisel ses dosyalarını
  meta veri eklemeden düz bir ZIP olarak dışa aktarır.
- Kütüphane dosyası açılamazsa mevcut `library.json` artık ezilmez; kullanıcı uyarılır.
- Kategori sağ tık menüsü, seçili olmayan kategoriye sağ tıklanınca da doğru çalışır.
- ZIP içe aktarma boyut limitleri gerçek açılan baytlar üzerinden uygulanır.
- Bozuk yedeklerdeki `null` kategori/ses listeleri güvenli biçimde reddedilir.
- Yeni testler: kütüphane yükleme güvenliği, ses dosyası ZIP dışa aktarma.

### v1.2.0 Yenilikleri

- Tam **Türkçe + İngilizce** arayüz; dil Ayarlar'dan değiştirilir ve kaydedilir.
- VB-CABLE eşleştirmesi artık Windows'un gerçek aygıt adı üzerinden yapılır
  (`Voxora In Audio` / `Voxora Out Audio` görünen adları korunur).
- Sessize alınmış seslerin tetiklendiğinde bir an duyulması engellendi.
- Akış (streaming) çalan seslerde dosya tutamacı sızıntısı giderildi.
- ZIP içe aktarma: zip-slip, symlink, aşırı boyut/adet ve bozuk manifest korumaları.
- Tema seçimi tüm pencerelerde tutarlı hale getirildi.
- Ses seviyesi kaydırmaları 400 ms gecikmeli kaydedilir; gereksiz disk yazımı azaltıldı.

---

## Ne İşe Yarar?

Voxora, bilgisayarınızda kayıtlı ses dosyalarını ("kartlar") tek tuşta oynatır ve sesini sanal bir mikrofon üzerinden karşı tarafa iletir. Örnek kullanım senaryoları:

- Discord'da ses efekti çalmak
- Oyun içinde ses klibi kullanmak
- Stream sırasında ses jingli eklemek

Ses yönlendirme için **VB-CABLE** (VB-Audio) sanal ses sürücüsü kullanılır. Voxora, VB-CABLE'nin iki uç bağlantısını algılar ve kullanıcıya şu görünen adlarla tanıtır:

| Voxora'da görünen ad | Windows'taki gerçek cihaz | İşlevi |
|----------------------|---------------------------|--------|
| **Voxora In Audio** | `CABLE Input (VB-Audio Virtual Cable)` | Voxora'nın efekt sesinin gittiği sanal oynatma (render) aygıtı. Discord/oyun burayı **seçmez**; ses buradan `CABLE Output` üzerinden akar. |
| **Voxora Out Audio** | `CABLE Output (VB-Audio Virtual Cable)` | Sanal kablodan çıkan sesi mikrofon gibi okuyan sanal kayıt (capture) aygıtı. Discord/oyun burayı **Giriş Aygıtı** olarak seçer. |

Gerçek cihaz kimlikleri (MMDevice ID) dahili olarak korunur; yalnızca ekranda görünen etiket Voxora markalıdır.

### Dil Desteği

Arayüz Türkçe (varsayılan) ve İngilizce olarak kullanılabilir:
**Ayarlar → Genel → Dil** bölümünden dil seçilip **Kaydet**'e basılır.
Seçim `settings.json` içindeki `language` alanına yazılır (`tr` / `en`);
bilinmeyen değerler Türkçe'ye döner. Dil değişikliği ana pencere, kartlar,
kategoriler, cihaz listeleri, tepsi menüsü ve tüm iletişim kutularına uygulanır.

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
├── settings.json   # Uygulama ayarları (dil, tema, cihaz ID'leri, ses seviyeleri)
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
dotnet publish src\KayoneZSoundpad\KayoneZSoundpad.csproj -c Release -r win-x64 --self-contained true -o dist\Voxora
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
│       │   ├── JsonStore.cs        # Güvenli JSON serileştirme (derinlik/boyut sınırlı)
│       │   ├── LibraryService.cs   # Ses kitaplığı yönetimi + gecikmeli kayıt
│       │   ├── LocalizationService.cs  # Türkçe/İngilizce metinler ve dil değiştirme
│       │   ├── PlaybackManager.cs  # Çalma davranışları, önbellek/akış seçimi
│       │   └── SettingsService.cs  # Ayar yükleme/kaydetme ve doğrulama
│       ├── Themes/
│       │   ├── Theme.Dark.xaml     # Koyu tema kaynakları
│       │   └── Theme.Light.xaml    # Açık tema kaynakları
│       ├── ViewModels/
│       │   ├── MainViewModel.cs    # Ana görünüm modeli (kategori, cihaz, kısayol)
│       │   ├── RelayCommand.cs     # Basit ICommand uygulaması
│       │   └── SoundCardViewModel.cs  # Tek ses kartı görünüm modeli
│       └── Views/
│           ├── MainWindow.xaml(.cs)        # Ana pencere (ses kartı ızgarası, mikser barı)
│           ├── SettingsWindow.xaml(.cs)    # Ayarlar penceresi (dil, tema, cihazlar)
│           ├── CableGuideWindow.xaml(.cs)  # VB-CABLE kurulum rehberi
│           ├── EditSoundDialog.xaml(.cs)   # Ses düzenleme iletişim kutusu
│           ├── InputDialog.xaml(.cs)       # Metin girişi (kategori adı vb.)
│           ├── HotkeyBox.xaml(.cs)         # Kısayol girişi kontrolü
│           └── Converters.cs               # XAML değer dönüştürücüleri
├── tests/
│   └── KayoneZSoundpad.Tests/
│       ├── AudioEngineIntegrationTests.cs
│       ├── AudioTests.cs
│       ├── DeviceNamingTests.cs
│       ├── FormatDecodeTests.cs
│       ├── HotkeyGestureTests.cs
│       ├── ImportExportTests.cs
│       ├── JsonStoreTests.cs
│       ├── LibraryServiceTests.cs
│       ├── LocalizationTests.cs
│       ├── PlaybackManagerTests.cs
│       ├── SettingsLanguageTests.cs
│       ├── ThemeResourceTests.cs
│       └── TestConfig.cs
└── dist/                       # Derleme çıktısı (git'e dahil edilmez)
```

---

## Güvenlik

Aşağıdaki güvenlik korumaları uygulanmıştır:

| Alan | Koruma |
|------|--------|
| ZIP içe aktarma | Dizin traversal (zip-slip) koruması; her girişin hedef yolu geçici dizin içinde doğrulanır, dışarı taşan girişler atlanır |
| ZIP kaynakları | Symlink/reparse-point girişleri atlanır; en fazla 5.000 giriş, toplam 2 GB, tek giriş 512 MB — limitler gerçek açılan baytlar üzerinden uygulanır |
| ZIP içe aktarma kopyası | Geçici çıkarma dizini silindiği için dosyalar her durumda kalıcı kitaplığa kopyalanır |
| Kütüphane yükleme | `library.json` açılamazsa mevcut dosya ezilmez (`LoadSucceeded=false` → `Save` no-op) |
| JSON serileştirme | `MaxDepth=64`, belge başına 32 MB üst sınır, büyük/küçük harf duyarlı üyeler, trailing comma/yorum kapalı |
| Ses dosyası çözme | `maxBytes` sınırı; sınır aşılırsa erken iptal (DoS koruması) |
| Ses kitaplığı ekleme | Kaynak yolu beyaz listesi ve desteklenen format kontrolü |
| Ses motoru | Akış kaynakları voice bitince/iptal edilince tek sefer serbest bırakılır; kilit kapsamı dar tutulur |
| Global kısayollar | Modül tanıtıcısı doğrulaması, `CallNextHookEx` sonucu iade edilir |
| Tek örnek | `"KayoneZSoundpad_SingleInstance"` mutex ile çift çalıştırma engellenir |

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