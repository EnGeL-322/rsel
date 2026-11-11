using System.Globalization;
using System.Windows.Data;
using Comercium.Domain;

namespace Comercium.App;

public static class Display
{
    public static string Money(long cents) => $"S/ {cents / 100m:N2}";
    public static string Input(long cents) => (cents / 100m).ToString("0.00", CultureInfo.CurrentCulture);
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    public static long ParseMoney(string text, string label)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.CurrentCulture, out var number) || number < 0 || number > 1_000_000_000_000m || decimal.Round(number, 2) != number)
            throw new BusinessException($"Ingresa un importe válido para {label}, con hasta dos decimales.");
        return (long)(number * 100);
    }

    public static int ParseCount(string text, string label) => int.TryParse(text, out var number) && number >= 0
        ? number : throw new BusinessException($"Ingresa un número entero válido para {label}.");

    public static string Status(DispatchStatus status) => status switch
    {
        DispatchStatus.EnPreparación => "En preparación",
        DispatchStatus.ListoParaRecoger => "Listo para recoger",
        _ => status.ToString()
    };
}

public sealed class DisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        long cents when parameter is "money" => Display.Money(cents),
        DateTime at => Display.Local(at).ToString("g", culture),
        DispatchStatus status => Display.Status(status),
        bool state when parameter is "active" => state ? "Activo" : "Desactivado",
        bool state when parameter is "sale" => state ? "Anulada" : "Confirmada",
        bool state when parameter is "pick" => state ? "Verificado" : "Por verificar",
        _ => value ?? ""
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
