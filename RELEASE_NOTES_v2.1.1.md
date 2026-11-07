# Voxora 2.1.1

Radyal menüye **sayfalama** ve **yumuşak sayfa geçişi** eklendi.

## Yenilikler

- **Fare tekerleğiyle diğer efektler:** Daire menüsü artık yalnızca ilk 8 sesi
  göstermiyor. Görünür listedeki (kategori/arama filtresi) **tüm sesler** 8'lik
  sayfalara bölünür; tekerleği yukarı/aşağı çevirerek sayfalar arasında geçilir.
- **Yumuşak geçiş:** Sayfa değişirken eski dilimler yön yönünde dönerek solar,
  yeni dilimler karşıdan gelir (190 ms, ease-out). Hareket azaltma açıksa geçiş
  anında olur.
- **Sayfa göstergesi:** Merkezde `TEKERLEK · 1/2` biçiminde kaçıncı sayfada
  olduğun görünür.
- **Odak çalınmadan tekerlek:** Menü açıkken tekerlek global olarak yakalanır
  (düşük seviyeli fare kancası) ve oyuna/arkadaki pencereye **geçmez**; yani
  oyun zoom yapmaz, sayfa değişir. Menü kapanınca kanca bırakılır.
- Menüde tekerlek çevirmek 12 saniyelik otomatik kapanma sayacını sıfırlar.

## Diğer

- Sürüm 2.1.1, testler **120/120**.
- 2.1.0'daki cihaz adı düzeltmesi ve tüm 2.0 yenilikleri korunuyor.

## Bilinen sınırlar

- Radyal menü pencereli/kenarlıksız tam ekran oyunlarda görünür; özel tam ekranda
  oyun üstteki pencereyi gizleyebilir.
- Sayfa başına 8 ses; çok büyük kütüphanelerde tekerlekle birkaç tur gerekir.
- EXE ve kurulum paketi kod imzalı değildir.
