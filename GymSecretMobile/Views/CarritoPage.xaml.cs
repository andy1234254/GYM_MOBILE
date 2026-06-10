using GymSecretMobile.Models;
using GymSecretMobile.Service;
using System.Collections.ObjectModel;

namespace GymSecretMobile.Views;

public partial class CarritoPage : ContentPage
{
    private readonly GymService _gymService;
    public ObservableCollection<DetalleVenta> CarritoItems { get; set; }
    public CarritoPage(List<DetalleVenta> itemsIniciales)
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        CarritoItems = new ObservableCollection<DetalleVenta>(itemsIniciales);
        lstCarrito.ItemsSource = CarritoItems;
        ActualizarTotal();
    }
    private void ActualizarTotal()
    {
        double total = CarritoItems.Sum(x => x.Subtotal);
        lblTotal.Text = $"Total: ${total:F2}";
    }
    private void OnEliminarProductoClicked(object sender, EventArgs e)
    {
        var item = (sender as Button).CommandParameter as DetalleVenta;
        if (item != null)
        {
            CarritoItems.Remove(item);
            ActualizarTotal();
        }
    }
    private void OnAumentarClicked(object sender, EventArgs e)
    {
        var item = (sender as Button).CommandParameter as DetalleVenta;
        if (item == null) return;

        if (item.CantidadVendida < item.StockDisponible)
        {
            item.CantidadVendida++;
            ActualizarTotal();
        }
        else
        {
            DisplayAlertAsync("Límite", "No hay más unidades disponibles en stock.", "OK");
        }
    }

    private void OnDisminuirClicked(object sender, EventArgs e)
    {
        var item = (sender as Button).CommandParameter as DetalleVenta;
        if (item == null) return;

        if (item.CantidadVendida > 1)
        {
            item.CantidadVendida--;
            ActualizarTotal();
        }
        // Si llega a 1, no restamos más, o podrías llamar a OnEliminarProductoClicked si prefieres borrarlo.
    }
    private async void OnCobrarClicked(object sender, EventArgs e)
    {
        if (CarritoItems.Count == 0)
        {
            await DisplayAlertAsync("Carrito Vacío", "No hay productos para cobrar.", "OK");
            return;
        }

        bool confirmar = await DisplayAlertAsync("Confirmar", "¿Deseas finalizar la venta?", "Sí", "No");
        if (!confirmar) return;

        try
        {
            await _gymService.ProcesarVentaCarritoAsync(CarritoItems.ToList());
            await DisplayAlertAsync("Éxito", "Venta registrada y stock actualizado.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "No se pudo procesar la venta: " + ex.Message, "OK");
        }
    }

}