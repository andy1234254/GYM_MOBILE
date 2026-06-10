using GymSecretMobile.Models;
using GymSecretMobile.Service;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GymSecretMobile.Views;

public partial class StockPage : ContentPage
{
    private readonly GymService _gymService;
    private readonly SyncService _syncService;
    private List<Producto> _todosLosProductos = new();
    private List<DetalleVenta> _carritoTemporal = new();
    private bool _estaActualizando = false;

    public StockPage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _syncService = IPlatformApplication.Current.Services.GetService<SyncService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarStock();
    }

    private async Task CargarStock()
    {
        _todosLosProductos = await _gymService.GetProductosAsync();
        lstStock.ItemsSource = _todosLosProductos;
    }
    private async void OnActualizarClicked(object sender, EventArgs e)
    {
        if (_estaActualizando) return;

        try
        {
            _estaActualizando = true;
            await borderActualizar.ScaleTo(0.9, 100);
            await borderActualizar.ScaleTo(1.0, 100);
            borderActualizar.BackgroundColor = Colors.Gray;
            await _syncService.SincronizarProductosAsync();
            await CargarStock();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error al actualizar", ex.Message, "OK");
        }
        finally
        {
            _estaActualizando = false;
            borderActualizar.BackgroundColor = Color.FromArgb("#2196F3");
        }
    }
    private async void OnAgregarAlCarritoClicked(object sender, EventArgs e)
    {
        var producto = (sender as Button).CommandParameter as Producto;

        if (producto.CantidadDisponible <= 0)
        {
            await DisplayAlertAsync("Sin Stock", $"Lo sentimos, ya no queda {producto.Nombre} disponible.", "OK");
            return;
        }

        // 1. CAPTURA EL STOCK TOTAL ANTES DE DESCONTAR
        int stockTotal = producto.CantidadDisponible;

        // 2. DESCUENTA
        producto.CantidadDisponible--;

        var existente = _carritoTemporal.FirstOrDefault(x => x.ProductoId == producto.Id);
        if (existente != null)
        {
            existente.CantidadVendida++;
            // No necesitamos actualizar Subtotal ni StockDisponible aquí porque el modelo es reactivo
        }
        else
        {
            _carritoTemporal.Add(new DetalleVenta
            {
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                PrecioUnitario = producto.PrecioVenta,
                CantidadVendida = 1,
                // 3. PASA EL STOCK TOTAL ORIGINAL
                StockDisponible = stockTotal,
                FotoRuta = producto.FotoLocalPath,
                ImagenMostrar = producto.ImagenMostrar
            });
        }
    }

    private async void OnVerCarritoClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CarritoPage(_carritoTemporal));
        _carritoTemporal = new List<DetalleVenta>();
    }
    private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        string textoBusqueda = e.NewTextValue;
        if (string.IsNullOrWhiteSpace(textoBusqueda))
        {
            lstStock.ItemsSource = _todosLosProductos;
        }
        else
        {
            lstStock.ItemsSource = _todosLosProductos
                .Where(p => p.Nombre.ToLower().Contains(textoBusqueda.ToLower()))
                .ToList();
        }

    }
    private async void OnProductoCardTapped(object sender, EventArgs e)
    {
        var tarjeta = sender as Border;
        var producto = tarjeta?.BindingContext as Producto;
        if (producto == null) return;
        bool confirmar = await DisplayAlertAsync(
            "Eliminar Producto",
            $"¿Estás seguro de que deseas eliminar \"{producto.Nombre}\" del inventario?",
            "Sí, eliminar",
            "Cancelar");
        if (!confirmar) return;

        try
        {
            await _gymService.EliminarProductoAsync(producto);
            await DisplayAlertAsync("Éxito", "Producto eliminado correctamente.", "OK");
            await CargarStock();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo procesar la eliminación: {ex.Message}", "OK");
        }
    }
}