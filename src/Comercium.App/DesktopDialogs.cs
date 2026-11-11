using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;

namespace Comercium.App;

public sealed class DesktopDialogs(string dataFolder)
{
    private static Window Owner => System.Windows.Application.Current.MainWindow;

    public bool Confirm(string message) => MessageBox.Show(Owner, message, "Comercium", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public string? SelectPhoto()
    {
        var dialog = new OpenFileDialog { Filter = "Imágenes PNG y JPEG|*.png;*.jpg;*.jpeg" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? ChooseBackupDestination()
    {
        var dialog = new SaveFileDialog { Filter = "Respaldo de Comercium|*.comercium", DefaultExt = ".comercium",
            FileName = $"Comercium-{DateTime.Now:yyyy-MM-dd}" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? ChooseBackupSource()
    {
        var dialog = new OpenFileDialog { Filter = "Respaldo de Comercium|*.comercium", CheckFileExists = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void Inform(string message) => MessageBox.Show(Owner, message, "Comercium", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Print(string title, IEnumerable<string> lines)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        var document = new FlowDocument { PagePadding = new Thickness(32), FontSize = 13,
            PageHeight = dialog.PrintableAreaHeight, PageWidth = dialog.PrintableAreaWidth,
            ColumnWidth = double.PositiveInfinity };
        document.Blocks.Add(new Paragraph(new Bold(new Run(title))) { FontSize = 19 });
        foreach (var line in lines)
            document.Blocks.Add(new Paragraph(new Run(line)) { Margin = new Thickness(0, 0, 0, 7) });
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, title);
    }

    public void RecordError(Exception error) => Log(dataFolder, error);

    public static void Log(string folder, Exception error)
    {
        try { File.AppendAllText(Path.Combine(folder, "errores.log"), $"{DateTimeOffset.Now:O} {error}\n"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
