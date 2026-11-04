# Voxora 2.0.0

Konsol görünümlü yeni arayüz, kurulum sonrası sihirbaz ve Windows cihaz adlarının
gerçekten "Voxora" olarak değiştirilmesi bu sürümün ana başlıkları.

## Öne çıkanlar

### Yeni arayüz
- **Kontrol konsolu** metaforuyla baştan yazılan ana pencere: komuta çubuğu,
  kütüphane rayı, kanal şeridi görünümlü ses kartları, alt miks masası
  (MIC / FX / ÇIKIŞ / MASTER).
- **Koyu "gece vardiyası"** ve **açık "gündüz masası"** temaları; yeni profillerde
  varsayılan koyu. Bahnschrift + Segoe UI Variable + Cascadia Mono tipografisi.
- Animasyonlar: pencere girişi, kart hover'ı, çalan kartta LED çubukları ve turuncu
  kenar, ayar sayfalarında kayma/solma, switch animasyonu. Hareket azaltma açıksa
  animasyonlar atlanır.
- `Ctrl+K` aramaya odaklanır; Windows başlık çubuğu temaya boyanır.
- Yeniden düzenlenen Ayarlar: Genel, Ses, Davranış, Özellikler, Güncellemeler, Hakkında.

### Kurulum sonrası sihirbaz
İlk açılışta 5 adımlı, animasyonlu kurulum:
1. Dil (TR/EN, anında)
2. Tema (canlı önizleme)
3. Ses: çıkış, mikrofon, kulaklıkta dinleme, **gerçek test tonu**, Oto Ayarla
4. Windows cihaz adları (Discord mikrofon listesi önizlemesi)
5. Özet ve kısayol ipuçları

Sihirbaz kapatılsa bile bir daha otomatik açılmaz; **Ayarlar → Kurulum sihirbazını aç**
ile her zaman yeniden çalıştırılabilir.

### Discord'da "Voxora Out Audio"
- `CABLE Input` → **Voxora In Audio**
- `CABLE Output` → **Voxora Out Audio**

Ad değişikliği yalnızca Voxora içinde değil, **Windows aygıt adı** olarak yapılır;
Discord ve oyunlar da bu adı görür. İşlem yönetici izni (UAC) ister ve cihazı tüm
uygulamalar için yeniden adlandırır. Discord'u yeniden başlatmanız gerekebilir.
Geri almak için Windows **Ayarlar → Sistem → Ses** üzerinden cihazı yeniden adlandırabilirsiniz.

### Diğer
- **Ses değiştirici:** pitch, robot, yankı ve telefon efektleri; hazır ayarlar.
- **Özellik anahtarları:** tamamlanmamış 2.0 modülleri **YAKINDA**, akıllı arama **BETA**
  rozetiyle işaretli. Bu modüller şu an yalnızca tercih olarak saklanır; çalışıyor gibi
  gösterilmez.
- **Düzeltme:** bazı ses sürücülerinin sık yayınladığı aygıt olayları artık 450 ms'de
  birleştiriliyor ve yalnızca görünür aygıt kümesi gerçekten değişince işleniyor.
  "Ses aygıtları değişti" tepsi bildirimi kaldırıldı; sağ altta bildirim fırtınası yok.
- Emoji yerine Segoe MDL2 mute ikonları; kablo durumu noktası; daha okunaklı arama alanı.

## Kurulum

1. **VB-CABLE** sürücüsünü kurun (gerekli): https://vb-audio.com/Cable/
2. `Voxora-2.0.0-setup.exe` ile kurun veya `Voxora-v2.0.0-win-x64.zip` arşivini açıp
   `Voxora.exe` çalıştırın.
3. İlk açılışta sihirbazı izleyin. Discord'da giriş aygıtı olarak **Voxora Out Audio**
   seçin; Gürültü Bastırma / Yankı İptali / Otomatik Kazancı kapatın.

Kurulum kullanıcı bazındadır (`%LOCALAPPDATA%\Programs\Voxora`); kaldırma işlemi
`%APPDATA%\KayoneZSoundpad` altındaki ayarları, kütüphaneyi ve sesleri **silmez**.

## Bilinen sınırlar

- Canlı Sahneler, Efekt Stüdyosu, Makrolar, Mini Mod, AI Ses Asistanı ve Topluluk
  Ses Paketleri bu sürümde **henüz uygulanmadı** (arayüzde YAKINDA).
- UAC'li cihaz yeniden adlandırma akışı geliştirici tarafından uçtan uca onaylanarak
  test edilemedi; yükseltilmemiş deneme beklendiği gibi erişim reddi veriyor.
- EXE ve kurulum paketi kod imzalı değildir.
- Fiziksel mikrofon/kulaklıkla canlı dinleme testi yapılmadı.

## Teknik

- .NET 8 / WPF / NAudio 2.2.1, self-contained win-x64.
- Testler: **115/115** geçti (Release).
- Arayüz dilleri: Türkçe (varsayılan), İngilizce.
