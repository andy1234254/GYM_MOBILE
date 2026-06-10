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
    public class Asistencia : BaseModel
    {
        [PrimaryKey, AutoIncrement]
        [FirestoreProperty("idLocal")]
        public int Id { get; set; }

        public int ClienteId { get; set; }

        [FirestoreProperty("clienteGuidHub")]
        public string ClienteGuidHub { get; set; }

        //Manejo de FechaHora con respaldo en ticks para Firestore
        private DateTime _fechaHora;
        public DateTime FechaHora
        {
            get => _fechaHora;
            set
            {
                if (value.Year > 1900)
                    _fechaHora = value;
            }
        }
        [SQLite.Ignore]
        [FirestoreProperty("fechaHoraTicks")]
        public long FechaHoraRespaldo
        {
            get => FechaHora.Ticks;
            set
            {
                if (value > 0)
                    FechaHora = new DateTime(value);
            }
        }
    }
}
