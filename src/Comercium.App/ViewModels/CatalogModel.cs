using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class CatalogModel : ViewModel
{
    private readonly Session session;
    private readonly ICatalog catalog;
    private IReadOnlyList<ProductRow> products = [];
    private IReadOnlyList<PriceRow> prices = [];
    private ProductRow? selected;
    private ProductForm form = new();
    private BitmapSource? preview;

    public CatalogModel(Session session, DesktopDialogs dialogs, ICatalog catalog) : base(dialogs)
    {
        this.session = session;
        this.catalog = catalog;
        Search = Action(Refresh);
        New = Action(() => Load(null));
        Edit = Action(() => Load(Selected?.Id), () => Selected is not null);
        Save = Action(SaveProduct);
        ChoosePhoto = Action(ReadPhoto);
        RemovePhoto = Action(() => { Form.Photo = null; Preview = null; }, () => Preview is not null);
        Run(Refresh);
    }

    public string Term { get; set; } = "";
    public string CategoryFilter { get; set; } = "";
    public bool ActiveOnly { get; set; }
    public IReadOnlyList<ProductRow> Products { get => products; private set => Set(ref products, value); }
    public IReadOnlyList<PriceRow> Prices { get => prices; private set => Set(ref prices, value); }
    public ProductRow? Selected { get => selected; set => Set(ref selected, value); }
    public ProductForm Form { get => form; private set => Set(ref form, value); }
    public BitmapSource? Preview { get => preview; private set => Set(ref preview, value); }
    public ICommand Search { get; }
    public ICommand New { get; }
    public ICommand Edit { get; }
    public ICommand Save { get; }
    public ICommand ChoosePhoto { get; }
    public ICommand RemovePhoto { get; }

    private void Refresh() => Products = catalog.Search(session, Term, CategoryFilter, ActiveOnly);

    private void Load(int? id)
    {
        if (!id.HasValue)
        {
            Form = new ProductForm();
            Preview = null;
            Prices = [];
            return;
        }

        var item = catalog.Load(session, id.Value);
        Form = new ProductForm { Id = item.Id, Code = item.Code, Name = item.Name, Category = item.Category,
            Description = item.Description, Unit = item.Unit, Location = item.Location, Cost = Display.Input(item.Cost),
            Retail = Display.Input(item.Retail), Wholesale = Display.Input(item.Wholesale), Minimum = item.Minimum.ToString(),
            Active = item.Active, Photo = item.Photo };
        Preview = item.Photo is null ? null : DecodePhoto(item.Photo);
        Prices = catalog.Prices(session, item.Id);
    }

    private void SaveProduct()
    {
        if (Form.Id != 0 && !Form.Active && !Dialogs.Confirm("¿Guardar este producto como desactivado?"))
            return;
        var id = catalog.Save(session, new ProductDraft(Form.Id, Form.Code, Form.Name, Form.Description,
            Form.Category, Form.Unit, Form.Location, Display.ParseMoney(Form.Cost, "costo"),
            Display.ParseMoney(Form.Retail, "precio minorista"), Display.ParseMoney(Form.Wholesale, "precio mayorista"),
            Display.ParseCount(Form.Minimum, "stock mínimo"), Form.Photo, Form.Active));
        Form.Id = id;
        Refresh();
        Load(id);
        Selected = Products.SingleOrDefault(row => row.Id == id);
        Show("Producto guardado.");
    }

    private void ReadPhoto()
    {
        var file = Dialogs.SelectPhoto();
        if (file is null)
            return;
        using var stream = File.OpenRead(file);
        if (stream.Length > 4_000_000)
            throw new BusinessException("Selecciona una imagen de hasta 4 MB.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Preview = DecodePhoto(bytes);
        Form.Photo = bytes;
    }

    private static BitmapSource DecodePhoto(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.DecodePixelWidth = 200;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception error) when (error is FileFormatException or NotSupportedException or ArgumentException)
        {
            throw new BusinessException("La foto seleccionada no es una imagen válida.");
        }
    }
}

public sealed class ProductForm
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "Unidad";
    public string Location { get; set; } = "";
    public string Cost { get; set; } = "0";
    public string Retail { get; set; } = "";
    public string Wholesale { get; set; } = "";
    public string Minimum { get; set; } = "0";
    public bool Active { get; set; } = true;
    public byte[]? Photo { get; set; }
}
