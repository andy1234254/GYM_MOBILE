using GymSecretMobile.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Networking;
using System.Diagnostics;

namespace GymSecretMobile
{
    public partial class App : Application
    {
        private readonly SyncService _syncService;
        public App(SyncService syncService, ImageSyncService imageSyncService)
        {
            InitializeComponent();
            _syncService = syncService;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
        protected override async void OnStart()
        {
            base.OnStart();
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                Debug.WriteLine("--> [App Inicial] Hay internet. Iniciando sincronización de arranque...");
                await EjecutarSincronizacionSeguraAsync();
            }
        }
        private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                Debug.WriteLine("--> [Internet] Conexión restaurada. Iniciando sincronización silenciosa...");
                await EjecutarSincronizacionSeguraAsync();
            }
            else
            {
                Debug.WriteLine("--> [Internet] El dispositivo se ha quedado sin conexión. Modo offline activo.");
            }
        }
        private async Task EjecutarSincronizacionSeguraAsync()
        {
            try
            {
                await _syncService.SincronizarClientesAsync();
                await _syncService.SincronizarAsistenciasAsync();
                await _syncService.SincronizarTransaccionesAsync();
                await _syncService.SincronizarProductosAsync();
                await _syncService.SincronizarVentasAsync();
                await _syncService.SincronizarCierresAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"--> [Error Sincronización]: {ex.Message}");
            }
        }
    }
}