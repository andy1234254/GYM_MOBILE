using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymSecretMobile.Models
{
    public class Venta : BaseModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        private DateTime _fecha;
        public DateTime Fecha
        {
            get => _fecha;
            set { if (value.Year > 1900) _fecha = value; }
        }
        [SQLite.Ignore]
        [FirestoreProperty("fechaTicks")]
        public long FechaTicks
        {
            get => Fecha.Ticks;
            set { if (value > 0) Fecha = new DateTime(value); }
        }
        [FirestoreProperty("totalVenta")]
        public double TotalVenta { get; set; }
        [SQLite.Ignore]
        [FirestoreProperty("detalles")]
        public List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }
}
