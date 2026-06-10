using GymSecretMobile.Models;
using GymSecretMobile.Service;
using System.Collections.ObjectModel;

namespace GymSecretMobile.Views;

public partial class PerfilClientePage : ContentPage
{
    private readonly GymService _gymService;
    private Cliente _cliente;
    private bool _hayEdicionPendiente = false;
    public PerfilClientePage(Cliente cliente)
    {
        InitializeComponent();
        _gymService = IPlatformApplication.Current.Services.GetService<GymService>();
        _cliente = cliente;
        ConfigurarPickerMeses();
    }

    private void ConfigurarPickerMeses()
    {
        pckMes.ItemsSource = new List<string> {
            "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
            "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
        };
        pckMes.SelectedIndex = DateTime.Now.Month - 1;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hayEdicionPendiente) return;

        var actualizado = await _gymService.GetClienteByIdAsync(_cliente.Id);
        if (actualizado != null)
        {
            var planes = await _gymService.ObtenerPlanesAsync();
            actualizado.Plan = planes.FirstOrDefault(p => p.GuidHub == actualizado.PlanGuidHub);
            _cliente = actualizado;
        }
        await CargarDatosPerfil();
        await CargarHistorialActual();
    }

    private async Task CargarHistorialActual()
    {
        if (pckMes.SelectedIndex == -1) return;
        try
        {
            int mesSeleccionado = pckMes.SelectedIndex + 1;
            int anioAConsultar = DateTime.Now.Year;
            if (mesSeleccionado > DateTime.Now.Month)
            {
                anioAConsultar--;
            }

            var asistenciasLista = await _gymService.GetAsistenciasPorMesAsync(_cliente.Id, mesSeleccionado, anioAConsultar);
            var asistenciasObservables = new ObservableCollection<Asistencia>(asistenciasLista);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                lstHistorial.ItemsSource = asistenciasObservables; 
                lblResumenAsistencia.Text = $"Total, días asistidos en el mes: {asistenciasLista.Count}";
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "No se pudo cargar el historial: " + ex.Message, "OK");
        }
    }

    private async void OnMesSeleccionado(object sender, EventArgs e)
    {
        await CargarHistorialActual();
    }

    private Asistencia _asistenciaSeleccionada;

    // SwipeView -> Editar
    private void OnEditarAsistenciaClicked(object sender, EventArgs e)
    {
        _asistenciaSeleccionada = (sender as SwipeItem)?.BindingContext as Asistencia;
        if (_asistenciaSeleccionada == null) return;

        // Cargamos los datos actuales en los controles
        editDatePicker.Date = _asistenciaSeleccionada.FechaHora;
        editTimePicker.Time = _asistenciaSeleccionada.FechaHora.TimeOfDay;

        // Mostramos el editor
        gridEditor.IsVisible = true;
    }

    // Botón Guardar del Editor
    private async void OnGuardarEdicionAsistenciaClicked(object sender, EventArgs e)
    {
        if (_asistenciaSeleccionada == null) return;

        // Combinamos fecha y hora seleccionada (editDatePicker.Date y editTimePicker.Time son nullable)
        DateTime fecha = editDatePicker.Date ?? DateTime.Now;
        TimeSpan hora = editTimePicker.Time ?? TimeSpan.Zero;
        DateTime nuevaFechaHora = fecha.Date + hora;

        _asistenciaSeleccionada.FechaHora = nuevaFechaHora;

        // Usamos el método que ya creamos en GymService
        await _gymService.ActualizarAsistenciaAsync(_asistenciaSeleccionada);

        gridEditor.IsVisible = false; // Cerramos editor
        await CargarHistorialActual(); // Refrescamos lista inmediatamente
    }

    // SwipeView -> Eliminar (SoftDelete)
    private async void OnEliminarAsistenciaClicked(object sender, EventArgs e)
    {
        var item = (sender as SwipeItem)?.BindingContext as Asistencia;
        if (item == null) return;

        bool confirm = await DisplayAlertAsync("Eliminar", "¿Deseas eliminar esta asistencia?", "Sí", "No");
        if (confirm)
        {
            await _gymService.SoftDeleteAsistenciaAsync(item);
            await CargarHistorialActual(); // Se refresca y, como está el filtro !IsDeleted, desaparece sola
        }
    }

    private void OnCancelarEdicionClicked(object sender, EventArgs e) => gridEditor.IsVisible = false;
    private async Task CargarDatosPerfil()
    {
        lblNombre.Text = _cliente.NombreCompleto;
        lblTelefono.Text = $"Teléfono: {_cliente.Telefono}";
        lblPlan.Text = $"Plan actual: {_cliente.Plan?.Nombre ?? "S/N"}";
        lblEstado.Text = $"Estado: {(_cliente.EstaActivo ? "Activo" : "Inactivo")}";
        lblDias.Text = $"Días restantes: {_cliente.AsistenciasTotales - _cliente.AsistenciasConsumidas}";
        dpRegistro.Date = _cliente.FechaRegistro;
        dpUltimoPago.Date = _cliente.FechaUltimoPago;
        dpVencimiento.Date = _cliente.FechaVencimiento;
        if (!string.IsNullOrEmpty(_cliente.FotoLocalPath) && File.Exists(_cliente.FotoLocalPath))
        {
            imgFoto.Source = ImageSource.FromFile(_cliente.FotoLocalPath);
        }
        if (_cliente.ParejaId.HasValue)
        {
            var pareja = await _gymService.GetClienteByIdAsync(_cliente.ParejaId.Value);
            lblPareja.Text = $"Pareja: {pareja?.NombreCompleto ?? "No encontrado"}";
        }
        else
        {
            lblPareja.Text = "Pareja: -";
        }
    }

    private async void OnRenovarClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RenovarSuscripcionPage(_cliente));
    }

    private async void OnActualizarFotoClicked(object sender, EventArgs e)
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    string rutaViejaABorrar = _cliente.FotoLocalPath;
                    string nuevoNombreArchivo = $"{_cliente.GuidHub}_{DateTime.Now.Ticks}.jpg";
                    string localFilePath = Path.Combine(FileSystem.AppDataDirectory, nuevoNombreArchivo);
                    using (Stream sourceStream = await photo.OpenReadAsync())
                    using (FileStream localFileStream = File.Create(localFilePath))
                    {
                        await sourceStream.CopyToAsync(localFileStream);
                    }
                    imgFoto.Source = null;
                    _cliente.FotoLocalPath = localFilePath;
                    _cliente.FotoUrl = string.Empty;
                    imgFoto.Source = ImageSource.FromFile(localFilePath);
                    await _gymService.ActualizarClienteAsync(_cliente);
                    if (!string.IsNullOrEmpty(rutaViejaABorrar) && File.Exists(rutaViejaABorrar) && rutaViejaABorrar != localFilePath)
                    {
                        try
                        {
                            File.Delete(rutaViejaABorrar);
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine("--> [Perfil] No se pudo borrar el archivo viejo temporalmente debido al recolector de basura.");
                        }
                    }
                    await DisplayAlertAsync("Éxito", "La foto de perfil ha sido actualizada localmente y puesta en cola de sincronización.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo actualizar la foto: {ex.Message}", "OK");
        }
    }
    private void OnImagenPerfilTapped(object sender, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_cliente.FotoLocalPath) && File.Exists(_cliente.FotoLocalPath))
        {
            imgPantallaCompleta.Source = ImageSource.FromFile(_cliente.FotoLocalPath);
            grdPantallaCompleta.IsVisible = true;
        }
    }

    private void OnCerrarPantallaCompletaClicked(object sender, EventArgs e)
    {
        grdPantallaCompleta.IsVisible = false;
    }
    private async void OnDesactivarClienteClicked(object sender, EventArgs e)
    {
        if (!_cliente.EstaActivo)
        {
            await DisplayAlertAsync("Atención", "Este cliente ya se encuentra inactivo.", "OK");
            return;
        }

        bool confirmar = await DisplayAlertAsync(
            "Desactivar Cliente",
            $"¿Estás seguro de que deseas marcar a {_cliente.NombreCompleto} como Inactivo?\nSe ignorarán los días o vigencia restante.",
            "Sí, desactivar",
            "Cancelar");
        if (!confirmar) return;

        try
        {
            _cliente.EstaActivo = false;
            await _gymService.ActualizarClienteAsync(_cliente);
            await CargarDatosPerfil();
            await DisplayAlertAsync("Éxito", "El cliente ha sido desactivado correctamente.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo cambiar el estado: {ex.Message}", "OK");
        }
    }
    private async void OnEliminarClienteClicked(object sender, EventArgs e)
    {
        bool confirmar = await DisplayAlertAsync(
            "Eliminar Cliente",
            $"¿Estás seguro de que deseas eliminar a {_cliente.NombreCompleto}?\nEsta acción lo quitará de todas las listas de la interfaz.",
            "Sí, eliminar",
            "Cancelar");

        if (!confirmar) return;
        try
        {
            await _gymService.EliminarClienteLogicoAsync(_cliente);
            await DisplayAlertAsync("Éxito", "Cliente eliminado correctamente del inventario de usuarios.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo eliminar al cliente: {ex.Message}", "OK");
        }
    }
    // Activa el botón de guardar y actualiza la vista visual
    private void ActivarBotonGuardar()
    {
        _hayEdicionPendiente = true;
        btnGuardarEdicion.IsVisible = true;
        _cliente.EstaActivo = _cliente.FechaVencimiento.Date >= DateTime.Now.Date && _cliente.AsistenciasTotales > _cliente.AsistenciasConsumidas;

        // Refrescamos los labels visuales inmediatamente sin consultar la BD
        lblNombre.Text = _cliente.NombreCompleto;
        lblTelefono.Text = $"Teléfono: {_cliente.Telefono}";
        lblPlan.Text = $"Plan actual: {_cliente.Plan?.Nombre ?? "S/N"}";
        lblEstado.Text = $"Estado: {(_cliente.EstaActivo ? "Activo" : "Inactivo")}";
        lblDias.Text = $"Días restantes: {_cliente.AsistenciasTotales - _cliente.AsistenciasConsumidas}";
    }

    // --- EDICIÓN DE TEXTOS Y NÚMEROS ---
    private async void OnEditNombreClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Editar Nombre", "Ingresa el nuevo nombre:", initialValue: _cliente.NombreCompleto);
        if (!string.IsNullOrWhiteSpace(result) && result != _cliente.NombreCompleto)
        {
            _cliente.NombreCompleto = result.Trim();
            ActivarBotonGuardar();
        }
    }

    private async void OnEditTelefonoClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Editar Teléfono", "Ingresa el nuevo teléfono:", initialValue: _cliente.Telefono, keyboard: Keyboard.Telephone);
        if (!string.IsNullOrWhiteSpace(result) && result != _cliente.Telefono)
        {
            _cliente.Telefono = result.Trim();
            ActivarBotonGuardar();
        }
    }

    private async void OnEditDiasClicked(object sender, EventArgs e)
    {
        int diasRestantesActuales = _cliente.AsistenciasTotales - _cliente.AsistenciasConsumidas;

        string result = await DisplayPromptAsync(
            "Editar Días Restantes",
            "Ingresa exactamente cuántos días le quedan a este cliente:",
            initialValue: diasRestantesActuales.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(result)) return;

        if (int.TryParse(result.Trim(), out int nuevosDiasRestantes) && nuevosDiasRestantes >= 0)
        {
            // Guardamos directamente lo que el admin ingresó.
            // AsistenciasTotales = 10 → Firestore/SQLite mostrará 10. Sin sorpresas.
            _cliente.AsistenciasTotales = nuevosDiasRestantes;
            _cliente.AsistenciasConsumidas = 0;
            ActivarBotonGuardar();
        }
    }

    // --- EDICIÓN DE RELACIONES (PLAN Y PAREJA) ---
    private async void OnEditPlanClicked(object sender, EventArgs e)
    {
        var planes = await _gymService.ObtenerPlanesAsync();
        var nombres = planes.Select(p => p.Nombre).ToArray();
        string seleccion = await DisplayActionSheet("Cambiar Plan", "Cancelar", null, nombres);

        if (!string.IsNullOrEmpty(seleccion) && seleccion != "Cancelar")
        {
            var planElegido = planes.FirstOrDefault(p => p.Nombre == seleccion);
            if (planElegido != null)
            {
                _cliente.PlanId = planElegido.Id;
                _cliente.PlanGuidHub = planElegido.GuidHub;
                _cliente.Plan = planElegido;
                ActivarBotonGuardar();
            }
        }
    }

    private async void OnEditParejaClicked(object sender, EventArgs e)
    {
        var clientes = await _gymService.ObtenerClientesAsync();
        var nombres = clientes.Where(c => c.Id != _cliente.Id).Select(c => c.NombreCompleto).ToList();
        nombres.Insert(0, "Ninguna (Quitar Pareja)");

        string seleccion = await DisplayActionSheet("Asignar Pareja", "Cancelar", null, nombres.ToArray());

        if (!string.IsNullOrEmpty(seleccion) && seleccion != "Cancelar")
        {
            if (seleccion == "Ninguna (Quitar Pareja)")
            {
                _cliente.ParejaId = null;
                lblPareja.Text = "Pareja: -";
            }
            else
            {
                var parejaElegida = clientes.FirstOrDefault(c => c.NombreCompleto == seleccion);
                if (parejaElegida != null)
                {
                    _cliente.ParejaId = parejaElegida.Id;
                    lblPareja.Text = $"Pareja: {parejaElegida.NombreCompleto}";
                }
            }
            ActivarBotonGuardar();
        }
    }

    private void OnRegistroSelected(object sender, DateChangedEventArgs e)
    {
        // Solo actualizamos si realmente cambió para evitar llamadas innecesarias
        if (!e.NewDate.HasValue) return;

        if (_cliente.FechaRegistro.Date != e.NewDate.Value.Date)
        {
            _cliente.FechaRegistro = e.NewDate.Value.Date;
            ActivarBotonGuardar();
        }
    }

    private void OnUltimoPagoSelected(object sender, DateChangedEventArgs e)
    {
        if (!e.NewDate.HasValue) return;

        if (_cliente.FechaUltimoPago.Date != e.NewDate.Value.Date)
        {
            _cliente.FechaUltimoPago = e.NewDate.Value.Date;
            ActivarBotonGuardar();
        }
    }

    private void OnVencimientoSelected(object sender, DateChangedEventArgs e)
    {
        if (!e.NewDate.HasValue) return;

        if (_cliente.FechaVencimiento.Date != e.NewDate.Value.Date)
        {
            _cliente.FechaVencimiento = e.NewDate.Value.Date;
            ActivarBotonGuardar();
        }
    }

    // --- GUARDADO FINAL ---
    private async void OnGuardarEdicionClicked(object sender, EventArgs e)
    {
        try
        {
            await _gymService.ActualizarClienteAsync(_cliente);
            _hayEdicionPendiente = false; // ← LIMPIAR EL FLAG ANTES DEL ALERT
            btnGuardarEdicion.IsVisible = false;
            await DisplayAlertAsync("Guardado", "Los datos del cliente han sido actualizados y sincronizados con éxito.", "OK");
            await CargarDatosPerfil();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo guardar la información: {ex.Message}", "OK");
        }
    }
}