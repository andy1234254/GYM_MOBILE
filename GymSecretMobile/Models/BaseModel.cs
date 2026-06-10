using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace GymSecretMobile.Models
{
    public class BaseModel
    {
        [FirestoreProperty("GuidHub")]
        public string GuidHub { get; set; } = Guid.NewGuid().ToString();

        public bool IsSynced { get; set; } = false;

        private DateTime _ultimaModificacion;

        public DateTime UltimaModificacion
        {
            get => _ultimaModificacion;
            set
            {
                if (value.Year > 1900)
                    _ultimaModificacion = value;
            }
        }

        [Ignore] 
        [FirestoreProperty("UltimaModificacionTicks")] 
        public long UltimaModificacionRespaldo
        {
            get => UltimaModificacion.Ticks;
            set
            {
                if (value > 0)
                    UltimaModificacion = new DateTime(value);
            }
        }
        [FirestoreProperty("IsDeleted")]
        public bool IsDeleted { get; set; } = false;
    }
}