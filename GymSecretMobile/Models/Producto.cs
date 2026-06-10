using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace GymSecretMobile.Models
{
    public class Producto : BaseModel, INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [FirestoreProperty("nombre")]
        public string Nombre { get; set; }
        [FirestoreProperty("precioVenta")]
        public double PrecioVenta { get; set; }
        [FirestoreProperty("fotoUrl")]
        public string FotoUrl { get; set; }
        [Column("FotoLocalPath")]
        public string FotoLocalPath { get; set; }
        private int _cantidadDisponible = 0;
        [FirestoreProperty("cantidadDisponible")]
        public int CantidadDisponible
        {
            get => _cantidadDisponible;
            set
            {
                if (_cantidadDisponible != value)
                {
                    _cantidadDisponible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorStock));
                }
            }
        }
        [SQLite.Ignore]
        public Color ColorStock => CantidadDisponible <= 0 ? Colors.Red :
                                   (CantidadDisponible <= 5 ? Colors.Orange : Colors.Green);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        [SQLite.Ignore]
        public ImageSource ImagenMostrar
        {
            get
            {
                if (!string.IsNullOrEmpty(FotoLocalPath) && System.IO.File.Exists(FotoLocalPath))
                {
                    return ImageSource.FromFile(FotoLocalPath);
                }
                if (!string.IsNullOrEmpty(FotoUrl))
                {
                    return ImageSource.FromUri(new Uri(FotoUrl));
                }
                return null;
            }
        }
    }
}
