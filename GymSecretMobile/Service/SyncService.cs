using Plugin.Firebase.Firestore;
using Plugin.Firebase;
using Firebase.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using GymSecretMobile.Models;
using Microsoft.Maui.Networking;
using SQLite;
using System.IO;
using Microsoft.Maui.Storage;
using System.Net.Http;

namespace GymSecretMobile.Service
{
    public class SyncService
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly IFirebaseFirestore _firestore;
        private readonly FirebaseStorage _storage;
        private static readonly SemaphoreSlim _clienteSyncLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _productoSyncLock = new SemaphoreSlim(1, 1);
        public SyncService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "YOUR_LOCAL");
            _db = new SQLiteAsyncConnection(dbPath);
            _firestore = CrossFirebaseFirestore.Current;
            _storage = new FirebaseStorage("YOUR_STORAGE");
        }
        /// <summary>
        /// Método principal que ejecutará la sincronización bidireccional de clientes
        /// </summary>
        public async Task SincronizarClientesAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;
            if (!_clienteSyncLock.Wait(0))
            {
                System.Diagnostics.Debug.WriteLine("--> [SyncEngine] Sincronización de clientes ignorada: Ya hay un proceso en curso.");
                return;
            }
            try
            {
                await _db.CreateTableAsync<Cliente>();
                await SubirClientesPendientesAsync();
                await BajarClientesNuevosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error]: {ex.Message}");
            }
            finally
            {
                _clienteSyncLock.Release();
            }
        }
        // ================= SUBIR CAMBIOS LOCALES A FIREBASE =================
        private async Task SubirClientesPendientesAsync()
        {
            var clientesPendientes = await _db.Table<Cliente>().Where(c => !c.IsSynced).ToListAsync();
            foreach (var cliente in clientesPendientes)
            {
                try
                {
                    if (!string.IsNullOrEmpty(cliente.FotoLocalPath))
                    {
                        if (string.IsNullOrEmpty(cliente.FotoUrl) || (!cliente.FotoUrl.StartsWith("http://") && !cliente.FotoUrl.StartsWith("https://")))
                        {
                            string urlNube = await SubirFotoStorageAsync(cliente.FotoLocalPath, cliente.GuidHub);
                            if (!string.IsNullOrEmpty(urlNube))
                            {
                                cliente.FotoUrl = urlNube;
                            }
                        }
                    }
                    await _firestore.GetCollection("Clientes").GetDocument(cliente.GuidHub).SetDataAsync(cliente);
                    cliente.IsSynced = true;
                    await _db.UpdateAsync(cliente);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error]: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// Método auxiliar para subir la foto física a Firebase.Storage
        /// ahora con parámetro opcional `carpeta` (default "perfiles") para reutilizarlo en productos y clientes.
        /// </summary>
        private async Task<string> SubirFotoStorageAsync(string rutaLocal, string guidHub, string carpeta = "perfiles")
        {
            if (!File.Exists(rutaLocal))
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Storage] Advertencia: El archivo local no existe: {rutaLocal}");
                return null;
            }
            try
            {
                using var stream = File.OpenRead(rutaLocal);
                var imageUrl = await _storage.Child(carpeta).Child($"{guidHub}.jpg").PutAsync(stream);
                return imageUrl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Storage Error Crítico]: {ex.Message}");
                return null;
            }
        }
        // ================= BAJAR CAMBIOS DESDE FIREBASE =================
        private async Task BajarClientesNuevosAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Clientes").GetDocumentsAsync<Cliente>();
                var guidsEnNube = new HashSet<string>();
                foreach (var doc in snapshot.Documents)
                {
                    var clienteNube = doc.Data;
                    if (clienteNube == null || string.IsNullOrEmpty(clienteNube.GuidHub))
                        continue;
                    guidsEnNube.Add(clienteNube.GuidHub);
                    var clienteLocal = await _db.Table<Cliente>().Where(c => c.GuidHub == clienteNube.GuidHub).FirstOrDefaultAsync();
                    if (clienteLocal == null)
                    {
                        if (!clienteNube.IsDeleted)
                        {
                            clienteNube.IsSynced = true;
                            if (!string.IsNullOrEmpty(clienteNube.FotoUrl))
                            {
                                clienteNube.FotoLocalPath = await DescargarFotoNubeAsync(clienteNube.FotoUrl, clienteNube.GuidHub);
                            }
                            await _db.InsertAsync(clienteNube);
                            System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Descargado nuevo cliente: {clienteNube.NombreCompleto}");
                        }
                    }
                    else
                    {
                        if (clienteNube.UltimaModificacion > clienteLocal.UltimaModificacion)
                        {
                            if (clienteNube.IsDeleted)
                            {
                                await _db.DeleteAsync(clienteLocal);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Cliente eliminado por la nube: {clienteLocal.NombreCompleto}");
                                if (!string.IsNullOrEmpty(clienteLocal.FotoLocalPath) && System.IO.File.Exists(clienteLocal.FotoLocalPath))
                                    System.IO.File.Delete(clienteLocal.FotoLocalPath);
                            }
                            else
                            {
                                clienteNube.Id = clienteLocal.Id;
                                clienteNube.IsSynced = true;
                                clienteNube.FotoLocalPath = clienteLocal.FotoLocalPath;
                                if (!string.IsNullOrEmpty(clienteNube.FotoUrl) && clienteNube.FotoUrl != clienteLocal.FotoUrl)
                                {
                                    clienteNube.FotoLocalPath = await DescargarFotoNubeAsync(clienteNube.FotoUrl, clienteNube.GuidHub);
                                }
                                await _db.UpdateAsync(clienteNube);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Cliente actualizado por la nube: {clienteNube.NombreCompleto}");
                            }
                        }
                    }
                }
                var clientesLocales = await _db.Table<Cliente>().ToListAsync();
                foreach (var local in clientesLocales)
                {
                    if (local.IsSynced && !guidsEnNube.Contains(local.GuidHub))
                    {
                        await _db.DeleteAsync(local);
                        System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Cliente eliminado localmente por reconciliación: {local.NombreCompleto}");
                        if (!string.IsNullOrEmpty(local.FotoLocalPath) && System.IO.File.Exists(local.FotoLocalPath))
                        {
                            System.IO.File.Delete(local.FotoLocalPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajada]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método auxiliar para descargar una imagen de Firebase Storage y guardarla localmente
        /// </summary>
        private async Task<string> DescargarFotoNubeAsync(string urlNube, string guidHub)
        {
            if (string.IsNullOrEmpty(urlNube)) return null;
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(urlNube);
                string nombreArchivo = $"{guidHub}.jpg";
                string rutaLocal = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
                await File.WriteAllBytesAsync(rutaLocal, bytes);
                return rutaLocal;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Error descargando foto: {ex.Message}");
                return null;
            }
        }
        public async Task SincronizarPlanesAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;
            await _db.CreateTableAsync<Plan>();
            try
            {
                await SubirPlanesPendientesAsync();
                await BajarPlanesNuevosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Planes Error]: {ex.Message}");
            }
        }
        // ================= SUBIR PLANES A FIREBASE =================
        private async Task SubirPlanesPendientesAsync()
        {
            var planesPendientes = await _db.Table<Plan>().Where(p => !p.IsSynced).ToListAsync();
            foreach (var plan in planesPendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(plan.GuidHub))
                    {
                        plan.GuidHub = Guid.NewGuid().ToString();
                        await _db.UpdateAsync(plan);
                    }
                    await _firestore.GetCollection("Planes").GetDocument(plan.GuidHub).SetDataAsync(plan);
                    plan.IsSynced = true;
                    await _db.UpdateAsync(plan);
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Plan subido exitosamente: {plan.Nombre}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error SUBIENDO Plan {plan.Nombre}]: {ex.Message}");
                }
            }
        }
        // ================= BAJAR PLANES DESDE FIREBASE =================
        private async Task BajarPlanesNuevosAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Planes").GetDocumentsAsync<Plan>();
                foreach (var doc in snapshot.Documents)
                {
                    var planNube = doc.Data;
                    if (planNube == null || string.IsNullOrEmpty(planNube.GuidHub))
                        continue;
                    var planLocal = await _db.Table<Plan>().Where(p => p.GuidHub == planNube.GuidHub).FirstOrDefaultAsync();
                    if (planLocal == null)
                    {
                        if (!planNube.IsDeleted)
                        {
                            planNube.IsSynced = true;
                            await _db.InsertAsync(planNube);
                            System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Descargado nuevo plan: {planNube.Nombre}");
                        }
                    }
                    else
                    {
                        if (planNube.UltimaModificacion > planLocal.UltimaModificacion)
                        {
                            if (planNube.IsDeleted)
                            {
                                await _db.DeleteAsync(planLocal);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Plan eliminado por la nube: {planLocal.Nombre}");
                            }
                            else
                            {
                                planNube.Id = planLocal.Id;
                                planNube.IsSynced = true;
                                await _db.UpdateAsync(planNube);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Plan actualizado por la nube: {planNube.Nombre}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajada Planes]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método principal para la sincronización bidireccional de Asistencias
        /// </summary>
        public async Task SincronizarAsistenciasAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;
            await _db.CreateTableAsync<Asistencia>();
            try
            {
                await SubirAsistenciasPendientesAsync();
                await BajarAsistenciasNuevasAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Asistencias]: {ex.Message}");
            }
        }
        // ================= SUBIR ASISTENCIAS A FIREBASE =================
        private async Task SubirAsistenciasPendientesAsync()
        {
            var asistenciasPendientes = await _db.Table<Asistencia>().Where(a => !a.IsSynced).ToListAsync();
            foreach (var asistencia in asistenciasPendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(asistencia.GuidHub))
                    {
                        asistencia.GuidHub = Guid.NewGuid().ToString();
                        await _db.UpdateAsync(asistencia);
                    }

                    await _firestore.GetCollection("Asistencias").GetDocument(asistencia.GuidHub).SetDataAsync(asistencia);
                    asistencia.IsSynced = true;
                    await _db.UpdateAsync(asistencia);
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Asistencia subida exitosamente.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error SUBIENDO Asistencia]: {ex.Message}");
                }
            }
        }
        // ================= BAJAR ASISTENCIAS DESDE FIREBASE =================
        private async Task BajarAsistenciasNuevasAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Asistencias").GetDocumentsAsync<Asistencia>();
                foreach (var doc in snapshot.Documents)
                {
                    var asistenciaNube = doc.Data;
                    if (asistenciaNube == null || string.IsNullOrEmpty(asistenciaNube.GuidHub))
                        continue;
                    var asistenciaLocal = await _db.Table<Asistencia>().Where(a => a.GuidHub == asistenciaNube.GuidHub).FirstOrDefaultAsync();
                    var clienteAsociado = await _db.Table<Cliente>().Where(c => c.GuidHub == asistenciaNube.ClienteGuidHub).FirstOrDefaultAsync();
                    if (clienteAsociado == null) continue;
                    asistenciaNube.ClienteId = clienteAsociado.Id;
                    if (asistenciaLocal == null)
                    {
                        if (!asistenciaNube.IsDeleted)
                        {
                            asistenciaNube.IsSynced = true;
                            await _db.InsertAsync(asistenciaNube);
                            System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Descargada nueva asistencia.");
                        }
                    }
                    else
                    {
                        if (asistenciaNube.UltimaModificacion > asistenciaLocal.UltimaModificacion)
                        {
                            if (asistenciaNube.IsDeleted)
                            {
                                await _db.DeleteAsync(asistenciaLocal);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Asistencia eliminada por la nube.");
                            }
                            else
                            {
                                asistenciaNube.Id = asistenciaLocal.Id;
                                asistenciaNube.IsSynced = true;
                                await _db.UpdateAsync(asistenciaNube);
                                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Asistencia actualizada por la nube.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajada Asistencias]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método orquestador para la sincronización de Transacciones
        /// </summary>
        public async Task SincronizarTransaccionesAsync()
        {
            // 1. Verificación de conexión
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

            // 2. Asegurar que la tabla existe
            await _db.CreateTableAsync<Transaccion>();

            try
            {
                // 3. Orquestación: Solo llamamos a los métodos privados
                await SubirTransaccionesPendientesAsync();
                await BajarTransaccionesNuevasAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Transacciones]: {ex.Message}");
            }
        }

        // ================= MÉTODOS PRIVADOS (Lógica interna) =================

        private async Task SubirTransaccionesPendientesAsync()
        {
            var pendientes = await _db.Table<Transaccion>().Where(t => !t.IsSynced).ToListAsync();
            foreach (var trans in pendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(trans.GuidHub)) trans.GuidHub = Guid.NewGuid().ToString();

                    await _firestore.GetCollection("Transacciones").GetDocument(trans.GuidHub).SetDataAsync(trans);

                    trans.IsSynced = true;
                    await _db.UpdateAsync(trans);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Subiendo]: {ex.Message}");
                }
            }
        }

        private async Task BajarTransaccionesNuevasAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Transacciones").GetDocumentsAsync<Transaccion>();
                var guidsEnNube = new HashSet<string>();

                foreach (var doc in snapshot.Documents)
                {
                    var txNube = doc.Data;
                    if (txNube == null || string.IsNullOrEmpty(txNube.GuidHub)) continue;

                    guidsEnNube.Add(txNube.GuidHub);

                    var txLocal = await _db.Table<Transaccion>().Where(t => t.GuidHub == txNube.GuidHub).FirstOrDefaultAsync();

                    // 1. Prioridad: Borrado (si viene marcado como borrado de la nube)
                    if (txLocal != null && txNube.IsDeleted)
                    {
                        await _db.DeleteAsync(txLocal);
                        continue;
                    }

                    // 2. Si está marcado como borrado en nube pero no existe localmente, ignorar
                    if (txNube.IsDeleted) continue;

                    // 3. Insertar o Actualizar
                    if (txLocal == null)
                    {
                        txNube.IsSynced = true;
                        await _db.InsertAsync(txNube);
                    }
                    else
                    {
                        if (txNube.UltimaModificacion > txLocal.UltimaModificacion)
                        {
                            txNube.Id = txLocal.Id; // Conservar ID local
                            txNube.IsSynced = true;
                            await _db.UpdateAsync(txNube);
                        }
                    }
                }

                // 4. Limpieza: Eliminar lo que no esté en la nube (reconciliación)
                var transaccionesLocales = await _db.Table<Transaccion>().ToListAsync();
                foreach (var local in transaccionesLocales)
                {
                    if (local.IsSynced && !guidsEnNube.Contains(local.GuidHub))
                    {
                        await _db.DeleteAsync(local);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajando]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método principal para la sincronización de Productos/Inventario
        /// </summary>
        public async Task SincronizarProductosAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;
            if (!_productoSyncLock.Wait(0))
            {
                System.Diagnostics.Debug.WriteLine("--> [SyncEngine] Sincronización de stock ignorada: Ya hay un proceso en curso.");
                return;
            }

            try
            {
                await _db.CreateTableAsync<Producto>();
                await SubirProductosPendientesAsync();
                await BajarProductosNuevosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Stock]: {ex.Message}");
            }
            finally
            {
                _productoSyncLock.Release();
            }
        }
        // ================= SUBIR PRODUCTOS =================
        private async Task SubirProductosPendientesAsync()
        {
            var pendientes = await _db.Table<Producto>().Where(p => !p.IsSynced).ToListAsync();
            foreach (var producto in pendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(producto.GuidHub))
                    {
                        producto.GuidHub = Guid.NewGuid().ToString();
                        await _db.UpdateAsync(producto);
                    }
                    if (!string.IsNullOrEmpty(producto.FotoLocalPath) && File.Exists(producto.FotoLocalPath) && string.IsNullOrEmpty(producto.FotoUrl))
                    {
                        string nuevaUrl = await SubirFotoStorageAsync(producto.FotoLocalPath, producto.GuidHub, "productos");
                        if (!string.IsNullOrEmpty(nuevaUrl))
                        {
                            producto.FotoUrl = nuevaUrl; 
                            await _db.UpdateAsync(producto);
                        }
                    }
                    await _firestore.GetCollection("Productos").GetDocument(producto.GuidHub).SetDataAsync(producto);
                    producto.IsSynced = true;
                    await _db.UpdateAsync(producto);
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Producto {producto.Nombre} sincronizado con éxito.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error SUBIENDO Producto]: {ex.Message}");
                }
            }
        }

        // ================= BAJAR PRODUCTOS =================
        private async Task BajarProductosNuevosAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Productos").GetDocumentsAsync<Producto>();
                var guidsEnNube = new HashSet<string>();
                foreach (var doc in snapshot.Documents)
                {
                    var prodNube = doc.Data;
                    if (prodNube == null || string.IsNullOrEmpty(prodNube.GuidHub))
                        continue;
                    guidsEnNube.Add(prodNube.GuidHub);
                    var prodLocal = await _db.Table<Producto>().Where(p => p.GuidHub == prodNube.GuidHub).FirstOrDefaultAsync();
                    if (prodLocal == null)
                    {
                        if (!prodNube.IsDeleted)
                        {
                            prodNube.IsSynced = true;
                            await _db.InsertAsync(prodNube);
                        }
                    }
                    else
                    {
                        if (prodNube.UltimaModificacion > prodLocal.UltimaModificacion)
                        {
                            if (prodNube.IsDeleted)
                            {
                                await _db.DeleteAsync(prodLocal);
                            }
                            else
                            {
                                prodNube.Id = prodLocal.Id;
                                prodNube.IsSynced = true;
                                await _db.UpdateAsync(prodNube);
                            }
                        }
                    }
                }
                var productosLocales = await _db.Table<Producto>().ToListAsync();
                foreach (var local in productosLocales)
                {
                    if (local.IsSynced && !guidsEnNube.Contains(local.GuidHub))
                    {
                        await _db.DeleteAsync(local);
                        System.Diagnostics.Debug.WriteLine($"--> [SyncEngine] Producto eliminado localmente por reconciliación: {local.Nombre}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajando Productos]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método principal para la sincronización de Ventas POS
        /// </summary>
        public async Task SincronizarVentasAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            await _db.CreateTableAsync<Venta>();
            await _db.CreateTableAsync<DetalleVenta>();
            try
            {
                await SubirVentasPendientesAsync();
                await BajarVentasNuevasAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Ventas]: {ex.Message}");
            }
        }
        // ================= SUBIR VENTAS =================
        private async Task SubirVentasPendientesAsync()
        {
            var ventasPendientes = await _db.Table<Venta>().Where(v => !v.IsSynced).ToListAsync();
            foreach (var venta in ventasPendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(venta.GuidHub))
                    {
                        venta.GuidHub = Guid.NewGuid().ToString();
                        await _db.UpdateAsync(venta);
                    }
                    var detalles = await _db.Table<DetalleVenta>().Where(d => d.VentaId == venta.Id).ToListAsync();
                    venta.Detalles = detalles;
                    await _firestore.GetCollection("Ventas").GetDocument(venta.GuidHub).SetDataAsync(venta);
                    venta.IsSynced = true;
                    await _db.UpdateAsync(venta);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error SUBIENDO Venta]: {ex.Message}");
                }
            }
        }
        // ================= BAJAR VENTAS =================
        private async Task BajarVentasNuevasAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("Ventas").GetDocumentsAsync<Venta>();
                foreach (var doc in snapshot.Documents)
                {
                    var ventaNube = doc.Data;
                    if (ventaNube == null || string.IsNullOrEmpty(ventaNube.GuidHub)) continue;
                    var ventaLocal = await _db.Table<Venta>().Where(v => v.GuidHub == ventaNube.GuidHub).FirstOrDefaultAsync();
                    if (ventaLocal == null)
                    {
                        if (!ventaNube.IsDeleted)
                        {
                            ventaNube.IsSynced = true;
                            await _db.InsertAsync(ventaNube);
                            if (ventaNube.Detalles != null && ventaNube.Detalles.Count > 0)
                            {
                                foreach (var detalle in ventaNube.Detalles)
                                {
                                    var productoRelacionado = await _db.Table<Producto>().Where(p => p.GuidHub == detalle.ProductoGuidHub).FirstOrDefaultAsync();
                                    detalle.VentaId = ventaNube.Id; 
                                    detalle.ProductoId = productoRelacionado?.Id ?? 0;
                                    await _db.InsertAsync(detalle);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajando Ventas]: {ex.Message}");
            }
        }
        /// <summary>
        /// Método principal para la sincronización de Cierres de Caja
        /// </summary>
        public async Task SincronizarCierresAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            await _db.CreateTableAsync<CierreCaja>();
            try
            {
                await SubirCierresPendientesAsync();
                await BajarCierresNuevosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Cierres]: {ex.Message}");
            }
        }
        // ================= SUBIR CIERRES =================
        private async Task SubirCierresPendientesAsync()
        {
            var pendientes = await _db.Table<CierreCaja>().Where(c => !c.IsSynced).ToListAsync();
            foreach (var cierre in pendientes)
            {
                try
                {
                    if (string.IsNullOrEmpty(cierre.GuidHub))
                    {
                        cierre.GuidHub = Guid.NewGuid().ToString();
                        await _db.UpdateAsync(cierre);
                    }
                    await _firestore.GetCollection("CierresCaja").GetDocument(cierre.GuidHub).SetDataAsync(cierre);
                    cierre.IsSynced = true;
                    await _db.UpdateAsync(cierre);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error SUBIENDO Cierre]: {ex.Message}");
                }
            }
        }
        // ================= BAJAR CIERRES =================
        private async Task BajarCierresNuevosAsync()
        {
            try
            {
                var snapshot = await _firestore.GetCollection("CierresCaja").GetDocumentsAsync<CierreCaja>();
                foreach (var doc in snapshot.Documents)
                {
                    var cierreNube = doc.Data;
                    if (cierreNube == null || string.IsNullOrEmpty(cierreNube.GuidHub)) continue;
                    var local = await _db.Table<CierreCaja>().Where(c => c.GuidHub == cierreNube.GuidHub).FirstOrDefaultAsync();
                    if (local == null)
                    {
                        if (!cierreNube.IsDeleted)
                        {
                            cierreNube.IsSynced = true;
                            await _db.InsertAsync(cierreNube);
                        }
                    }
                    else
                    {
                        if (cierreNube.UltimaModificacion > local.UltimaModificacion)
                        {
                            if (cierreNube.IsDeleted)
                            {
                                await _db.DeleteAsync(local);
                            }
                            else
                            {
                                cierreNube.Id = local.Id;
                                cierreNube.IsSynced = true;
                                await _db.UpdateAsync(cierreNube);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--> [SyncEngine Error Bajando Cierres]: {ex.Message}");
            }
        }
    }

}
