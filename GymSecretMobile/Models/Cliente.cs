using Plugin.Firebase.Firestore;
using Plugin.Firebase;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace GymSecretMobile.Models
{
    [Preserve(AllMembers = true)]
    public class Cliente : BaseModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [FirestoreProperty("NombreCompleto")]
        public string NombreCompleto { get; set; }
        [FirestoreProperty("planGuidHub")]
        public string PlanGuidHub { get; set; }
        [FirestoreProperty("Telefono")]
        public string Telefono { get; set; }
        [FirestoreProperty("PlanId")]
        public int PlanId { get; set; }
        [FirestoreProperty("ParejaId")]
        public int? ParejaId { get; set; }
        // 1. URL de la nube: Se sincroniza en Firestore y se guarda en SQLite
        [FirestoreProperty("fotoUrl")]
        public string FotoUrl { get; set; }
        // 2. Ruta local: Se guarda en SQLite, pero Firebase la ignora automáticamente al NO tener el atributo FirestoreProperty
        [Column("FotoLocalPath")]
        public string FotoLocalPath { get; set; }
        // Control de Suscripción
        [FirestoreProperty("AsistenciasConsumidas")]
        public int AsistenciasConsumidas { get; set; }
        [FirestoreProperty("AsistenciasTotales")]
        public int AsistenciasTotales { get; set; }
        [FirestoreProperty("EstaActivo")]
        public bool EstaActivo { get; set; }
        private DateTime _fechaRegistro;
        public DateTime FechaRegistro
        {
            get => _fechaRegistro;
            set
            {
                if (value.Year > 1900)
                    _fechaRegistro = value;
            }
        }
        [Ignore] 
        [FirestoreProperty("FechaRegistroTicks")] 
        public long FechaRegistroRespaldo
        {
            get => FechaRegistro.Ticks;
            set
            {
                if (value > 0)
                    FechaRegistro = new DateTime(value);
            }
        }
        private DateTime _fechaUltimoPago;
        public DateTime FechaUltimoPago
        {
            get => _fechaUltimoPago;
            set
            {
                if (value.Year > 1900)
                    _fechaUltimoPago = value;
            }
        }
        [Ignore]
        [FirestoreProperty("FechaUltimoPagoTicks")] 
        public long FechaUltimoPagoRespaldo
        {
            get => FechaUltimoPago.Ticks;
            set
            {
                if (value > 0)
                    FechaUltimoPago = new DateTime(value);
            }
        }
        private DateTime _fechaVencimiento;
        public DateTime FechaVencimiento
        {
            get => _fechaVencimiento;
            set
            {
                if (value.Year > 1900)
                    _fechaVencimiento = value;
            }
        }
        [Ignore] 
        [FirestoreProperty("FechaVencimientoTicks")] 
        public long FechaVencimientoRespaldo
        {
            get => FechaVencimiento.Ticks;
            set
            {
                if (value > 0)
                    FechaVencimiento = new DateTime(value);
            }
        }
        [Ignore]
        public Plan Plan { get; set; }
        [Ignore] 
        public int DiasRestantes => AsistenciasTotales - AsistenciasConsumidas;
        [Ignore]
        public Color BotonAsistenciaColor => PuedeRegistrarAsistencia ? Colors.Black : Colors.LightGray;
        [Ignore]
        public bool PuedeRegistrarAsistencia => EstaActivo && DiasRestantes > 0 && FechaVencimiento.Date >= DateTime.Now.Date;
        [Ignore]
        public string EstadoTexto => EstaActivo && FechaVencimiento.Date >= DateTime.Now.Date ? "Activo" : "Inactivo";
        [Ignore]
        public Color EstadoColor => EstadoTexto == "Activo" ? Colors.Green : Colors.Red;
    }
}
