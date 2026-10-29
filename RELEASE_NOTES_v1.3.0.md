# Voxora v1.3.0 Release Notes

**Date:** 2026-10-29  
**Branch:** `voxora`

---

## 🎵 Yeni: Kendi Efektlerini ZIP Olarak Dışa Aktar

Ana araç çubuğuna **Sesler / Sounds** düğmesi eklendi. Bu düğme:

- Kütüphanedeki tüm sesleri ve `%APPDATA%\KayoneZSoundpad\Sounds` klasöründeki
  desteklenen tüm ses dosyalarını tek bir ZIP içinde toplar.
- `library.json`, `settings.json`, günlükler veya başka kullanıcı verisi **eklemez**.
- Dosya adlarını korur; aynı ada sahip dosyalarda otomatik `(1)`, `(2)` soneki kullanır.
- Boş kütüphanede bilgilendirici mesaj gösterir.

Mevcut **Dışa Aktar / Export** düğmesi ise tam kütüphane yedeği (sesler + kategoriler +
kısayollar + meta veri) üretmeye devam eder.

---

## 🐛 Düzeltmeler

| Alan | Düzeltme |
|------|----------|
| Kütüphane güvenliği | `library.json` açılamazsa (kilit, bozuk JSON, 32 MB üstü) mevcut dosya artık **ezilmiyor**; `LoadSucceeded=false` iken `Save` no-op ve kullanıcı uyarılıyor |
| Kategori menüsü | `DataTemplate` içindeki Grid'e taşınan sağ tık menüsü, seçili olmayan kategoriye sağ tıklandığında da doğru kategoriyi seçiyor |
| ZIP güvenliği | Boyut limitleri artık ZIP metadata'sındaki `Length` yerine **gerçek açılan baytlar** üzerinden uygulanıyor |
| Bozuk yedek | `categories: null` / `sounds: null` içeren manifestler NullReferenceException yerine yerelleştirilmiş `Import_InvalidBackup` hatası veriyor |
| Test dürüstlüğü | Ses entegrasyon testi, çıkış açma hatasını sessizce yutmak yerine başarısız oluyor; donanım yoksa mevcut `HasRenderDevice()` kontrolü devrede |

---

## ✅ Doğrulama

- **Build:** `dotnet build -c Release` — 0 warning, 0 error
- **Testler:** **70/70 geçti** (önceki 66)
- Yeni testler:
  - Bozuk kütüphanenin ezilmemesi
  - `null` kategori/ses listelerinin normalize edilmesi
  - Ses dosyalarının meta verisiz ZIP olarak dışa aktarılması
- **Gizlilik:** Kişisel ses dosyaları, `library.json` veya `settings.json` repo'ya/paketlere dahil edilmedi.

---

## 📦 Dağıtım

| Paket | SHA256 | Boyut |
|-------|--------|-------|
| `Voxora-Setup.exe` (Inno Setup 6 installer) | `98554FE363369B79514068CA0386AD7E90C48447EFF38AA33CFCAE9BA062298A` | 49,515,525 bytes |
| `Voxora-v1.3.0-win-x64.zip` (self-contained portable) | `5C2C8BB206D54207F0FC1D19A974A24B374D84D236510DA62BCDD4BCD601F273` | 64,588,891 bytes |

---

## ⚠️ Gerçek Donanımda Doğrulanamayanlar

- Installer sessiz kurulum/kaldırma ve başlat menüsü kısayolları
- VB-CABLE kurulu olmayan sistemde otomatik ayar davranışı
- Windows 10 21H2 / Windows 11 24H2 DPI ölçekleme
- Uzun süreli (>1 saat) çalışma stabilitesi
- Tam ekran DirectX/Vulkan oyunlarında global kısayol yakalama
