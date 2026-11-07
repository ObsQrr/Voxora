# Voxora

Windows ses efekti pedalı (soundboard) uygulaması. Ses efektlerini mikrofon yönlendirmesi üzerinden Discord, Valorant ve benzeri uygulamalara aktarmak için tasarlanmıştır.

## Sürüm

**v2.2.0** — .NET 8 / WPF / NAudio

| Öğe | Değer |
|-----|-------|
| Uygulama | `Voxora.exe` |
| Kurulum paketi | `Voxora-2.2.0-setup.exe` |
| Taşınabilir | `Voxora-v2.2.0-win-x64.zip` |
| Arayüz dilleri | Türkçe (varsayılan), İngilizce |
| Testler | 120/120 geçti (.NET 8, Release) |
| Durum | v2.2.0 GitHub sürümü |

> Sürüm paketlerinin SHA-256 değerleri GitHub Release sayfasında yer alır.

### v2.2.0 Yenilikleri

- **Radyal menüde tekerlekle sayfalama:** Görünür tüm sesler 8'lik sayfalara bölünür;
  fare tekerleğiyle diğer efektlere geçilir (`TEKERLEK · 1/2` göstergesi).
- **Yumuşak geçiş:** 190 ms döndürme + çapraz sönümleme (ease-out).
- Menü açıkken tekerlek global yakalanır, oyuna geçmez; kapanınca bırakılır.
- Not: 2.2 planındaki Canlı Sahneler, Efekt Stüdyosu, Makrolar, Mini Mod, AI ve
  Topluluk paketleri bu sürümde **YAKINDA** (yalnızca tercih anahtarı).

### v2.1.1 Yenilikleri

- **Radyal menüde sayfalama:** Görünür tüm sesler 8'lik sayfalara bölünür; fare
  tekerleğiyle diğer efektlere geçilir. Merkezde `TEKERLEK · 1/2` göstergesi.
- **Yumuşak sayfa geçişi:** 190 ms döndürme + çapraz sönümleme (ease-out).
- Menü açıkken tekerlek global yakalanır, oyuna geçmez; kapanınca bırakılır.

### v2.1.0 Yenilikleri

- **F1 radyal menüsü:** Oyunun üstünde, imlecin olduğu yerde açılan daire; dilime
  tıklayınca ses çalar, pencere odak çalmaz. Tuş Ayarlar → Davranış'tan değiştirilir.
- **Cihaz adı düzeltmesi:** `IPolicyConfig` doğru vtable ile çağrılıyor; UAC akışı,
  kayıt defteri yedeği ve gerçek ad doğrulaması eklendi. `CABLE Output` artık
  Discord'da **Voxora Out Audio** olarak değiştirilebiliyor.
- Radyal menü için özellik anahtarı ve tanılama günlüğü (`%TEMP%\voxora-rename.log`).

### v2.0.0 Yenilikleri

- **Konsol arayüzü:** ana pencere, kartlar, miks masası ve Ayarlar baştan yazıldı;
  koyu/açık tema, yeni tipografi ve animasyonlar (giriş, hover, çalan kart, sayfa geçişi).
- **Kurulum sonrası sihirbaz:** dil, tema, ses yönlendirmesi, gerçek test tonu ve
  Windows cihaz adları; TR/EN canlı önizleme.
- **Windows cihaz adları:** `CABLE Input/Output` isteğe bağlı olarak (UAC ile)
  **Voxora In Audio / Voxora Out Audio** yapılır; Discord da bu adı görür.
- **Ses değiştirici:** gerçek DSP ile pitch, robot, yankı ve telefon efektleri.
- **Özellik anahtarları:** tamamlanmamış modüller YAKINDA, akıllı arama BETA rozetiyle.
- **Düzeltme:** aygıt olayları birleştirildi; "ses aygıtları değişti" bildirim fırtınası
  kaldırıldı (bildirim artık gösterilmiyor, liste sessizce tazeleniyor).
- `Ctrl+K` arama, temaya boyanan başlık çubuğu, MDL2 mute ikonları, kablo durumu noktası.

### v1.4.0 Yenilikleri

- **Otomatik güncelleme kontrolü:** Açılışta günde en fazla bir kez GitHub Releases kontrol edilir.
- Yeni sürüm bulununca ana pencerenin üstünde **uyarı şeridi** ve tepsi bildirimi görünür.
- Ayarlar → Güncellemeler bölümünde mevcut sürüm, **“Güncellemeyi Kontrol Et”** düğmesi ve indirme bağlantısı yer alır.
- Otomatik kontrol Ayarlar'dan kapatılabilir; manuel kontrol her zaman kullanılabilir.
- Güncelleme yalnızca bildirilir; sessiz indirme/kurulum yapılmaz.

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

Gerçek cihaz kimlikleri (MMDevice ID) dahili olarak korunur. **v2.0.0** ile, Ayarlar →
Ses → **Cihazları yeniden adlandır** düğmesi Windows'taki aygıt adını da değiştirir
(yönetici izni ister). Böylece Discord'un mikrofon listesinde `CABLE Output` yerine
**Voxora Out Audio** görünür. İşlem geri alınabilir: Windows **Ayarlar → Sistem → Ses**
bölümünden cihazı yeniden adlandırın.

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

`Voxora-2.2.0-setup.exe` dosyasını çalıştırın. Kurulum:

- Kullanıcı bazında (`%LOCALAPPDATA%\Programs\Voxora`) — yönetici hakları **gerekmez**
- Başlat Menüsü kısayolu oluşturur
- Masaüstü kısayolu isteğe bağlıdır
- "Windows açılışında başlat" seçeneği işaretlenirse `HKCU\...\Run` anahtarına yazılır
- Kaldırma işlemi **`%APPDATA%\KayoneZSoundpad`** klasöründeki kullanıcı verilerini (ayarlar, ses kitaplığı) **silmez**

Taşınabilir sürüm: `Voxora-v2.2.0-win-x64.zip` arşivini bir klasöre açıp `Voxora.exe`
çalıştırın; .NET kurulumu gerekmez.

### 3. İlk çalıştırma ve ayarlar

1. Voxora'yı başlatın; **kurulum sihirbazı** açılır (dil → tema → ses → cihaz adları).
2. Sihirbazda **Oto Ayarla** ile yönlendirme otomatik kurulur:
   - **Çıkış (Discord/Oyun):** `Voxora In Audio`
   - **Mikrofon:** gerçek mikrofonunuz (`Voxora Out Audio` **değil**)
   - **Lokal Monitör:** kulaklık/hoparlörünüz
3. Discord (veya oyun) ayarlarında Giriş Aygıtı olarak **`Voxora Out Audio`** seçin.
4. Discord'da Gürültü Bastırma, Yankı İptali ve Otomatik Kazancı **KAPALI** tutun (Krisp açıkken efektler bozulur).
5. Cihaz adları hâlâ `CABLE` görünüyorsa Ayarlar → Ses → **Cihazları yeniden adlandır**
   (UAC onayı) ve Discord'u yeniden başlatın.

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

Çıktı: `dist\Voxora-2.2.0-setup.exe`

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
│       │   ├── SettingsService.cs  # Ayar yükleme/kaydetme ve doğrulama
│       │   └── UpdateService.cs    # GitHub Releases güncelleme kontrolü
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
│       ├── UpdateServiceTests.cs
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
| Cihaz adı değiştirme | Yalnızca kullanıcı onayı + UAC ile; `--rename-audio-endpoints` yardımcısı sadece `PKEY_Device_FriendlyName` yazar, başka ayar değiştirmez |
| Güncelleme kontrolü | Yalnızca GitHub Releases API'sine okuma isteği; token yok, kişisel veri gönderilmez |

---

## Lisans

MIT Lisansı — Telif sahibi: ObsQrr (2026). [LICENSE](LICENSE) dosyasına bakın.

Kısa özet: Yazıyı özgürce kullanabilir, kopyalayabilir, değiştirebilir, dağıtabilir ve satabilirsiniz. Üst bildirim lisans metniyle birlikte tüm kopyalarda bulunmalı. Yazılım "olduğu gibi" sağlanır, hiçbir garanti verilmez.

---

## Bilinen Sınırlamalar

- VB-CABLE sürücüsü kurulu olmadan ses yönlendirme çalışmaz.
- Sadece Windows 10/11 (WPF/NAudio bağımlılığı).
- Canlı Sahneler, Efekt Stüdyosu, Makrolar, Mini Mod, AI Ses Asistanı ve Topluluk Ses
  Paketleri v2.0.0'da henüz uygulanmadı; arayüzde **YAKINDA** olarak işaretlidir.
- Windows cihaz adı değiştirme UAC ister ve cihaz adını tüm uygulamalarda değiştirir.
- Radyal menü pencereli/kenarlıksız tam ekran oyunlarda görünür; özel tam ekranda
  oyun üstteki pencereyi gizleyebilir. Tekerlekle 8'lik sayfalar arasında geçilir.
- Global kısayollar sistem genelinde çalışır; bazı oyunlar kendi kısayollarını yakalayabilir.
- Mikrofon izni Windows Gizlilik ayarlarından kontrol edilir; Voxora bunu otomatik olarak istemez.

---

## Destek

Hata bildirimi veya özellik isteği için GitHub Issues bölümünü kullanın.

---