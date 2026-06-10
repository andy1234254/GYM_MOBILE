using GymSecretMobile.Models;
using Plugin.Firebase.Firestore;
using Firebase.Storage;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;           
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace GymSecretMobile.Service
{
    public class GymService
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly SyncService _syncService;    
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);//Evita que múltiples hilos inicialicen la DB a la vez
        private bool _tablasCreadas = false;

        public GymService(SyncService syncService)
        {
            _syncService = syncService;
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "YOUR_LOCAL");
            _db = new SQLiteAsyncConnection(dbPath);
            Microsoft.Maui.Networking.Connectivity.Current.ConnectivityChanged += async (sender, e) =>
            {
                if (e.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet)
                {
                    System.Diagnostics.Debug.WriteLine("--> [Red] ¡Internet detectado! Disparando sincronización...");
                    await _syncService.SincronizarClientesAsync();
                    await _syncService.SincronizarPlanesAsync();
                }
            };
            Task.Run(async () => await AutoClosePreviousMonthIfNeededAsync());
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                System.Diagnostics.Debug.WriteLine("--> [Inicio] Ejecutando sincronización inicial de apertura...");
                await _syncService.SincronizarClientesAsync();
                await _syncService.SincronizarPlanesAsync();
            });
        }
        private async Task InitAsync()
        {
            if (_tablasCreadas)
                return;
            await _initSemaphore.WaitAsync();
            try
            {
                if (_tablasCreadas) return;
                await _db.CreateTableAsync<Plan>();
                await _db.CreateTableAsync<Cliente>();
                await _db.CreateTableAsync<Transaccion>();
                await _db.CreateTableAsync<CierreCaja>();
                await _db.CreateTableAsync<Producto>();
                await _db.CreateTableAsync<Venta>();
                await _db.CreateTableAsync<DetalleVenta>();
                await _db.CreateTableAsync<Asistencia>();
                _tablasCreadas = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }
        // --- MÉTODOS DE FOTOS ---
        public async Task<List<Cliente>> ObtenerClientesSinFotoEnNubeAsync()
        {
            await InitAsync();
            return await _db.Table<Cliente>()
                .Where(c => !c.IsDeleted &&
                            c.FotoLocalPath != null && c.FotoLocalPath != "" &&
                            (c.FotoUrl == null || c.FotoUrl == ""))
                .ToListAsync();
        }
        public async Task ActualizarFotoClienteAsync(Cliente cliente)
        {
            await InitAsync();
            cliente.IsSynced = false;
            cliente.FotoUrl = string.Empty;
            cliente.UltimaModificacion = DateTime.Now;
            await _db.UpdateAsync(cliente);
            _ = _syncService.SincronizarClientesAsync();
        }
        public async Task<List<Producto>> ObtenerProductosSinFotoEnNubeAsync()
        {
            await InitAsync();
            return await _db.Table<Producto>()
                .Where(p => !p.IsDeleted &&
                            p.FotoLocalPath != null && p.FotoLocalPath != "" &&
                            (p.FotoUrl == null || p.FotoUrl == ""))
                .ToListAsync();
        }
        public async Task ActualizarFotoProductoAsync(Producto producto)
        {
            await InitAsync();
            producto.IsSynced = false;
            producto.UltimaModificacion = DateTime.Now;
            await _db.UpdateAsync(producto);
            _ = _syncService.SincronizarProductosAsync();
        }
        // ================= MÓDULO DE PLANES =================
        public async Task<List<Plan>> ObtenerPlanesAsync()
        {
            await InitAsync();
            var planes = await _db.Table<Plan>().Where(p => !p.IsDeleted).ToListAsync();
            bool requiereSync = false;
            foreach (var plan in planes)
            {
                if (string.IsNullOrEmpty(plan.GuidHub))
                {
                    plan.GuidHub = Guid.NewGuid().ToString();
                    plan.IsSynced = false;
                    await _db.UpdateAsync(plan);
                    requiereSync = true;
                }
            }
            if (requiereSync)
            {
                _ = _syncService.SincronizarPlanesAsync();
            }
            return planes;
        }
        public async Task<int> GuardarPlanAsync(Plan plan)
        {
            await InitAsync();
            plan.UltimaModificacion = DateTime.Now;
            plan.IsSynced = false;
            if (string.IsNullOrEmpty(plan.GuidHub))
            {
                plan.GuidHub = Guid.NewGuid().ToString();
            }
            int resultado = plan.Id != 0 ? await _db.UpdateAsync(plan) : await _db.InsertAsync(plan);
            _ = _syncService.SincronizarPlanesAsync();
            return resultado;
        }
        public async Task<int> EliminarPlanAsync(Plan plan)
        {
            await InitAsync();
            plan.IsDeleted = true;
            plan.UltimaModificacion = DateTime.Now;
            plan.IsSynced = false;
            int resultado = await _db.UpdateAsync(plan);
            _ = _syncService.SincronizarPlanesAsync();
            return resultado;
        }
        // ================= RENOVAR PLAN =================
        public async Task RenovarSuscripcionAsync(Cliente cliente, Plan plan, Cliente pareja = null)
        {
            await InitAsync();
            cliente.PlanId = plan.Id;
            cliente.PlanGuidHub = plan.GuidHub;
            cliente.FechaUltimoPago = DateTime.Now;
            cliente.FechaVencimiento = DateTime.Now.AddDays(plan.DiasDuracion > 0 ? plan.DiasDuracion : 30);
            cliente.AsistenciasConsumidas = 0;
            cliente.EstaActivo = true;
            cliente.ParejaId = pareja?.Id;
            cliente.UltimaModificacion = DateTime.Now;
            cliente.IsSynced = false;
            await _db.UpdateAsync(cliente);
            if (pareja != null)
            {
                pareja.ParejaId = cliente.Id;
                pareja.UltimaModificacion = DateTime.Now;
                pareja.IsSynced = false;
                await _db.UpdateAsync(pareja);
            }
            var transaccion = new Transaccion
            {
                GuidHub = Guid.NewGuid().ToString(),
                Fecha = DateTime.Now,
                Monto = plan.Precio,
                Tipo = "Ingreso",
                Concepto = $"Renovación {plan.Nombre}: {cliente.NombreCompleto}",
                ClienteId = cliente.Id,
                UltimaModificacion = DateTime.Now,
                IsSynced = false
            };
            await _db.InsertAsync(transaccion);
            _ = _syncService.SincronizarClientesAsync();
            _ = _syncService.SincronizarPlanesAsync();
            _ = _syncService.SincronizarTransaccionesAsync();
        }
        // ================= MÓDULO DE CLIENTES =================
        public async Task<List<Cliente>> ObtenerClientesAsync()
        {
            await InitAsync();
            return await _db.Table<Cliente>().Where(c => !c.IsDeleted).ToListAsync();
        }
        public async Task<List<Cliente>> GetClientesConPlanesAsync()
        {
            await InitAsync();
            var clientes = await _db.Table<Cliente>().Where(c => !c.IsDeleted).ToListAsync();
            var planes = await _db.Table<Plan>().Where(p => !p.IsDeleted).ToListAsync();
            foreach (var cliente in clientes)
            {
                cliente.Plan = planes.Find(p => p.GuidHub == cliente.PlanGuidHub);
            }
            return clientes;
        }
        public async Task<List<Cliente>> SearchClientesAsync(string term)
        {
            await InitAsync();
            var like = "%" + term + "%";
            var sql = "SELECT * FROM Cliente WHERE IsDeleted = 0 AND NombreCompleto LIKE ? ORDER BY NombreCompleto";
            return await _db.QueryAsync<Cliente>(sql, like);
        }
        public async Task GuardarClienteConTodoAsync(Cliente cliente, Plan plan, Cliente pareja = null)
        {
            await InitAsync();
            if (string.IsNullOrEmpty(cliente.GuidHub))
            {
                cliente.GuidHub = Guid.NewGuid().ToString();
            }
            cliente.UltimaModificacion = DateTime.Now;
            cliente.IsSynced = false;
            cliente.IsDeleted = false;
            await _db.InsertAsync(cliente);
            if (pareja != null)
            {
                pareja.ParejaId = cliente.Id;
                pareja.UltimaModificacion = DateTime.Now;
                pareja.IsSynced = false;
                await _db.UpdateAsync(pareja);
            }
            var pago = new Transaccion
            {
                GuidHub = Guid.NewGuid().ToString(),
                Fecha = DateTime.Now,
                Monto = plan.Precio,
                Tipo = "Ingreso",
                Concepto = $"Suscripción Plan {plan.Nombre}: {cliente.NombreCompleto}",
                ClienteId = cliente.Id,            
                ClienteGuidHub = cliente.GuidHub,   
                UltimaModificacion = DateTime.Now,
                IsSynced = false,
                IsDeleted = false
            };
            await _db.InsertAsync(pago);
            _ = _syncService.SincronizarClientesAsync();
            _ = _syncService.SincronizarTransaccionesAsync();
        }
        public async Task<int> ActualizarClienteAsync(Cliente cliente)
        {
            await InitAsync();
            cliente.UltimaModificacion = DateTime.Now;
            cliente.IsSynced = false; 
            int resultado = await _db.UpdateAsync(cliente);
            _ = _syncService.SincronizarClientesAsync();
            return resultado;
        }
        public async Task<bool> RegistrarAsistenciaAsync(int clienteId)
        {
            await InitAsync();
            var cliente = await _db.Table<Cliente>().Where(c => c.Id == clienteId).FirstOrDefaultAsync();
            if (cliente != null &&
                cliente.EstaActivo &&
                !cliente.IsDeleted && 
                cliente.AsistenciasConsumidas < cliente.AsistenciasTotales &&
                cliente.FechaVencimiento >= DateTime.Today)
            {
                cliente.AsistenciasConsumidas++;
                cliente.UltimaModificacion = DateTime.Now;
                cliente.IsSynced = false;
                await _db.UpdateAsync(cliente);
                _ = _syncService.SincronizarClientesAsync();
                return true;
            }
            return false;
        }
        public async Task<Cliente> GetClienteByIdAsync(int id)
        {
            await InitAsync();
            return await _db.Table<Cliente>().Where(c => c.Id == id && !c.IsDeleted).FirstOrDefaultAsync();
        }
        public async Task EliminarClienteLogicoAsync(Cliente cliente)
        {
            await InitAsync();
            cliente.IsDeleted = true;
            cliente.UltimaModificacion = DateTime.Now;
            cliente.IsSynced = false;
            await _db.UpdateAsync(cliente);
            _ = _syncService.SincronizarClientesAsync();
        }
        // ================= MÓDULO DE ASISTENCIA DIARIA =================
        public async Task<(bool Exito, string Mensaje)> RegistrarAsistenciaConLogAsync(int clienteId)
        {
            await InitAsync();
            var cliente = await _db.Table<Cliente>().Where(c => c.Id == clienteId).FirstOrDefaultAsync();
            if (cliente == null) return (false, "Cliente no encontrado.");
            if (!cliente.EstaActivo || cliente.AsistenciasConsumidas >= cliente.AsistenciasTotales || cliente.FechaVencimiento < DateTime.Now)
            {
                return (false, "El cliente no tiene asistencias disponibles o su plan venció.");
            }
            var nuevaAsistencia = new Asistencia
            {
                GuidHub = Guid.NewGuid().ToString(),
                ClienteId = clienteId,
                ClienteGuidHub = cliente.GuidHub,
                FechaHora = DateTime.Now,
                UltimaModificacion = DateTime.Now,
                IsSynced = false
            };
            await _db.InsertAsync(nuevaAsistencia);
            cliente.AsistenciasConsumidas++;
            if (cliente.AsistenciasConsumidas >= cliente.AsistenciasTotales)
                cliente.EstaActivo = false;
            cliente.UltimaModificacion = DateTime.Now;
            cliente.IsSynced = false;
            await _db.UpdateAsync(cliente);
            _ = _syncService.SincronizarClientesAsync();
            _ = _syncService.SincronizarAsistenciasAsync();
            return (true, "Asistencia registrada con éxito.");
        }
        public async Task<List<Asistencia>> GetAsistenciasPorMesAsync(int clienteId, int mes, int anio)
        {
            await InitAsync();
            // Filtramos activamente por !IsDeleted aquí
            var lista = await _db.Table<Asistencia>()
                                 .Where(a => a.ClienteId == clienteId && !a.IsDeleted)
                                 .ToListAsync();

            return lista.Where(a => a.FechaHora.Month == mes && a.FechaHora.Year == anio)
                        .OrderByDescending(a => a.FechaHora)
                        .ToList();
        }
        // Agrega esto en tu GymService
        public async Task ActualizarAsistenciaAsync(Asistencia asistencia)
        {
            await InitAsync();
            asistencia.UltimaModificacion = DateTime.Now;
            asistencia.IsSynced = false; // Forzamos la subida
            await _db.UpdateAsync(asistencia);

            // Disparamos sync en segundo plano
            _ = _syncService.SincronizarAsistenciasAsync();
        }

        public async Task SoftDeleteAsistenciaAsync(Asistencia asistencia)
        {
            await InitAsync();
            asistencia.IsDeleted = true; // Soft Delete
            asistencia.IsSynced = false; // Forzamos la subida para avisar a Firestore
            asistencia.UltimaModificacion = DateTime.Now;
            await _db.UpdateAsync(asistencia);

            // Disparamos sync en segundo plano
            _ = _syncService.SincronizarAsistenciasAsync();
        }
        // ================= MÓDULO DE INVENTARIO Y PRODUCTOS =================
        public async Task<List<Producto>> GetProductosAsync()
        {
            await InitAsync();
            return await _db.Table<Producto>().Where(p => !p.IsDeleted).ToListAsync();
        }
        public async Task<int> InsertarProductoAsync(Producto producto)
        {
            await InitAsync();
            if (string.IsNullOrEmpty(producto.GuidHub))
            producto.GuidHub = Guid.NewGuid().ToString();
            producto.UltimaModificacion = DateTime.Now;
            producto.IsSynced = false;
            int resultado = await _db.InsertAsync(producto);
            _ = _syncService.SincronizarProductosAsync();
            return resultado;
        }
        public async Task ComprarProductoInventarioAsync(Producto producto, int cantidad, double total)
        {
            await InitAsync();
            producto.CantidadDisponible += cantidad;
            producto.UltimaModificacion = DateTime.Now;
            producto.IsSynced = false;
            await _db.UpdateAsync(producto);
            _ = _syncService.SincronizarProductosAsync();
            var transaccion = new Transaccion
            {
                Tipo = "Producto",
                Monto = total,
                Fecha = DateTime.Now,
                Concepto = $"Compra Inventario: {producto.Nombre} (x{cantidad})"
            };
            await GuardarTransaccionAsync(transaccion);
        }
        public async Task EliminarProductoAsync(Producto producto)
        {
            await InitAsync();
            producto.IsDeleted = true;
            producto.IsSynced = false;
            producto.UltimaModificacion = DateTime.Now;
            await _db.UpdateAsync(producto);
            _ = _syncService.SincronizarProductosAsync();
        }
        // ================= MÓDULO DE TIENDA Y VENTAS =================
        public async Task ProcesarVentaCarritoAsync(List<DetalleVenta> carrito)
        {
            await InitAsync();
            if (carrito == null || carrito.Count == 0) return;
            double totalVenta = carrito.Sum(d => d.Subtotal);
            string detalleDeProductos = string.Join(", ", carrito.Select(d => $"{d.NombreProducto} (x{d.CantidadVendida})"));
            var venta = new Venta
            {
                Fecha = DateTime.Now,
                TotalVenta = totalVenta,
                GuidHub = Guid.NewGuid().ToString(),
                UltimaModificacion = DateTime.Now,
                IsSynced = false
            };
            await _db.InsertAsync(venta);
            foreach (var item in carrito)
            {
                var prod = await _db.Table<Producto>().Where(p => p.Id == item.ProductoId).FirstOrDefaultAsync();
                if (prod != null)
                {
                    item.ProductoGuidHub = prod.GuidHub;
                    item.VentaId = venta.Id;
                    await _db.InsertAsync(item);
                    prod.CantidadDisponible -= item.CantidadVendida;
                    if (prod.CantidadDisponible < 0) prod.CantidadDisponible = 0;
                    prod.UltimaModificacion = DateTime.Now;
                    prod.IsSynced = false;
                    await _db.UpdateAsync(prod);
                }
            }
            var transaccion = new Transaccion
            {
                Fecha = DateTime.Now,
                Monto = totalVenta,
                Tipo = "Ingreso",
                Concepto = $"Venta: {detalleDeProductos}"
            };
            await GuardarTransaccionAsync(transaccion);
            _ = _syncService.SincronizarVentasAsync();
            _ = _syncService.SincronizarProductosAsync();
        }
        // ================= MÓDULO DE TRANSACCIONES =================
        public async Task<List<Transaccion>> GetTransaccionesRecientesAsync()
        {
            await InitAsync();
            return await _db.Table<Transaccion>().OrderByDescending(t => t.Fecha).ToListAsync();
        }
        public async Task<int> GuardarTransaccionAsync(Transaccion t)
        {
            await InitAsync();
            if (string.IsNullOrEmpty(t.GuidHub))
            {
                t.GuidHub = Guid.NewGuid().ToString();
            }

            t.UltimaModificacion = DateTime.Now;
            t.IsSynced = false;
            int resultado = await _db.InsertAsync(t);
            _ = _syncService.SincronizarTransaccionesAsync();
            return resultado;
        }
        // ================= MÓDULO DE CIERRE DE CAJA MENSUAL =================
        public async Task<CierreCaja> GenerateAndSaveMonthlyClosureAsync(int anio, int mes, bool force = false)
        {
            await InitAsync();
            var inicio = new DateTime(anio, mes, 1);
            var fin = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes)).Date.AddDays(1).AddTicks(-1);
            var transacciones = await _db.Table<Transaccion>().Where(t => t.Fecha >= inicio && t.Fecha <= fin).ToListAsync();
            var totalIngresos = transacciones.Where(t => t.Tipo == "Ingreso").Sum(t => t.Monto);
            var totalEgresos = transacciones.Where(t => t.Tipo == "Egreso" || t.Tipo == "Producto").Sum(t => t.Monto);
            var existente = await _db.Table<CierreCaja>()
                         .Where(c => c.FechaInicioPeriodo == inicio)
                         .FirstOrDefaultAsync();
            if (existente != null)
            {
                if (!force) return existente;
                existente.TotalIngresos = totalIngresos;
                existente.TotalEgresos = totalEgresos;
                existente.BalanceNeto = totalIngresos - totalEgresos;
                existente.FechaCierre = DateTime.Now;
                existente.UltimaModificacion = DateTime.Now;
                existente.IsSynced = false;
                await _db.UpdateAsync(existente);
                _ = _syncService.SincronizarCierresAsync();
                return existente;
            }
            var cierre = new CierreCaja
            {
                GuidHub = Guid.NewGuid().ToString(),
                FechaInicioPeriodo = inicio,
                FechaCierre = DateTime.Now,
                TotalIngresos = totalIngresos,
                TotalEgresos = totalEgresos,
                BalanceNeto = totalIngresos - totalEgresos,
                UltimaModificacion = DateTime.Now,
                IsSynced = false
            };
            await _db.InsertAsync(cierre);
            _ = _syncService.SincronizarCierresAsync();
            return cierre;
        }
        public async Task AutoClosePreviousMonthIfNeededAsync()
        {
            await InitAsync();
            try
            {
                var prev = DateTime.Now.AddMonths(-1);
                var inicio = new DateTime(prev.Year, prev.Month, 1);
                var fin = new DateTime(prev.Year, prev.Month, DateTime.DaysInMonth(prev.Year, prev.Month)).Date.AddDays(1).AddTicks(-1);
                var existe = await _db.Table<CierreCaja>()
                      .Where(c => c.FechaInicioPeriodo == inicio)
                      .FirstOrDefaultAsync();
                if (existe != null) return;
                var trans = await _db.Table<Transaccion>().Where(t => t.Fecha >= inicio && t.Fecha <= fin).ToListAsync();
                if (trans != null && trans.Count > 0)
                {
                    await GenerateAndSaveMonthlyClosureAsync(inicio.Year, inicio.Month);
                }
            }
            catch { }
        }
        public async Task EliminarTransaccionAsync(Transaccion t)
        {
            await InitAsync();
            t.IsDeleted = true;
            t.IsSynced = false;
            t.UltimaModificacion = DateTime.Now;
            await _db.UpdateAsync(t);
            _ = _syncService.SincronizarTransaccionesAsync();
        }
        public async Task<List<Transaccion>> GetTransaccionesPorRangoAsync(DateTime inicio, DateTime fin)
        {
            await InitAsync();
            DateTime finAjustado = fin.Date.AddDays(1).AddTicks(-1);
            return await _db.Table<Transaccion>().Where(t => t.Fecha >= inicio && t.Fecha <= finAjustado && !t.IsDeleted).OrderByDescending(t => t.Fecha).ToListAsync();
        }
        public async Task<int> GuardarCierreAsync(CierreCaja cierre)
        {
            await InitAsync();
            if (string.IsNullOrEmpty(cierre.GuidHub))
            cierre.GuidHub = Guid.NewGuid().ToString();
            cierre.UltimaModificacion = DateTime.Now;
            cierre.IsSynced = false;
            int resultado = await _db.InsertAsync(cierre);
            _ = _syncService.SincronizarCierresAsync();
            return resultado;
        }
    }
}