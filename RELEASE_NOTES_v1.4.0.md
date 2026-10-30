# Voxora v1.4.0 Release Notes

**Date:** 2026-10-30  
**Branch:** `voxora`

---

## 🔔 Yeni: Güncelleme Kontrolü

- Uygulama açılışında **günde en fazla bir kez** GitHub Releases üzerinden son sürüm kontrol edilir.
- Yeni sürüm varsa ana pencerenin **üstünde uyarı şeridi** ve tepsi bildirimi görünür.
- **Ayarlar → Güncellemeler** bölümünde:
  - Mevcut sürüm
  - **“Güncellemeyi Kontrol Et”** düğmesi
  - Otomatik kontrol açma/kapatma
  - Yeni sürüm varsa **“İndir”** düğmesi (GitHub release sayfasını açar)
- Güncelleme yalnızca bildirilir; **sessiz indirme/kurulum yapılmaz**.
- Kontrol başarısız olursa (çevrimdışı, GitHub limiti vb.) uygulama etkilenmez; durum metni gösterilir.
- Public repo olduğu için token kullanılmaz; kimliksiz GitHub API limitine uyulur (günde 1 kontrol).

---

## ✅ Doğrulama

- **Build:** `dotnet build -c Release` — 0 warning, 0 error
- **Testler:** **80/80 geçti** (önceki 70)
- Yeni testler:
  - Yeni sürüm / aynı sürüm / ön sürüm etiketi karşılaştırması
  - HTTP hatası ve bozuk JSON
  - Sürüm etiketi ayrıştırma
- **Gizlilik:** Kişisel ses dosyaları, `library.json`, `settings.json` repo'ya veya paketlere eklenmedi.

---

## 📦 Dağıtım

| Paket | SHA256 | Boyut |
|-------|--------|-------|
| `Voxora-Setup.exe` (Inno Setup 6 installer) | `53728DAE869445F882E84DA2023C0DCC86D620818410D66C6CB3835D0F8B0205` | 49,524,051 bytes |
| `Voxora-v1.4.0-win-x64.zip` (self-contained portable) | `FA03B2B817290223FFAE7CB860F0C68211647BEFF569840D4DEF8E81DF9F49DC` | 64,598,753 bytes |

---

## ⚠️ Gerçek Donanımda Doğrulanamayanlar

- Installer sessiz kurulum/kaldırma ve başlat menüsü kısayolları
- VB-CABLE kurulu olmayan sistemde otomatik ayar davranışı
- Farklı Windows sürümlerinde DPI ölçekleme
- Uzun süreli çalışma stabilitesi
- Tam ekran DirectX/Vulkan oyunlarında global kısayol yakalama
- Gerçek GitHub API’ye karşı canlı güncelleme bildirimi (birim testleri sahte HTTP yanıtı kullanır)
