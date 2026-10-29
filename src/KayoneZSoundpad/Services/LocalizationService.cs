using System.Globalization;
using System.Windows;

namespace KayoneZSoundpad.Services;

/// <summary>
/// Basit, bakımı kolay yerelleştirme katmanı.
///
/// - Metinler kod içinde iki sözlükte tutulur (tr/en) → test edilebilir, paket URI'si gerektirmez.
/// - XAML tarafı {DynamicResource Key} ile bağlanır; dil değişince WPF otomatik günceller.
/// - Kod tarafı Loc.T("Key", args) kullanır.
/// - Varsayılan dil Türkçe'dir (mevcut davranış korunur).
/// </summary>
public static class LocalizationService
{
    public const string Turkish = "tr";
    public const string English = "en";

    private static string _language = Turkish;
    private static IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();
    private static ResourceDictionary? _resourceDictionary;

    static LocalizationService()
    {
        // Alan başlatıcıları metinsel sırada çalıştığı için Tr henüz null olabilir;
        // sözlükler hazır olduktan sonra varsayılan dili burada bağla.
        _current = Tr;
    }

    /// <summary>Dil değiştiğinde tetiklenir. Kod içinde üretilen metinler burada tazelenir.</summary>
    public static event Action? LanguageChanged;

    public static string Language => _language;
    public static bool IsEnglish => _language == English;

    public static string Normalize(string? language)
        => string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Turkish;

    /// <summary>Uygulama başlarken çağrılır (olay tetiklemez).</summary>
    public static void Initialize(string? language) => Apply(language, notify: false);

    /// <summary>Dili değiştirir; XAML kaynaklarını ve CurrentUICulture'ı günceller.</summary>
    public static void SetLanguage(string? language) => Apply(language, notify: true);

    private static void Apply(string? language, bool notify)
    {
        var lang = Normalize(language);
        _language = lang;
        _current = lang == English ? En : Tr;

        try
        {
            var culture = CultureInfo.GetCultureInfo(lang == English ? "en-US" : "tr-TR");
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch { /* kültür yoksa varsayılan kalır */ }

        var app = System.Windows.Application.Current;
        if (app != null)
        {
            var merged = app.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(merged[i], _resourceDictionary))
                    merged.RemoveAt(i);
            }

            var dict = new ResourceDictionary();
            foreach (var kv in _current) dict[kv.Key] = kv.Value;
            _resourceDictionary = dict;
            merged.Add(dict);
        }

        if (notify) LanguageChanged?.Invoke();
    }

    /// <summary>Anahtarı geçerli dilde döndürür. Anahtar yoksa anahtarın kendisi döner.</summary>
    public static string T(string key, params object?[] args)
    {
        if (!_current.TryGetValue(key, out var text)) text = key;
        if (args.Length == 0) return text;
        try { return string.Format(CultureInfo.CurrentCulture, text, args); }
        catch { return text; }
    }

    /// <summary>Testler ve denetimler için ham sözlük erişimi.</summary>
    public static IReadOnlyDictionary<string, string> Strings(string? language)
        => Normalize(language) == English ? En : Tr;

    public static IReadOnlyCollection<string> Keys => Tr.Keys.ToArray();

    public static bool HasKey(string key, string? language) => Strings(language).ContainsKey(key);

    // ------------------------------------------------------------------ TR
    private static readonly IReadOnlyDictionary<string, string> Tr =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Ortak
            ["Common_Cancel"] = "İptal",
            ["Common_Save"] = "Kaydet",
            ["Common_Close"] = "Kapat",
            ["Common_OK"] = "Tamam",
            ["Common_Default"] = "varsayılan",
            ["Common_SecondsShort"] = "sn",
            ["Common_On"] = "Aktif",
            ["Common_Off"] = "KAPALI",

            // Uygulama / tepsi
            ["App_AlreadyRunning"] = "Voxora zaten çalışıyor (sistem tepsisine bakın).",
            ["App_ErrorTitle"] = "Voxora - Hata",
            ["App_UnexpectedError"] = "Beklenmeyen hata: {0}\n\n{1}",
            ["Library_LoadFailed"] = "Ses kütüphanesi okunamadı. Mevcut dosya korunuyor; lütfen yedeğinizi kontrol edin:\n{0}",
            ["Tray_Show"] = "Göster",
            ["Tray_Exit"] = "Çıkış",
            ["Notify_DevicesChanged"] = "Ses aygıtları değişti.",

            // Kategoriler
            ["Category_All"] = "Tüm Sesler",
            ["Category_Favorites"] = "Favoriler",
            ["Category_NoCategory"] = "Kategori yok",
            ["Category_NewTitle"] = "Yeni Kategori",
            ["Category_NamePrompt"] = "Kategori adı:",
            ["Category_RenameTitle"] = "Kategori Yeniden Adlandır",
            ["Category_RenamePrompt"] = "Yeni ad:",
            ["Category_Rename"] = "Yeniden Adlandır",
            ["Category_Delete"] = "Kategoriyi Sil",
            ["Category_DeleteConfirm"] = "'{0}' kategorisini ve içindeki sesleri sil?",

            // Ana pencere
            ["Main_SearchPlaceholder"] = "Ses ara...",
            ["Main_AddSound"] = "Ses Ekle",
            ["Main_AddSoundFile"] = "Ses Dosyası Ekle",
            ["Main_AddFolder"] = "Klasör",
            ["Main_Import"] = "İçe Al",
            ["Main_Export"] = "Dışa Aktar",
            ["Main_ExportSounds"] = "Sesler",
            ["Main_ExportSoundsTip"] = "Tüm ses dosyalarını ZIP olarak dışa aktar",
            ["Main_Settings"] = "Ayarlar",
            ["Main_AddSoundTip"] = "Ses dosyası ekle",
            ["Main_AddFolderTip"] = "Klasördeki tüm ses dosyalarını ekle",
            ["Main_ImportTip"] = "Kütüphaneyi geri yükle (.zip)",
            ["Main_ExportTip"] = "Kütüphaneyi yedekle (.zip)",
            ["Main_SettingsTip"] = "Ayarlar (Ctrl+,)",
            ["Main_Microphone"] = "MİKROFON",
            ["Main_EffectSounds"] = "EFEKT SESLERİ",
            ["Main_Effect"] = "EFEKT",
            ["Main_OutputMeter"] = "ÇIKIŞ",
            ["Main_OutputGameDiscord"] = "ÇIKIŞ (OYUN / DISCORD)",
            ["Main_LocalMonitor"] = "Lokal Dinle",
            ["Main_Stop"] = "Durdur",
            ["Main_StopAll"] = "Tüm Sesleri Durdur",
            ["Main_Categories"] = "KATEGORİLER",
            ["Main_AddCategory"] = "Kategori Ekle",
            ["Main_NewCategory"] = "Yeni Kategori",
            ["Main_CategoryGuide"] = "Mikrofon Yönlendirme Rehberi",
            ["Main_EmptyTitle"] = "Henüz Ses Efekti Yok",
            ["Main_EmptyBody"] = "MP3, WAV, OGG veya FLAC dosyalarını buraya sürükleyip bırakın.\nKarşı tarafın duyması için Mikrofon Rehberi'ni açın.",
            ["Main_MicGuide"] = "Mikrofon Rehberi",
            ["Main_CardPlayStop"] = "Çal / Durdur",
            ["Main_CardMuteToggle"] = "Sesi Kıs / Aç",
            ["Main_CardEdit"] = "Düzenle...",
            ["Main_CardFavorite"] = "Favori",
            ["Main_CardRemove"] = "Kütüphaneden Kaldır",
            ["Main_Mute"] = "Sesi kıs",
            ["Main_Unmute"] = "Sesi aç",
            ["Main_MuteAutomation"] = "Sesi Kıs/Aç",
            ["Main_MicActive"] = "Aktif",
            ["Main_MicDevice"] = "Mikrofon Aygıtı",
            ["Main_MicVolume"] = "Mikrofon Ses Seviyesi",
            ["Main_FxVolume"] = "Efekt Ses Seviyesi",
            ["Main_MicActiveAutomation"] = "Mikrofon Aktif",
            ["Main_MonitorAutomation"] = "Lokal Dinleme",
            ["Main_OutputDevice"] = "Çıkış Aygıtı",
            ["Main_CategoriesAutomation"] = "Ses Kategorileri",

            // Dosya seçiciler
            ["Dialog_SelectSoundTitle"] = "Ses Dosyası Seç",
            ["Dialog_SoundFilesFilter"] = "Ses Dosyaları|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac;*.wma;*.opus|Tüm Dosyalar|*.*",
            ["Dialog_SelectFolderTitle"] = "Klasör Seç",
            ["Dialog_BackupFilter"] = "Voxora Yedek|*.zip",
            ["Dialog_SoundsZipFilter"] = "ZIP Arşivi|*.zip",
            ["Dialog_SelectSoundsZipTitle"] = "Ses Dosyalarını Dışa Aktar",
            ["Dialog_SelectBackupTitle"] = "Kütüphane Yedeğini Seç",
            ["Dialog_SaveBackupTitle"] = "Kütüphaneyi Yedekle",

            // Kablolar / durum
            ["Cable_Active"] = "✓ Voxora sanal aygıtları bağlı — efektler mikrofonuna gidiyor",
            ["Cable_InstalledNotRouted"] = "⚠ Voxora sanal aygıtları kurulu ama çıkış bağlı değil (tıkla)",
            ["Cable_Missing"] = "Voxora sanal aygıtları bulunamadı (rehber için tıkla)",

            // Kart
            ["Card_FileMissing"] = "⚠ dosya yok",
            ["Card_Volume"] = "Ses seviyesi",

            // Ayarlar
            ["Settings_Title"] = "Ayarlar",
            ["Settings_General"] = "GENEL",
            ["Settings_Theme"] = "Tema",
            ["Settings_ThemeDark"] = "Koyu",
            ["Settings_ThemeLight"] = "Açık",
            ["Settings_Language"] = "Dil",
            ["Settings_ThemeAutomation"] = "Uygulama Teması",
            ["Settings_LanguageAutomation"] = "Uygulama Dili",
            ["Settings_AutoStart"] = "Windows açılışında başlat",
            ["Settings_StartMinimized"] = "Küçültülmüş başlat",
            ["Settings_CloseToTray"] = "Kapat tuşu tepsiye küçültür",
            ["Settings_CopyOnImport"] = "İçe aktarılan dosyaları Sounds klasörüne kopyala",
            ["Settings_CopyAutomation"] = "Dosyaları kopyala",
            ["Settings_Behavior"] = "DAVRANIŞ",
            ["Settings_DefaultBehavior"] = "Yeni sesin davranışı",
            ["Settings_BehaviorAutomation"] = "Yeni ses davranışı",
            ["Settings_BehaviorRestart"] = "Yeniden başlat",
            ["Settings_BehaviorOverlap"] = "Üstüne çal",
            ["Settings_BehaviorToggle"] = "Çalıyorsa durdur",
            ["Settings_SuppressHotkeys"] = "Kısayolların diğer uygulamalara gitmesini engelle",
            ["Settings_SuppressAutomation"] = "Kısayolları yakala",
            ["Settings_EngineLatency"] = "Motor gecikmesi",
            ["Settings_LatencyAutomation"] = "Motor gecikmesi (ms)",
            ["Settings_Cache"] = "Bellek önbelleği",
            ["Settings_CacheAutomation"] = "Bellek önbelleği (MB)",
            ["Settings_Routing"] = "SES YÖNLENDİRME",
            ["Settings_Output"] = "Çıkış (Discord/Oyun)",
            ["Settings_OutputTip"] = "VB-CABLE kuruluysa 'Voxora In Audio' seçin; Discord'da mikrofonu 'Voxora Out Audio' yapın",
            ["Settings_OutputAutomation"] = "Çıkış Aygıtı",
            ["Settings_Microphone"] = "Mikrofon",
            ["Settings_Monitor"] = "Lokal Monitör (Kulaklık)",
            ["Settings_MonitorAutomation"] = "Monitör Aygıtı",
            ["Settings_AutoSetup"] = "Oto Ayarla",
            ["Settings_AutoSetupAutomation"] = "Ses yönlendirmesini otomatik ayarla",
            ["Settings_OpenGuide"] = "Mikrofon Rehberi",
            ["Settings_AutoSetupOk"] = "Ses yönlendirmesi otomatik yapılandırıldı.\n\nÇıkış: Voxora In Audio\nMikrofon: {0}\nLokal monitör: {1}",
            ["Settings_AutoSetupMissing"] = "Voxora sanal aygıtları bulunamadı. VB-CABLE kurup bilgisayarı yeniden başlatın.",
            ["Settings_AutoSetupFailed"] = "Yönlendirme ayarlanamadı.",
            ["Settings_Ducking"] = "DUCKING (Mikrofon Kısma)",
            ["Settings_DuckingAutomation"] = "Ducking aktif",
            ["Settings_DuckEnable"] = "Efekt çalarken mikrofonu kıs",
            ["Settings_DuckRatio"] = "Kısma oranı",
            ["Settings_DuckAutomation"] = "Ducking oranı",
            ["Settings_Hotkeys"] = "KISAYOLLAR",
            ["Settings_StopAllHotkey"] = "Tümünü Durdur",
            ["Settings_StopAllHotkeyAutomation"] = "Tüm sesleri durdur kısayolu",
            ["Settings_About"] = "HAKKINDA",
            ["Settings_AboutTagline"] = "NAudio/WASAPI tabanlı ses efekti pedi",
            ["Settings_AboutData"] = "Voxora v{0} — NAudio/WASAPI tabanlı ses efekti pedi. Veriler: {1}",
            ["Settings_HotkeyConflictTitle"] = "Kısayol çakışması",
            ["Settings_BrowserError"] = "Tarayıcı açılamadı. Adres: https://vb-audio.com/Cable/",
            ["Settings_SoundSettingsError"] = "Ses ayarları açılamadı.",

            // Mikrofon rehberi
            ["Guide_Title"] = "Mikrofon Yönlendirme Rehberi",
            ["Guide_Subtitle"] = "Ses efektlerinin karşı tarafa gitmesi için sanal kablo kurulumu",
            ["Guide_Step1"] = "ADIM 1: VB-CABLE KURULUMU",
            ["Guide_Step1Body"] = "1. https://vb-audio.com/Cable/ adresine gidin.\n2. \"Download\" → \"VB-CABLE Driver\" indirin.\n3. Arşivi çıkartın, VBCABLE_Driver64.exe yönetici olarak çalıştırın.\n4. Bilgisayarı yeniden başlatın.",
            ["Guide_OpenDownload"] = "İndirme Sayfasını Aç",
            ["Guide_DownloadAutomation"] = "VB-CABLE indir",
            ["Guide_Step2"] = "ADIM 2: WINDOWS SES AYARLARI",
            ["Guide_SetOutput"] = "Çıkış aygıtını {0} yapın",
            ["Guide_SetInput"] = "Giriş aygıtını kendi mikrofonunuz (ör. {0}) yapın",
            ["Guide_OpenSoundSettings"] = "Ses Ayarlarını Aç",
            ["Guide_OpenSoundSettingsAutomation"] = "Windows ses ayarlarını aç",
            ["Guide_Step3"] = "ADIM 3: DISCORD / OYUN AYARLARI",
            ["Guide_Step3Body"] = "Discord'da: Kullanıcı Ayarları → Ses ve Video\n• Giriş Aygıtı: Voxora Out Audio\n• Gürültü Bastırma, Yankı İptali, Otomatik Kazanç: KAPALI (Krisp açıkken efektler bozulur)",
            ["Guide_AutoNote"] = "Otomatik yapılandırma: Ayarlar penceresindeki \"Oto Ayarla\" butonu Voxora sanal aygıtlarını algılayıp çıkışı Voxora In Audio, mikrofonu varsayılan aygıta çeker.",
            ["Guide_AutoReadyTitle"] = "Voxora — Mikrofon Yönlendirme Hazır",
            ["Guide_AutoReadyBody"] = "✓ Yönlendirme kuruldu\n\n• Çıkış (kablo):  Voxora In Audio\n• Mikrofon:       {0}{1}\n• Lokal dinleme:  {2}{3}\n\nŞimdi Discord / Valorant gibi uygulamada\nGİRİŞ AYGITI olarak \"Voxora Out Audio\" seç.\n\nNot: Kendi mikrofonunu Discord'da doğrudan seçersen\nefektler duyulmaz — Voxora Out Audio seçmelisin.",
            ["Guide_NotFound"] = "(bulunamadı)",
            ["Guide_Off"] = "  [KAPALI]",

            // Düzenleme
            ["Edit_Title"] = "Sesi Düzenle",
            ["Edit_Name"] = "İSİM",
            ["Edit_NameAutomation"] = "Ses adı",
            ["Edit_Category"] = "KATEGORİ",
            ["Edit_CategoryAutomation"] = "Kategori",
            ["Edit_Icon"] = "İKON",
            ["Edit_IconAutomation"] = "Ses ikonu (emoji)",
            ["Edit_IconHint"] = "Emoji veya kısa metin (örn: 🎵 💥 ⚡)",
            ["Edit_File"] = "SES DOSYASI",
            ["Edit_FileAutomation"] = "Ses dosyası yolu",
            ["Edit_Change"] = "Değiştir",
            ["Edit_ChangeAutomation"] = "Ses dosyasını değiştir",
            ["Edit_Hotkey"] = "KISAYOL",
            ["Edit_HotkeyAutomation"] = "Oynatma kısayolu",
            ["Edit_Clear"] = "Temizle",
            ["Edit_ClearAutomation"] = "Kısayolu temizle",
            ["Edit_Volume"] = "SES SEVİYESİ",
            ["Edit_VolumeAutomation"] = "Ses seviyesi",
            ["Edit_Favorite"] = "Favorilere ekle (listede öne çıkar)",
            ["Edit_FavoriteAutomation"] = "Favori",
            ["Edit_Muted"] = "Başlangıçta sessiz",
            ["Edit_MutedAutomation"] = "Başlangıçta sessiz",
            ["Edit_FileInvalid"] = "Seçilen dosya bulunamadı veya desteklenmiyor.",

            // Giriş penceresi / kısayol kutusu
            ["Input_DefaultPrompt"] = "Ad:",
            ["Hotkey_Hint"] = "Tıkla ve kısayol tuşlarına bas (Esc = temizle)",
            ["Hotkey_PendingSuffix"] = " (tuşa bas)",
            ["Hotkey_PendingEllipsis"] = "...",
            ["Key_LeftMouse"] = "Sol Tuş",
            ["Key_RightMouse"] = "Sağ Tuş",
            ["Key_MiddleMouse"] = "Orta Tuş",
            ["Key_Left"] = "Sol",
            ["Key_Up"] = "Yukarı",
            ["Key_Right"] = "Sağ",
            ["Key_Down"] = "Aşağı",
            ["Key_LeftWin"] = "Sol Win",
            ["Key_RightWin"] = "Sağ Win",
            ["Key_NumPrefix"] = "Num ",

            // Aygıtlar
            ["Device_DefaultSuffix"] = "  (varsayılan)",
            ["Device_UnknownName"] = "(bilinmeyen aygıt)",

            // Bildirimler
            ["Notify_AddFailed"] = "Ses eklenemedi: {0}",
            ["Notify_ImportDone"] = "İçe aktarma tamamlandı: {0} ses eklendi.",
            ["Notify_ExportDone"] = "Dışa aktarma tamamlandı: {0} ses → {1}",
            ["Notify_ExportSoundsDone"] = "Ses dosyaları dışa aktarıldı: {0} dosya → {1}",
            ["Notify_ExportSoundsEmpty"] = "Dışa aktarılacak ses dosyası bulunamadı.",
            ["Notify_ExportSoundsFailed"] = "Ses dosyaları dışa aktarılamadı: {0}",
            ["Export_TargetIsSoundFile"] = "Hedef dosya bir ses dosyasıyla aynı olamaz.",
            ["Notify_ImportFailed"] = "İçe aktarma başarısız: {0}",
            ["Notify_ExportFailed"] = "Dışa aktarma başarısız: {0}",

            // Kısayol çakışmaları
            ["Hotkey_StopAllOwner"] = "Tüm sesleri durdur",
            ["Hotkey_ConflictBoth"] = "⚠ \"{0}\" kısayolu hem \"{1}\" hem \"{2}\" için atanmış!",
            ["Hotkey_AssignedToStopAll"] = "\"{0}\" kısayolu \"Tüm sesleri durdur\" için atanmış.",
            ["Hotkey_AssignedToSound"] = "\"{0}\" kısayolu \"{1}\" için atanmış.",

            // Ses motoru / çalma hataları
            ["Err_FileNotFound"] = "Dosya bulunamadı: {0}",
            ["Err_DecodeFailed"] = "Ses çözülemedi: {0} — {1}",
            ["Err_EngineNotRunning"] = "Ses motoru çalışmıyor — aygıt ayarlarını kontrol edin.",
            ["Err_OutputOpen"] = "Çıkış aygıtı açılamadı. Ses yönlendirme şu an kapalı — ayarları kontrol edin.",
            ["Info_EngineStarted"] = "Ses motoru başlatıldı.",
            ["Err_OutputNotFound"] = "Seçilen çıkış aygıtı bulunamadı.",
            ["Err_OutputStart"] = "Çıkış aygıtı başlatılamadı: {0}",
            ["Err_MicNotFound"] = "Seçilen mikrofon bulunamadı.",
            ["Err_MicCapture"] = "Mikrofon yakalama hatası: {0}",
            ["Err_MicStart"] = "Mikrofon başlatılamadı: {0}",
            ["Err_SoundNotFound"] = "Ses dosyası bulunamadı.",

            // İçe/dışa aktarma
            ["Import_InvalidBackup"] = "Geçersiz yedek dosyası (library.json yok).",
            ["Import_TooLarge"] = "Yedek dosyası çok büyük veya çok fazla dosya içeriyor.",
        };

    // ------------------------------------------------------------------ EN
    private static readonly IReadOnlyDictionary<string, string> En =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Common_Cancel"] = "Cancel",
            ["Common_Save"] = "Save",
            ["Common_Close"] = "Close",
            ["Common_OK"] = "OK",
            ["Common_Default"] = "default",
            ["Common_SecondsShort"] = "s",
            ["Common_On"] = "On",
            ["Common_Off"] = "OFF",

            ["App_AlreadyRunning"] = "Voxora is already running (check the system tray).",
            ["App_ErrorTitle"] = "Voxora - Error",
            ["App_UnexpectedError"] = "Unexpected error: {0}\n\n{1}",
            ["Library_LoadFailed"] = "The sound library could not be loaded. The existing file is being preserved; please check your backup:\n{0}",
            ["Tray_Show"] = "Show",
            ["Tray_Exit"] = "Exit",
            ["Notify_DevicesChanged"] = "Audio devices changed.",

            ["Category_All"] = "All Sounds",
            ["Category_Favorites"] = "Favorites",
            ["Category_NoCategory"] = "No category",
            ["Category_NewTitle"] = "New Category",
            ["Category_NamePrompt"] = "Category name:",
            ["Category_RenameTitle"] = "Rename Category",
            ["Category_RenamePrompt"] = "New name:",
            ["Category_Rename"] = "Rename",
            ["Category_Delete"] = "Delete Category",
            ["Category_DeleteConfirm"] = "Delete the category '{0}' and the sounds inside it?",

            ["Main_SearchPlaceholder"] = "Search sounds...",
            ["Main_AddSound"] = "Add Sound",
            ["Main_AddSoundFile"] = "Add Sound File",
            ["Main_AddFolder"] = "Folder",
            ["Main_Import"] = "Import",
            ["Main_Export"] = "Export",
            ["Main_ExportSounds"] = "Sounds",
            ["Main_ExportSoundsTip"] = "Export all sound files as ZIP",
            ["Main_Settings"] = "Settings",
            ["Main_AddSoundTip"] = "Add a sound file",
            ["Main_AddFolderTip"] = "Add every sound file in a folder",
            ["Main_ImportTip"] = "Restore a library backup (.zip)",
            ["Main_ExportTip"] = "Back up the library (.zip)",
            ["Main_SettingsTip"] = "Settings (Ctrl+,)",
            ["Main_Microphone"] = "MICROPHONE",
            ["Main_EffectSounds"] = "SOUND EFFECTS",
            ["Main_Effect"] = "FX",
            ["Main_OutputMeter"] = "OUT",
            ["Main_OutputGameDiscord"] = "OUTPUT (GAME / DISCORD)",
            ["Main_LocalMonitor"] = "Local Monitor",
            ["Main_Stop"] = "Stop",
            ["Main_StopAll"] = "Stop All Sounds",
            ["Main_Categories"] = "CATEGORIES",
            ["Main_AddCategory"] = "Add Category",
            ["Main_NewCategory"] = "New Category",
            ["Main_CategoryGuide"] = "Microphone Routing Guide",
            ["Main_EmptyTitle"] = "No Sound Effects Yet",
            ["Main_EmptyBody"] = "Drag and drop MP3, WAV, OGG or FLAC files here.\nOpen the Microphone Guide so other people can hear them.",
            ["Main_MicGuide"] = "Microphone Guide",
            ["Main_CardPlayStop"] = "Play / Stop",
            ["Main_CardMuteToggle"] = "Mute / Unmute",
            ["Main_CardEdit"] = "Edit...",
            ["Main_CardFavorite"] = "Favorite",
            ["Main_CardRemove"] = "Remove from Library",
            ["Main_Mute"] = "Mute",
            ["Main_Unmute"] = "Unmute",
            ["Main_MuteAutomation"] = "Mute/Unmute",
            ["Main_MicActive"] = "On",
            ["Main_MicDevice"] = "Microphone Device",
            ["Main_MicVolume"] = "Microphone Volume",
            ["Main_FxVolume"] = "Effect Volume",
            ["Main_MicActiveAutomation"] = "Microphone enabled",
            ["Main_MonitorAutomation"] = "Local monitoring",
            ["Main_OutputDevice"] = "Output Device",
            ["Main_CategoriesAutomation"] = "Sound categories",

            ["Dialog_SelectSoundTitle"] = "Select Sound File",
            ["Dialog_SoundFilesFilter"] = "Sound Files|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac;*.wma;*.opus|All Files|*.*",
            ["Dialog_SelectFolderTitle"] = "Select Folder",
            ["Dialog_BackupFilter"] = "Voxora Backup|*.zip",
            ["Dialog_SoundsZipFilter"] = "ZIP Archive|*.zip",
            ["Dialog_SelectSoundsZipTitle"] = "Export Sound Files",
            ["Dialog_SelectBackupTitle"] = "Select Library Backup",
            ["Dialog_SaveBackupTitle"] = "Back Up Library",

            ["Cable_Active"] = "✓ Voxora virtual devices connected — effects are going to your microphone",
            ["Cable_InstalledNotRouted"] = "⚠ Voxora virtual devices are installed but the output is not connected (click)",
            ["Cable_Missing"] = "Voxora virtual devices not found (click for the guide)",

            ["Card_FileMissing"] = "⚠ file missing",
            ["Card_Volume"] = "Volume",

            ["Settings_Title"] = "Settings",
            ["Settings_General"] = "GENERAL",
            ["Settings_Theme"] = "Theme",
            ["Settings_ThemeDark"] = "Dark",
            ["Settings_ThemeLight"] = "Light",
            ["Settings_Language"] = "Language",
            ["Settings_ThemeAutomation"] = "Application theme",
            ["Settings_LanguageAutomation"] = "Application language",
            ["Settings_AutoStart"] = "Start with Windows",
            ["Settings_StartMinimized"] = "Start minimized",
            ["Settings_CloseToTray"] = "Close button minimizes to tray",
            ["Settings_CopyOnImport"] = "Copy imported files into the Sounds folder",
            ["Settings_CopyAutomation"] = "Copy files",
            ["Settings_Behavior"] = "BEHAVIOR",
            ["Settings_DefaultBehavior"] = "Behavior for new sounds",
            ["Settings_BehaviorAutomation"] = "New sound behavior",
            ["Settings_BehaviorRestart"] = "Restart",
            ["Settings_BehaviorOverlap"] = "Overlap",
            ["Settings_BehaviorToggle"] = "Stop if playing",
            ["Settings_SuppressHotkeys"] = "Prevent shortcuts from reaching other applications",
            ["Settings_SuppressAutomation"] = "Capture shortcuts",
            ["Settings_EngineLatency"] = "Engine latency",
            ["Settings_LatencyAutomation"] = "Engine latency (ms)",
            ["Settings_Cache"] = "Memory cache",
            ["Settings_CacheAutomation"] = "Memory cache (MB)",
            ["Settings_Routing"] = "AUDIO ROUTING",
            ["Settings_Output"] = "Output (Discord/Game)",
            ["Settings_OutputTip"] = "If VB-CABLE is installed select 'Voxora In Audio'; in Discord set the microphone to 'Voxora Out Audio'",
            ["Settings_OutputAutomation"] = "Output Device",
            ["Settings_Microphone"] = "Microphone",
            ["Settings_Monitor"] = "Local Monitor (Headphones)",
            ["Settings_MonitorAutomation"] = "Monitor Device",
            ["Settings_AutoSetup"] = "Auto Setup",
            ["Settings_AutoSetupAutomation"] = "Configure audio routing automatically",
            ["Settings_OpenGuide"] = "Microphone Guide",
            ["Settings_AutoSetupOk"] = "Audio routing configured automatically.\n\nOutput: Voxora In Audio\nMicrophone: {0}\nLocal monitor: {1}",
            ["Settings_AutoSetupMissing"] = "Voxora virtual devices not found. Install VB-CABLE and restart the computer.",
            ["Settings_AutoSetupFailed"] = "Routing could not be configured.",
            ["Settings_Ducking"] = "DUCKING (Mic Attenuation)",
            ["Settings_DuckingAutomation"] = "Ducking enabled",
            ["Settings_DuckEnable"] = "Lower the microphone while effects play",
            ["Settings_DuckRatio"] = "Ducking ratio",
            ["Settings_DuckAutomation"] = "Ducking ratio",
            ["Settings_Hotkeys"] = "HOTKEYS",
            ["Settings_StopAllHotkey"] = "Stop All",
            ["Settings_StopAllHotkeyAutomation"] = "Stop all sounds shortcut",
            ["Settings_About"] = "ABOUT",
            ["Settings_AboutTagline"] = "NAudio/WASAPI-based sound effect pad",
            ["Settings_AboutData"] = "Voxora v{0} — NAudio/WASAPI-based sound effect pad. Data: {1}",
            ["Settings_HotkeyConflictTitle"] = "Hotkey conflict",
            ["Settings_BrowserError"] = "Could not open the browser. Address: https://vb-audio.com/Cable/",
            ["Settings_SoundSettingsError"] = "Could not open sound settings.",

            ["Guide_Title"] = "Microphone Routing Guide",
            ["Guide_Subtitle"] = "Set up the virtual cable so other people hear your sound effects",
            ["Guide_Step1"] = "STEP 1: INSTALL VB-CABLE",
            ["Guide_Step1Body"] = "1. Go to https://vb-audio.com/Cable/.\n2. Click \"Download\" → \"VB-CABLE Driver\".\n3. Extract the archive and run VBCABLE_Driver64.exe as administrator.\n4. Restart the computer.",
            ["Guide_OpenDownload"] = "Open Download Page",
            ["Guide_DownloadAutomation"] = "Download VB-CABLE",
            ["Guide_Step2"] = "STEP 2: WINDOWS SOUND SETTINGS",
            ["Guide_SetOutput"] = "Set the output device to {0}",
            ["Guide_SetInput"] = "Set the input device to your own microphone (e.g. {0})",
            ["Guide_OpenSoundSettings"] = "Open Sound Settings",
            ["Guide_OpenSoundSettingsAutomation"] = "Open Windows sound settings",
            ["Guide_Step3"] = "STEP 3: DISCORD / GAME SETTINGS",
            ["Guide_Step3Body"] = "In Discord: User Settings → Voice & Video\n• Input Device: Voxora Out Audio\n• Noise Suppression, Echo Cancellation, Automatic Gain Control: OFF (Krisp breaks the effects)",
            ["Guide_AutoNote"] = "Automatic setup: the \"Auto Setup\" button in Settings detects the Voxora virtual devices and routes the output to Voxora In Audio with your default microphone.",
            ["Guide_AutoReadyTitle"] = "Voxora — Microphone Routing Ready",
            ["Guide_AutoReadyBody"] = "✓ Routing configured\n\n• Output (cable):  Voxora In Audio\n• Microphone:      {0}{1}\n• Local monitor:   {2}{3}\n\nNow set the INPUT DEVICE in Discord / Valorant\nand similar apps to \"Voxora Out Audio\".\n\nNote: if you select your own microphone directly in Discord\nthe effects will not be heard — choose Voxora Out Audio.",
            ["Guide_NotFound"] = "(not found)",
            ["Guide_Off"] = "  [OFF]",

            ["Edit_Title"] = "Edit Sound",
            ["Edit_Name"] = "NAME",
            ["Edit_NameAutomation"] = "Sound name",
            ["Edit_Category"] = "CATEGORY",
            ["Edit_CategoryAutomation"] = "Category",
            ["Edit_Icon"] = "ICON",
            ["Edit_IconAutomation"] = "Sound icon (emoji)",
            ["Edit_IconHint"] = "Emoji or short text (e.g. 🎵 💥 ⚡)",
            ["Edit_File"] = "AUDIO FILE",
            ["Edit_FileAutomation"] = "Sound file path",
            ["Edit_Change"] = "Change",
            ["Edit_ChangeAutomation"] = "Change sound file",
            ["Edit_Hotkey"] = "HOTKEY",
            ["Edit_HotkeyAutomation"] = "Playback shortcut",
            ["Edit_Clear"] = "Clear",
            ["Edit_ClearAutomation"] = "Clear shortcut",
            ["Edit_Volume"] = "VOLUME",
            ["Edit_VolumeAutomation"] = "Volume",
            ["Edit_Favorite"] = "Add to favorites (highlighted in the list)",
            ["Edit_FavoriteAutomation"] = "Favorite",
            ["Edit_Muted"] = "Start muted",
            ["Edit_MutedAutomation"] = "Start muted",
            ["Edit_FileInvalid"] = "The selected file was not found or is not supported.",

            ["Input_DefaultPrompt"] = "Name:",
            ["Hotkey_Hint"] = "Click and press a shortcut (Esc = clear)",
            ["Hotkey_PendingSuffix"] = " (press a key)",
            ["Hotkey_PendingEllipsis"] = "...",
            ["Key_LeftMouse"] = "Left Mouse",
            ["Key_RightMouse"] = "Right Mouse",
            ["Key_MiddleMouse"] = "Middle Mouse",
            ["Key_Left"] = "Left",
            ["Key_Up"] = "Up",
            ["Key_Right"] = "Right",
            ["Key_Down"] = "Down",
            ["Key_LeftWin"] = "Left Win",
            ["Key_RightWin"] = "Right Win",
            ["Key_NumPrefix"] = "Num ",

            ["Device_DefaultSuffix"] = "  (default)",
            ["Device_UnknownName"] = "(unknown device)",

            ["Notify_AddFailed"] = "Could not add sound: {0}",
            ["Notify_ImportDone"] = "Import complete: {0} sounds added.",
            ["Notify_ExportDone"] = "Export complete: {0} sounds → {1}",
            ["Notify_ExportSoundsDone"] = "Sound files exported: {0} files → {1}",
            ["Notify_ExportSoundsEmpty"] = "No sound files found to export.",
            ["Notify_ExportSoundsFailed"] = "Could not export sound files: {0}",
            ["Export_TargetIsSoundFile"] = "The destination cannot be the same as a sound file.",
            ["Notify_ImportFailed"] = "Import failed: {0}",
            ["Notify_ExportFailed"] = "Export failed: {0}",

            ["Hotkey_StopAllOwner"] = "Stop all sounds",
            ["Hotkey_ConflictBoth"] = "⚠ The shortcut \"{0}\" is assigned to both \"{1}\" and \"{2}\"!",
            ["Hotkey_AssignedToStopAll"] = "The shortcut \"{0}\" is assigned to \"Stop all sounds\".",
            ["Hotkey_AssignedToSound"] = "The shortcut \"{0}\" is assigned to \"{1}\".",

            ["Err_FileNotFound"] = "File not found: {0}",
            ["Err_DecodeFailed"] = "Could not decode audio: {0} — {1}",
            ["Err_EngineNotRunning"] = "The audio engine is not running — check the device settings.",
            ["Err_OutputOpen"] = "Could not open the output device. Audio routing is currently off — check the settings.",
            ["Info_EngineStarted"] = "Audio engine started.",
            ["Err_OutputNotFound"] = "The selected output device was not found.",
            ["Err_OutputStart"] = "Could not start the output device: {0}",
            ["Err_MicNotFound"] = "The selected microphone was not found.",
            ["Err_MicCapture"] = "Microphone capture error: {0}",
            ["Err_MicStart"] = "Could not start the microphone: {0}",
            ["Err_SoundNotFound"] = "Sound file not found.",

            ["Import_InvalidBackup"] = "Invalid backup file (library.json is missing).",
            ["Import_TooLarge"] = "The backup file is too large or contains too many files.",
        };
}
