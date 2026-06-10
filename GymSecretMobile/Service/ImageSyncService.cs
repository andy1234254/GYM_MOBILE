using Firebase.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using GymSecretMobile.Models;

namespace GymSecretMobile.Service
{
    public class ImageSyncService
    {
        private readonly string _storageBucket = "YOUR_FIRESTORAGE";
        public ImageSyncService()
        {
        }
        /// <summary>
        /// Sube una foto de cliente a Firebase Storage y devuelve su URL pública.
        /// Diseñado para ser invocado de forma segura dentro de la transacción de sincronización estructurada.
        /// </summary>
        public async Task<string> SubirFotoClienteAsync(string rutaLocal, string guidHub)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;
            if (string.IsNullOrEmpty(rutaLocal) || !File.Exists(rutaLocal))
            {
                System.Diagnostics.Debug.WriteLine($"--> [ImageSync] Advertencia: Archivo local no encontrado: {rutaLocal}");
                return null;
            }
            try
            {
                using var stream = File.OpenRead(rutaLocal);
                var task = new FirebaseStorage(_storageBucket).Child("perfiles").Child($"{guidHub}.jpg").PutAsync(stream);
                string downloadUrl = await task;
                return downloadUrl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [ImageSync Error] Falla al subir foto de cliente {guidHub}: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Sube una foto de producto a Firebase Storage y devuelve su URL pública.
        /// </summary>
        public async Task<string> SubirFotoProductoAsync(string rutaLocal, string guidHub)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;
            if (string.IsNullOrEmpty(rutaLocal) || !File.Exists(rutaLocal))
            {
                System.Diagnostics.Debug.WriteLine($"--> [ImageSync] Advertencia: Archivo local de producto no encontrado: {rutaLocal}");
                return null;
            }
            try
            {
                using var stream = File.OpenRead(rutaLocal);
                var task = new FirebaseStorage(_storageBucket).Child("productos").Child($"{guidHub}.jpg").PutAsync(stream);
                string downloadUrl = await task;
                return downloadUrl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [ImageSync Error] Falla al subir foto de producto {guidHub}: {ex.Message}");
                return null;
            }
        }
    }
}
