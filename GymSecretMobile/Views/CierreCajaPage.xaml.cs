using System;
using System.Linq;
using GymSecretMobile.Models;
using GymSecretMobile.Service;
using Microsoft.Extensions.DependencyInjection;

namespace GymSecretMobile.Views;

public partial class CierreCajaPage : ContentPage
{
    private readonly GymService _gymService;
    private readonly SyncService _syncService;
    private readonly List<string> _meses = new()
    {
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    public CierreCajaPage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _syncService = IPlatformApplication.Current.Services.GetService<SyncService>(); // Resolvemos
        pckMes.ItemsSource = _meses;
        pckMes.SelectedIndex = DateTime.Now.Month - 1;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (pckMes.SelectedIndex != -1)
        {
            RefrescarDatosActuales();
        }
    }

    private void OnMesSeleccionadoChanged(object sender, EventArgs e)
    {
        RefrescarDatosActuales();
    }

    private async void RefrescarDatosActuales()
    {
        if (pckMes.SelectedIndex == -1) return;

        // Limpiamos visualmente antes de cargar para evitar ver datos viejos mientras procesa
        lstTransacciones.ItemsSource = null;

        int mesSeleccionado = pckMes.SelectedIndex + 1;
        int anioActual = DateTime.Now.Year;
        lblMesSeleccionado.Text = $"{_meses[pckMes.SelectedIndex]} {anioActual}";

        try
        {
            // 1. Sincronizamos primero
            await _syncService.SincronizarTransaccionesAsync();
            await _syncService.SincronizarCierresAsync();

            // 2. Definimos fechas
            var inicio = new DateTime(anioActual, mesSeleccionado, 1);
            var fin = new DateTime(anioActual, mesSeleccionado, DateTime.DaysInMonth(anioActual, mesSeleccionado)).Date.AddDays(1).AddTicks(-1);

            // 3. Obtenemos datos limpios de la BD (que ya debería estar actualizada tras el Sync)
            var trans = await _gymService.GetTransaccionesPorRangoAsync(inicio, fin);

            // 4. Asignamos datos
            lstTransacciones.ItemsSource = trans;

            var ingresos = trans.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
            var egresos = trans.Where(t => t.Tipo == "Egreso" || t.Tipo == "Producto").Sum(t => t.Monto);
            var neto = ingresos - egresos;

            lblIngresos.Text = $"${ingresos:F2}";
            lblEgresos.Text = $"${egresos:F2}";
            lblNeto.Text = $"${neto:F2}";

            // 5. Generar cierre si aplica
            if (fin <= DateTime.Now)
            {
                await _gymService.GenerateAndSaveMonthlyClosureAsync(anioActual, mesSeleccionado, force: true);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "No se pudo actualizar: " + ex.Message, "OK");
        }
    }
    public async void OnTransaccionTapped(object sender, EventArgs e)
    {
        var elemento = sender as Element;
        var transaccion = elemento?.BindingContext as Transaccion;
        if (transaccion == null) return;
        bool confirmar = await DisplayAlertAsync(
            "Eliminar Registro",
            $"¿Deseas eliminar este {transaccion.Tipo} por un monto de ${transaccion.Monto:F2}?",
            "Sí, eliminar", "Cancelar");
        if (confirmar)
        {
            try
            {
                await _gymService.EliminarTransaccionAsync(transaccion);
                RefrescarDatosActuales();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo eliminar: {ex.Message}", "OK");
            }
        }
    }
}