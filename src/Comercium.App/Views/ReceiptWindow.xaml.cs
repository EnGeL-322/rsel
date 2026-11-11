using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Comercium.Application;

namespace Comercium.App.Views;

public partial class ReceiptWindow : Window
{
    public ReceiptWindow(ReportData receipt)
    {
        InitializeComponent();
        DataContext = new { receipt.Title, receipt.Subtitle, Rows = receipt.Rows.Select(row => row.Select(ReportExporter.Cell).ToArray()).ToList() };
        for (var index = 0; index < receipt.Columns.Count; index++)
            Lines.Columns.Add(new DataGridTextColumn { Header = receipt.Columns[index], Binding = new Binding($"[{index}]") { Mode = BindingMode.OneWay },
                Width = new DataGridLength(index == 1 ? 2.5 : 1, DataGridLengthUnitType.Star), MinWidth = 69 });
    }
}
