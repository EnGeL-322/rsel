using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Comercium.App.Views;

public partial class ReportsView : UserControl
{
    public ReportsView() => InitializeComponent();

    private void BindReportColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is not DataGridTextColumn column)
            return;
        column.Binding = new Binding($"[{e.PropertyName}]") { Mode = BindingMode.OneWay };
        column.MinWidth = 80;
        var weight = e.PropertyName is "Producto" or "Concepto" or "Cliente" or "Proveedor" ? 1.6 : 1;
        column.Width = new DataGridLength(weight, DataGridLengthUnitType.Star);
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0)));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        column.ElementStyle = style;
    }
}
