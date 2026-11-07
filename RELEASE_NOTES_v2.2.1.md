# Voxora 2.2.1

Hata düzeltme sürümü. Yeni özellik yok; kararlılık ve doğruluk düzeltmeleri var.

## Düzeltmeler

- **Kısayol çakışması:** Aynı tuş hem radyal menüye/stop-all'a hem bir sese atanmışsa,
  ses kısayolu artık sessizce diğerini ezmiyor; çakışma bildirilir ve ilk sahip korunur.
- **Ses aygıtı sızıntısı:** Çıkış/monitör açılamadığında `MMDevice` tutamacı
  serbest bırakılmıyordu; artık her durumda bırakılıyor.
- **Aygıt olayları:** Değişiklik imzası artık oluşturulduğu UI bağlamında
  hesaplanıyor; debounce/bildirimlerin kaçması engellendi.
- **Radyal menü:** Otomatik kapanma sayacı menü açılır açılmaz başlıyor;
  kapanırken gelen tekerlek olayı yok sayılıyor; geçiş animasyonu penceresi
  kapanınca duruyor.
- **Cihaz adı değişimi:** Başarılı yeniden adlandırmadan sonra cihaz listeleri
  anında tazeleniyor.
- **Çıkış ayarı korunuyor:** VB-Cable kuruluyken, kullanıcının seçtiği geçerli
  çıkış her açılışta otomatik olarak kabloya çevrilmiyor; yalnızca kayıtlı cihaz
  yoksa otomatik kurulum yapılıyor.
- Radyal kısayolu artık temizlenebiliyor (boş bırakınca kapalı kalır).

## Doğrulama

- Build **0 uyarı / 0 hata**, testler **123/123**.
- XAML bağlama taraması (ana pencere, 6 ayar sayfası, sihirbaz, radyal menü)
  **0 uyarı**; çalışma zamanı istisnası yok.

## Bilinen sınırlar

- UAC'li cihaz adı akışı geliştirici tarafından canlı onayla test edilmedi.
- Radyal menü özel tam ekran (exclusive) oyunlarda görünmeyebilir.
- Cihaz fiziksel olarak çıkarılırsa elle yeni cihaz seçmek gerekir.
- EXE ve kurulum paketi kod imzalı değildir.
