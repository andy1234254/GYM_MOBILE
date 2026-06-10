using GymSecretMobile.Models;
using GymSecretMobile.Service;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Graphics;
using System;

namespace GymSecretMobile.Views;

public partial class VerClientesPage : ContentPage
{
    private readonly GymService _gymService;
    private readonly SyncService _syncService;
    private List<Cliente> _todosLosClientes = new();
    private enum FilterMode { Recientes, Activos, Inactivos }
    private FilterMode _filter = FilterMode.Recientes;
    private bool _estaActualizando = false;

    public VerClientesPage(GymService gymService, SyncService syncService)
    {
        InitializeComponent();
        _gymService = gymService;
        _syncService = syncService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var cultura = new System.Globalization.CultureInfo("es-ES");
        string diaDeHoy = DateTime.Now.ToString("dddd", cultura);
        lblDiaSemana.Text = cultura.TextInfo.ToTitleCase(diaDeHoy);
        lblFechaLarga.Text = DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", cultura);
        try
        {
            await CargarListaClientes();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error en Release", $"Detalle: {ex.Message}\nStack: {ex.StackTrace}", "OK");
        }
    }

    private async Task CargarListaClientes()
    {
        try
        {
            _todosLosClientes = await _gymService.GetClientesConPlanesAsync();
            ActualizarInterfaz();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnAgregarClienteClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RegistraClientePage());
    }

    private void ActualizarInterfaz()
    {
        var filtered = FiltrarYLimitar(_todosLosClientes);
        lstClientes.ItemsSource = filtered;
        ActualizarEstadoBotonesFiltro();
    }

    private IEnumerable<Cliente> FiltrarYLimitar(IEnumerable<Cliente> source)
    {
        IEnumerable<Cliente> query = _filter switch
        {
            FilterMode.Recientes => source.OrderByDescending(c => c.Id),
            FilterMode.Activos => source.Where(c => c.EstaActivo),
            FilterMode.Inactivos => source.Where(c => !c.EstaActivo),
            _ => source
        };

        return query.Take(10).ToList();
    }

    // --- EVENTOS DE FILTRO ---
    private void OnFiltroRecientesClicked(object sender, EventArgs e) { _filter = FilterMode.Recientes; ActualizarInterfaz(); }
    private void OnFiltroActivosClicked(object sender, EventArgs e) { _filter = FilterMode.Activos; ActualizarInterfaz(); }
    private void OnFiltroInactivosClicked(object sender, EventArgs e) { _filter = FilterMode.Inactivos; ActualizarInterfaz(); }
    private async void OnRegistrarAsistenciaClicked(object sender, EventArgs e)
    {
        var btn = (Button)sender;
        var cliente = (Cliente)btn.CommandParameter;
        var resultado = await _gymService.RegistrarAsistenciaConLogAsync(cliente.Id);
        if (resultado.Exito)
        {
            cliente.AsistenciasConsumidas++;
            if (cliente.AsistenciasConsumidas >= cliente.AsistenciasTotales) cliente.EstaActivo = false;
            await DisplayAlertAsync("Éxito", resultado.Mensaje, "OK");
            ActualizarInterfaz();
        }
        else
        {
            await DisplayAlertAsync("Atención", resultado.Mensaje, "OK");
        }
    }
    private async void OnClienteTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Cliente clienteSeleccionado)
        {
            try
            {
                await Navigation.PushAsync(new PerfilClientePage(clienteSeleccionado));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error de navegación: {ex.Message}");
            }
        }
    }
    private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            ActualizarInterfaz();
            return;
        }
        var busqueda = _todosLosClientes.Where(c => c.NombreCompleto.ToLower().Contains(e.NewTextValue.ToLower())).Take(10);
        lstClientes.ItemsSource = busqueda;
    }

    private void ActualizarEstadoBotonesFiltro()
    {
        var fondoSeleccionado = Colors.Black;
        var textoSeleccionado = Color.FromArgb("#FFFFFF");
        var fondoNormal = Color.FromArgb("#80555555");
        var textoNormal = Colors.White;
        borderRecientes.BackgroundColor = _filter == FilterMode.Recientes ? fondoSeleccionado : fondoNormal;
        lblRecientes.TextColor = _filter == FilterMode.Recientes ? textoSeleccionado : textoNormal;
        borderActivos.BackgroundColor = _filter == FilterMode.Activos ? fondoSeleccionado : fondoNormal;
        lblActivos.TextColor = _filter == FilterMode.Activos ? textoSeleccionado : textoNormal;
        borderInactivos.BackgroundColor = _filter == FilterMode.Inactivos ? fondoSeleccionado : fondoNormal;
        lblInactivos.TextColor = _filter == FilterMode.Inactivos ? textoSeleccionado : textoNormal;
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
            await _syncService.SincronizarClientesAsync();
            await CargarListaClientes();
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
}