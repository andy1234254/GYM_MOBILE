using GymSecretMobile.Models;
using GymSecretMobile.Service;

namespace GymSecretMobile.Views;

public partial class RegistraClientePage : ContentPage
{
    private readonly GymService _gymService;
    private readonly MediaService _mediaService;
    private string _rutaFotoActual;
    private List<Cliente> _todosLosClientes;
    private Cliente _parejaSeleccionada;
    private readonly string _nuevoClienteGuidHub;

    public RegistraClientePage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _mediaService = IPlatformApplication.Current.Services.GetService<MediaService>();
        _nuevoClienteGuidHub = Guid.NewGuid().ToString();
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

    private void OnPlanSeleccionado(object sender, EventArgs e)
    {
        if (pckPlan.SelectedItem is Plan planSeleccionado)
        {
            slPareja.IsVisible = planSeleccionado.ActivaPareja;
            lblPrecio.Text = $"Precio a cobrar: ${planSeleccionado.Precio:F2}";
            int diasDuracionPlan = planSeleccionado.DiasDuracion > 0 ? planSeleccionado.DiasDuracion : 30;
            dpFechaVencimiento.Date = DateTime.Now.AddDays(diasDuracionPlan);
            txtAsistenciasEditables.Text = diasDuracionPlan.ToString();
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
        var filtrados = _todosLosClientes
            .Where(c => c.NombreCompleto != null && c.NombreCompleto.ToLower().Contains(busqueda)).Take(5).ToList();
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

    private async void OnTomarFotoTapped(object sender, EventArgs e)
    {

        string rutaTemporal = await _mediaService.TomarFotoAsync();
        if (!string.IsNullOrEmpty(rutaTemporal) && File.Exists(rutaTemporal))
        {
            string nombreArchivo = $"{_nuevoClienteGuidHub}.jpg";
            string rutaPersistente = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
            try
            {
                File.Copy(rutaTemporal, rutaPersistente, true);
                _rutaFotoActual = rutaPersistente;
                imgPerfil.Source = ImageSource.FromFile(_rutaFotoActual);
                lblFotoPlaceholder.IsVisible = false;
                iconoImg.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo guardar la foto físicamente: {ex.Message}", "OK");
            }
        }
    }

    private async void OnCobrarGuardarClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtNombre.Text) || pckPlan.SelectedItem == null)
        {
            await DisplayAlertAsync("Error", "El nombre y el plan son obligatorios.", "OK");
            return;
        }
        if (!int.TryParse(txtAsistenciasEditables.Text, out int asistenciasTotales) || asistenciasTotales <= 0)
        {
            await DisplayAlertAsync("Error", "Ingresa un número válido de asistencias totales.", "OK");
            return;
        }
        try
        {
            var planSeleccionado = pckPlan.SelectedItem as Plan;
            Cliente parejaParaGuardar = slPareja.IsVisible ? _parejaSeleccionada : null;
            DateTime fechaVencimientoFinal = (DateTime)dpFechaVencimiento.Date;
            bool estaActivoInicial = fechaVencimientoFinal >= DateTime.Today && asistenciasTotales > 0;
            var nuevoCliente = new Cliente
            {
                GuidHub = _nuevoClienteGuidHub, 
                NombreCompleto = txtNombre.Text.Trim(),
                Telefono = txtTelefono.Text?.Trim() ?? "S/N",
                PlanId = planSeleccionado.Id,
                PlanGuidHub = planSeleccionado.GuidHub,
                FotoLocalPath = _rutaFotoActual,
                ParejaId = parejaParaGuardar?.Id,
                FechaRegistro = DateTime.Now,
                FechaUltimoPago = DateTime.Now,
                FechaVencimiento = fechaVencimientoFinal,
                AsistenciasTotales = asistenciasTotales,
                AsistenciasConsumidas = 0,
                EstaActivo = estaActivoInicial
            };
            await _gymService.GuardarClienteConTodoAsync(nuevoCliente, planSeleccionado, parejaParaGuardar);
            await DisplayAlertAsync("Éxito", $"¡Socio '{nuevoCliente.NombreCompleto}' registrado!", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Ocurrió un problema al guardar: {ex.Message}", "OK");
        }
    }
    private async void OnNuevoPlanClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new PlanesPage());
    }
}