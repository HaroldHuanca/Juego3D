using System;
using System.Collections.Generic;
using UnityEngine;
using Tienda.Productos;

namespace Tienda.SistemaVentas
{
    [Serializable]
    public class LineaPedido
    {
        public ProductoData producto;
        [Min(1)] public int cantidad = 1;

        public float Subtotal => producto != null ? producto.Precio * cantidad : 0f;

        public LineaPedido(ProductoData producto, int cantidad = 1)
        {
            this.producto = producto;
            this.cantidad = cantidad;
        }
    }

    [Serializable]
    public class PedidoCliente
    {
        public List<LineaPedido> lineas = new List<LineaPedido>();
        public float billeteEntregado; // $5, $10, $20, $50, etc.

        public float TotalPagar
        {
            get
            {
                float total = 0f;
                foreach (var linea in lineas)
                {
                    total += linea.Subtotal;
                }
                return (float)Math.Round(total, 2);
            }
        }

        public float VueltoRequerido
        {
            get
            {
                float vuelto = billeteEntregado - TotalPagar;
                return vuelto > 0f ? (float)Math.Round(vuelto, 2) : 0f;
            }
        }

        /// <summary>
        /// Comprueba si el vuelto entregado por el jugador coincide con el vuelto exacto (margen de 1 centavo por redondeo flotante).
        /// </summary>
        public bool EsVueltoCorrecto(float vueltoEntregado)
        {
            return Mathf.Abs(vueltoEntregado - VueltoRequerido) < 0.01f;
        }
    }
}
