using GymSecretMobile.Models;
using GymSecretMobile.Service;

namespace GymSecretMobile.Views;

public partial class RenovarSuscripcionPage : ContentPage
{
    private readonly GymService _gymService;
    private Cliente _cliente;
    private Cliente _parejaSeleccionada;
    private List<Cliente> _todosLosClientes;

    public RenovarSuscripcionPage(Cliente cliente)
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _cliente = cliente;
        lblClienteNombre.Text = _cliente.NombreCompleto;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarDatosIniciales();
    }

    private async Task CargarDatosIniciales()
    {
        try
        {
            var planes = await _gymService.ObtenerPlanesAsync();
            pckPlan.ItemsSource = planes;
            _todosLosClientes = await _gymService.ObtenerClientesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando datos: {ex.Message}");
        }
    }

    private void OnSearchParejaTextChanged(object sender, TextChangedEventArgs e)
    {
        string busqueda = e.NewTextValue?.ToLower();

        if (string.IsNullOrWhiteSpace(busqueda))
        {
            brdResultados.IsVisible = false;
            return;
        }
        var filtrados = _todosLosClientes.Where(c => c.NombreCompleto != null && c.NombreCompleto.ToLower().Contains(busqueda)).Take(5).ToList();
        if (filtrados.Any())
        {
            lstResultadosPareja.ItemsSource = filtrados;
            brdResultados.IsVisible = true;
        }
        else
        {
            brdResultados.IsVisible = false;
        }
    }

    private void OnParejaSeleccionadaDeLista(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Cliente seleccionado)
        {
            _parejaSeleccionada = seleccionado;
            lblParejaSeleccionada.Text = $"Seleccionado: {seleccionado.NombreCompleto}";
            brdResultados.IsVisible = false;
            searchPareja.Text = string.Empty;
        }
    }
    private void OnPlanSeleccionado(object sender, EventArgs e)
    {
        if (pckPlan.SelectedItem is Plan plan)
        {
            slPareja.IsVisible = plan.ActivaPareja;
            lblPrecio.Text = $"Precio a cobrar: ${plan.Precio:F2}";
            int diasDuracionPlan = plan.DiasDuracion > 0 ? plan.DiasDuracion : 30;
            dpFechaVencimiento.Date = DateTime.Today.AddDays(diasDuracionPlan);
            txtAsistenciasEditables.Text = diasDuracionPlan.ToString();
        }
    }

    private async void OnConfirmarRenovacionClicked(object sender, EventArgs e)
    {
        if (pckPlan.SelectedItem is Plan planSeleccionado)
        {
            if (!int.TryParse(txtAsistenciasEditables.Text, out int asistenciasTotales) || asistenciasTotales <= 0) // Validamos que las asistencias sean un número válido
            {
                await DisplayAlertAsync("Error", "Ingresa un número válido de asistencias totales.", "OK");
                return;
            }
            try
            {
                Cliente parejaParaGuardar = null;

                if (slPareja.IsVisible)
                {
                    if (_parejaSeleccionada != null)
                    {
                        parejaParaGuardar = _parejaSeleccionada;
                    }
                    else if (_cliente.ParejaId != null)
                    {
                        parejaParaGuardar = _todosLosClientes.FirstOrDefault(c => c.Id == _cliente.ParejaId);
                    }
                }

                DateTime fechaVencimientoFinal = (DateTime)dpFechaVencimiento.Date;
                _cliente.PlanId = planSeleccionado.Id;
                _cliente.ParejaId = parejaParaGuardar?.Id;
                _cliente.FechaUltimoPago = DateTime.Now;
                _cliente.FechaVencimiento = (DateTime)fechaVencimientoFinal;
                _cliente.AsistenciasTotales = asistenciasTotales;
                _cliente.AsistenciasConsumidas = 0;
                _cliente.EstaActivo = fechaVencimientoFinal >= DateTime.Today && asistenciasTotales > 0;
                await _gymService.RenovarSuscripcionAsync(_cliente, planSeleccionado, parejaParaGuardar);
                await DisplayAlertAsync("Éxito", $"Suscripción de '{_cliente.NombreCompleto}' renovada correctamente.", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo renovar: {ex.Message}", "OK");
            }
        }
        else
        {
            await DisplayAlertAsync("Atención", "Por favor seleccione un plan.", "OK");
        }
    }
}