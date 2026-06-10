using GymSecretMobile.Models;
using GymSecretMobile.Service;

namespace GymSecretMobile.Views;

public partial class NuevoProductoPage : ContentPage
{
    private readonly GymService _gymService;
    private readonly MediaService _mediaService;
    private string _rutaFotoActual;
    public NuevoProductoPage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _mediaService = IPlatformApplication.Current.Services.GetService<MediaService>(); 
    }

    private async void OnTomarFotoTapped(object sender, EventArgs e)
    {
        string rutaLocal = await _mediaService.TomarFotoAsync();
        if (!string.IsNullOrEmpty(rutaLocal))
        {
            _rutaFotoActual = rutaLocal;
            imgProducto.Source = ImageSource.FromFile(rutaLocal);
            lblFotoPlaceholder.IsVisible = false;
            iconoImg.IsVisible = false;
        }
    }
    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtNombre.Text) || string.IsNullOrWhiteSpace(txtPrecio.Text))
        {
            await DisplayAlertAsync("Error", "El nombre y el precio son obligatorios.", "OK");
            return;
        }
        try
        {
            var nuevoProd = new Producto
            {
                Nombre = txtNombre.Text.Trim(),
                PrecioVenta = double.Parse(txtPrecio.Text),
                CantidadDisponible = 0, 
                FotoLocalPath = _rutaFotoActual                
            };
            await _gymService.InsertarProductoAsync(nuevoProd);
            await Navigation.PopModalAsync();
        }
        catch (FormatException)
        {
            await DisplayAlertAsync("Error", "Asegúrate de que el precio sea un valor numérico válido.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo guardar el producto: {ex.Message}", "OK");
        }
    }
    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}