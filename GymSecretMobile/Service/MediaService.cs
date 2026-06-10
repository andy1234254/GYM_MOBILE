using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GymSecretMobile.Service
{
    public class MediaService
    {
        public async Task<string> TomarFotoAsync()
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
                    return await GuardarImagenComprimidaAsync(photo);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [MediaService Error] Cámara: {ex.Message}");
                return null;
            }
        }

        public async Task<string> SeleccionarFotoAsync()
        {
            try
            {
                FileResult photo = await MediaPicker.Default.PickPhotoAsync();
                return await GuardarImagenComprimidaAsync(photo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [MediaService Error] Galería: {ex.Message}");
                return null;
            }
        }

        // 🌟 NUEVO: Método optimizado para redimensionar y comprimir
        private async Task<string> GuardarImagenComprimidaAsync(FileResult photo)
        {
            if (photo == null) return null;

            // Forzamos la extensión a .jpg para aprovechar la compresión
            string nombreArchivo = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(photo.FileName)}.jpg";
            string rutaLocal = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);

            using Stream sourceStream = await photo.OpenReadAsync();

            try
            {
                // 1. Cargamos la imagen nativa usando el motor de gráficos de MAUI
                Microsoft.Maui.Graphics.IImage image = PlatformImage.FromStream(sourceStream);
                if (image == null) return null;

                // 2. Redimensionamos. 400x400 px es calidad HD más que suficiente para una foto de perfil de recepción.
                Microsoft.Maui.Graphics.IImage resizedImage = image.Downsize(400, 400, true);

                // 3. Guardamos la imagen comprimiendo la calidad al 75% (0.75f)
                using FileStream localFileStream = File.OpenWrite(rutaLocal);

                // ImageFormat.Jpeg es clave para reducir el peso drásticamente
                resizedImage.Save(localFileStream, ImageFormat.Jpeg, 0.75f);

                return rutaLocal;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [MediaService Error] Fallo al comprimir: {ex.Message}");
                return null;
            }
        }
    }
}
