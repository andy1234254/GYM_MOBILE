using Plugin.Firebase.Firestore;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Runtime.CompilerServices;

namespace GymSecretMobile.Models
{
    public class DetalleVenta : BaseModel, INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int VentaId { get; set; }
        public int ProductoId { get; set; }
        [FirestoreProperty("productoGuidHub")]
        public string ProductoGuidHub { get; set; }
        [SQLite.Ignore]
        public int StockDisponible { get; set; }

        private int _cantidadVendida;
        [FirestoreProperty("cantidadVendida")]
        public int CantidadVendida
        {
            get => _cantidadVendida;
            set
            {
                if (_cantidadVendida != value)
                {
                    _cantidadVendida = value;
                    OnPropertyChanged(); // Avisa a la UI que la cantidad cambió
                    OnPropertyChanged(nameof(Subtotal)); // Avisa que el subtotal debe recalcularse
                }
            }
        }

        private double _subtotal;
        [FirestoreProperty("subtotal")]
        public double Subtotal
        {
            get => CantidadVendida * PrecioUnitario;
            set => _subtotal = value; // Permite que el serializador de Firestore escriba
        }
        [SQLite.Ignore]
        public string NombreProducto { get; set; }
        [SQLite.Ignore]
        public double PrecioUnitario { get; set; }
        [SQLite.Ignore]
        public string FotoRuta { get; set; }
        [SQLite.Ignore]
        public ImageSource ImagenMostrar { get; set; }
        // Evento necesario para que la UI se actualice automáticamente
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
