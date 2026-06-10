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
    public class Plan : BaseModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [FirestoreProperty("nombre")]
        public string Nombre { get; set; }
        [FirestoreProperty("precio")]
        public double Precio { get; set; }
        [FirestoreProperty("activaPareja")]
        public bool ActivaPareja { get; set; }
        [FirestoreProperty("diasDuracion")]
        public int DiasDuracion { get; set; }
    }
}
