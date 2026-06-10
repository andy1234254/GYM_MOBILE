using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymSecretMobile.Models
{
    public class Transaccion : BaseModel
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
        [FirestoreProperty("monto")]
        public double Monto { get; set; }
        [FirestoreProperty("tipo")]
        public string Tipo { get; set; }
        [FirestoreProperty("concepto")]
        public string Concepto { get; set; }
        public int? ClienteId { get; set; }
        [FirestoreProperty("clienteGuidHub")]
        public string ClienteGuidHub { get; set; }
        [SQLite.Ignore]
        public Color ColorMonto => Tipo == "Ingreso" ? Color.FromArgb("#27AE60") : Color.FromArgb("#E74C3C");
        [SQLite.Ignore]
        public string SignoMonto => Tipo == "Ingreso" ? "+" : "-";
    }
}

