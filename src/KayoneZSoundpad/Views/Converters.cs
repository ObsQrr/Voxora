using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KayoneZSoundpad.Views;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool → mute ikonu. Segoe MDL2 Assets kod noktaları kullanılır; emoji
/// yazı tipi bağımlılığı (ve tofu kutuları) böylece ortadan kalkar.
/// </summary>
public class MuteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "\uE74F" : "\uE767"; // Mute / Volume

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → mute ipucu metni (dile duyarlı).</summary>
public class MuteTipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? KayoneZSoundpad.Services.LocalizationService.T("Main_Unmute")
            : KayoneZSoundpad.Services.LocalizationService.T("Main_Mute");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
