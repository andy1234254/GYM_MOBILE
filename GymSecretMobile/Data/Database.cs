using GymSecretMobile.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymSecretMobile.Data
{
    public class Database
    {
        readonly SQLiteAsyncConnection _database;

        public Database(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            // 1. Creamos todas las tablas 
            _database.CreateTableAsync<Cliente>().Wait();
            _database.CreateTableAsync<Plan>().Wait();
            _database.CreateTableAsync<Transaccion>().Wait();
            _database.CreateTableAsync<CierreCaja>().Wait();
            _database.CreateTableAsync<Producto>().Wait();
            _database.CreateTableAsync<Venta>().Wait();        
            _database.CreateTableAsync<DetalleVenta>().Wait();
            _database.CreateTableAsync<Asistencia>().Wait();
        }
        // Métodos para el Stock/Productos
        public Task<List<Producto>> GetProductosAsync() =>
            _database.Table<Producto>().OrderBy(p => p.Nombre).ToListAsync();
        public Task<int> SaveProductoAsync(Producto prod) =>
            (prod.Id != 0) ? _database.UpdateAsync(prod) : _database.InsertAsync(prod);
        public Task<int> DeleteProductoAsync(Producto prod) =>
            _database.DeleteAsync(prod);
        public Task<Venta> GetVentaAsync(int id) =>
            _database.Table<Venta>().Where(v => v.Id == id).FirstOrDefaultAsync();
        public Task<List<DetalleVenta>> GetDetallesVentaAsync(int ventaId) =>
            _database.Table<DetalleVenta>().Where(d => d.VentaId == ventaId).ToListAsync();
    }
}
