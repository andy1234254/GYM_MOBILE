using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymSecretMobile.Models
{
    public class CierreCaja : BaseModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        private DateTime _fechaCierre;
        public DateTime FechaCierre
        {
            get => _fechaCierre;
            set { if (value.Year > 1900) _fechaCierre = value; }
        }
        [SQLite.Ignore]
        [FirestoreProperty("fechaCierreTicks")]
        public long FechaCierreTicks
        {
            get => FechaCierre.Ticks;
            set { if (value > 0) FechaCierre = new DateTime(value); }
        }
        private DateTime _fechaInicioPeriodo;
        public DateTime FechaInicioPeriodo
        {
            get => _fechaInicioPeriodo;
            set { if (value.Year > 1900) _fechaInicioPeriodo = value; }
        }
        [SQLite.Ignore]
        [FirestoreProperty("fechaInicioPeriodoTicks")]
        public long FechaInicioPeriodoTicks
        {
            get => FechaInicioPeriodo.Ticks;
            set { if (value > 0) FechaInicioPeriodo = new DateTime(value); }
        }
        [FirestoreProperty("totalIngresos")]
        public double TotalIngresos { get; set; }

        [FirestoreProperty("totalEgresos")]
        public double TotalEgresos { get; set; }

        [FirestoreProperty("balanceNeto")]
        public double BalanceNeto { get; set; }
    }
}
