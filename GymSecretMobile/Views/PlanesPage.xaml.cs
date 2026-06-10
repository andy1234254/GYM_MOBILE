using GymSecretMobile.Models;
using GymSecretMobile.Service;
using Microsoft.Extensions.DependencyInjection;

namespace GymSecretMobile.Views;

public partial class PlanesPage : ContentPage
{
    private readonly GymService _gymService;

    public PlanesPage()
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarPlanes();
    }
    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Navigation.PopToRootAsync();
            }
            catch
            {
            }
        });
        return true;
    }

    private async Task CargarPlanes()
    {
        try
        {
            lstPlanes.ItemsSource = null;
            var planes = await _gymService.ObtenerPlanesAsync();
            lstPlanes.ItemsSource = planes;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error de Carga", "No se pudieron obtener los planes: " + ex.Message, "OK");
        }
    }
    private async void OnGuardarPlanClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtNombrePlan.Text) ||
            string.IsNullOrWhiteSpace(txtPrecio.Text) ||
            string.IsNullOrWhiteSpace(txtDiasDuracion.Text))
        {
            await DisplayAlertAsync("Error", "Por favor completa todos los campos, incluyendo la duración en días.", "OK");
            return;
        }

        try
        {
            var nuevoPlan = new Plan
            {
                Nombre = txtNombrePlan.Text.Trim(),
                Precio = double.Parse(txtPrecio.Text),
                DiasDuracion = int.Parse(txtDiasDuracion.Text.Trim()),
                ActivaPareja = chkActivaPareja.IsChecked
            };
            await _gymService.GuardarPlanAsync(nuevoPlan);
            txtNombrePlan.Text = string.Empty;
            txtPrecio.Text = string.Empty;
            txtDiasDuracion.Text = string.Empty;
            chkActivaPareja.IsChecked = false;
            await CargarPlanes();
            await DisplayAlertAsync("Éxito", $"Plan '{nuevoPlan.Nombre}' guardado correctamente.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Error", "Asegúrate de ingresar valores numéricos válidos en precio y días.", "OK");
        }
    }

    private async void OnEliminarPlanClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not Plan plan) return;
        bool ok = await DisplayAlertAsync("Confirmar", $"¿Eliminar el plan '{plan.Nombre}'? Esta acción afectará a futuros registros.", "Sí", "No");
        if (!ok) return;

        try
        {
            await _gymService.EliminarPlanAsync(plan);
            await CargarPlanes();
            await DisplayAlertAsync("Listo", "Plan eliminado.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo eliminar: {ex.Message}", "OK");
        }
    }
}