# Voxora 2.1.0

İki iş: **Discord cihaz adı hatasının gerçek düzeltmesi** ve **F1 radyal menüsü**.

## Düzeltme: "Cihazları Voxora olarak adlandır" artık çalışıyor

Önceki sürümde düğme hata veriyordu; ad hiç değişmiyordu. Kök neden, Windows'un
belgelenmemiş `IPolicyConfig` arayüzünün **yanlış vtable imzasıyla** çağrılmasıydı:
çağrı `S_OK` dönüyor ama yanlış slota gittiği için hiçbir şey yazmıyordu.

- Doğru imza kullanılıyor: `ResetDeviceFormat` dahil Windows 7+ vtable ve
  `Get/SetPropertyValue(deviceId, fxStore, key, value)`.
- Önce normal kullanıcı denenir; Windows izin vermezse onay + UAC ile yükseltilmiş
  yardımcı çalışır (`--rename-audio-endpoints`).
- Yükseltilmiş tarafta policy reddederse kayıt defteri yedeği (`PKEY_Device_FriendlyName`)
  ve ses servisi yeniden başlatma devreye girer.
- Sonuç **gerçek aygıt adı okunarak doğrulanır**; "başarılı" denmesi için adın
  gerçekten değişmiş olması gerekir.
- Ayrıntılı tanılama: `%TEMP%\voxora-rename.log` (HRESULT'lar, denenen yollar).

Uygulama: **Ayarlar → Ses → Cihazları yeniden adlandır** (veya kurulum sihirbazı
4. adım) → UAC onayı → Discord'u yeniden başlat.

## Yeni: F1 radyal menüsü

- **Varsayılan F1**; Ayarlar → Davranış → **Radial Menü → Açma tuşu** ile değiştirilir.
- Kısayola basınca **imlecin olduğu yerde** daire açılır; dilime tıklayarak ses çalınır.
- Pencere **odak çalmaz** (`WS_EX_NOACTIVATE` + `Topmost`): oyun arkada çalışmaya
  devam eder, alt+tab olmaz.
- Ekrandaki görünür seslerden ilk **8** tanesi dilimlenir (arama/kategori filtresi
  dikkate alınır); adlar iki satıra sarılır.
- Sağ tık, daire dışına tık veya 12 saniye hareketsizlik menüyü kapatır; F1 tekrar
  basınca da kapanır.
- Özellik anahtarı: Ayarlar → Özellikler → **Radial Menü (F1)** (varsayılan açık).

## Diğer

- Sürüm 2.1.0, testler **118/118**.
- Önceki 2.0.0 sürümündeki tüm yenilikler (konsol arayüzü, kurulum sihirbazı,
  ses değiştirici, aygıt bildirimi düzeltmesi) korunuyor.

## Bilinen sınırlar

- Radyal menü, **pencereli veya kenarlıksız tam ekran** oyunlarda görünür; özel
  tam ekran (exclusive fullscreen) oyunlar üstteki pencereyi gizleyebilir.
- Menüde tek seferde en fazla 8 ses gösterilir.
- Cihaz adı değiştirme yönetici izni ister; UAC onayı verilmezse ad değişmez.
- EXE ve kurulum paketi kod imzalı değildir.
