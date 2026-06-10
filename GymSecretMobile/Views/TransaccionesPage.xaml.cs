using GymSecretMobile.Models;
using GymSecretMobile.Service;
using System.Diagnostics;

namespace GymSecretMobile.Views;

public partial class TransaccionesPage : ContentPage
{
    private readonly GymService _gymService;

    public TransaccionesPage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ActualizarListaProductos();
    }

    private void OnTipoChanged(object sender, EventArgs e)
    {
        if (pckTipo.SelectedIndex == -1) return;
        string seleccionado = pckTipo.SelectedItem.ToString();
        if (seleccionado == "Producto")
        {
            stackProducto.IsVisible = true;
            stackNormal.IsVisible = false;
        }
        else
        {
            stackProducto.IsVisible = false;
            stackNormal.IsVisible = true;
            gridFotoProducto.IsVisible = false;
            imgProductoPreview.Source = null;
        }
    }
    private void OnProductoSeleccionadoChanged(object sender, EventArgs e)
    {
        var productoSeleccionado = pckSeleccionarProducto.SelectedItem as Producto;
        if (productoSeleccionado == null)
        {
            gridFotoProducto.IsVisible = false;
            imgProductoPreview.Source = null;
            return;
        }

        gridFotoProducto.IsVisible = true;
        if (!string.IsNullOrEmpty(productoSeleccionado.FotoLocalPath) && File.Exists(productoSeleccionado.FotoLocalPath))
        {
            imgProductoPreview.Source = ImageSource.FromFile(productoSeleccionado.FotoLocalPath);
        }
        else if (!string.IsNullOrEmpty(productoSeleccionado.FotoUrl))
        {
            imgProductoPreview.Source = ImageSource.FromUri(new Uri(productoSeleccionado.FotoUrl));
        }
        else
        {
            imgProductoPreview.Source = null;
        }
    }
    private async void OnActualizarFotoProductoClicked(object sender, EventArgs e)
    {
        var productoSeleccionado = pckSeleccionarProducto.SelectedItem as Producto;
        if (productoSeleccionado == null) return;
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

                if (photo != null)
                {
                    string rutaViejaABorrar = productoSeleccionado.FotoLocalPath;
                    string nuevoNombreArchivo = $"{productoSeleccionado.GuidHub}_{DateTime.Now.Ticks}.jpg";
                    string localFilePath = Path.Combine(FileSystem.AppDataDirectory, nuevoNombreArchivo);
                    using (Stream sourceStream = await photo.OpenReadAsync())
                    using (FileStream localFileStream = File.Create(localFilePath))
                    {
                        await sourceStream.CopyToAsync(localFileStream);
                    }
                    imgProductoPreview.Source = null;
                    productoSeleccionado.FotoLocalPath = localFilePath;
                    productoSeleccionado.FotoUrl = string.Empty;
                    imgProductoPreview.Source = ImageSource.FromFile(localFilePath);
                    await _gymService.ActualizarFotoProductoAsync(productoSeleccionado);
                    if (!string.IsNullOrEmpty(rutaViejaABorrar) && File.Exists(rutaViejaABorrar) && rutaViejaABorrar != localFilePath)
                    {
                        try
                        {
                            File.Delete(rutaViejaABorrar);
                        }
                        catch
                        {
                            Debug.WriteLine("--> [Transacciones] El archivo físico anterior del producto se eliminará en el próximo ciclo del GC.");
                        }
                    }

                    await DisplayAlertAsync("Éxito", $"La imagen de '{productoSeleccionado.Nombre}' fue actualizada localmente y puesta en cola para subirse a la nube.", "OK");
                    int indexGuardado = pckSeleccionarProducto.SelectedIndex;
                    await ActualizarListaProductos();
                    pckSeleccionarProducto.SelectedIndex = indexGuardado;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo actualizar la foto del producto: {ex.Message}", "OK");
        }
    }

    private async void OnGuardarTransaccionClicked(object sender, EventArgs e)
    {
        if (pckTipo.SelectedIndex == -1)
        {
            await DisplayAlertAsync("Error", "Selecciona un tipo de movimiento.", "OK");
            return;
        }
        string tipo = pckTipo.SelectedItem.ToString();
        try
        {
            if (tipo == "Producto")
            {
                var productoSeleccionado = pckSeleccionarProducto.SelectedItem as Producto;
                if (productoSeleccionado == null || string.IsNullOrEmpty(txtCantidadIngresa.Text) || string.IsNullOrEmpty(txtTotalCompra.Text))
                {
                    await DisplayAlertAsync("Error", "Completa todos los campos del producto.", "OK");
                    return;
                }
                int cantidad = int.Parse(txtCantidadIngresa.Text);
                double total = double.Parse(txtTotalCompra.Text);
                await _gymService.ComprarProductoInventarioAsync(productoSeleccionado, cantidad, total);
                await DisplayAlertAsync("Éxito", "Inventario actualizado y registrado en caja.", "OK");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtConcepto.Text) || string.IsNullOrWhiteSpace(txtMonto.Text))
                {
                    await DisplayAlertAsync("Error", "Completa concepto y monto.", "OK");
                    return;
                }

                var nuevaTransaccion = new Transaccion
                {
                    Tipo = tipo,
                    Concepto = txtConcepto.Text.Trim(),
                    Monto = double.Parse(txtMonto.Text),
                    Fecha = DateTime.Now
                };
                await _gymService.GuardarTransaccionAsync(nuevaTransaccion);
                await DisplayAlertAsync("Éxito", $"{tipo} registrado correctamente.", "OK");
            }
            LimpiarCampos();
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Error", "Asegúrate de ingresar valores numéricos válidos.", "OK");
        }
    }

    private void LimpiarCampos()
    {
        pckTipo.SelectedIndex = -1;
        txtConcepto.Text = "";
        txtMonto.Text = "";
        txtCantidadIngresa.Text = "";
        txtTotalCompra.Text = "";
        pckSeleccionarProducto.SelectedIndex = -1;
        stackNormal.IsVisible = false;
        stackProducto.IsVisible = false;
        gridFotoProducto.IsVisible = false;
        imgProductoPreview.Source = null;
    }

    private async void OnNuevoProductoClicked(object sender, EventArgs e)
    {
        var nuevoProductoPage = new NuevoProductoPage();
        nuevoProductoPage.Unfocused += async (s, args) => await ActualizarListaProductos();
        await Navigation.PushModalAsync(nuevoProductoPage);
    }

    private async Task ActualizarListaProductos()
    {
        var productos = await _gymService.GetProductosAsync();
        pckSeleccionarProducto.ItemsSource = productos;
    }
}