
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EntradaInventario
{
    public DefinicionObjeto definicion;

    // Datos utilizados por la UI actual.
    public string idObjeto;
    public string nombre;
    public string descripcion;
    public Sprite icono;
    public int cantidad;
    public bool acumulable;
}

public class InventarioJugador : MonoBehaviour
{
    [Header("Objetos almacenados")]
    [SerializeField]
    private List<EntradaInventario> objetos =
        new List<EntradaInventario>();

    public IReadOnlyList<EntradaInventario> Objetos => objetos;

    public event Action InventarioCambiado;

    public bool AgregarObjeto(ObjetoInteractuable objeto)
    {
        if (objeto == null || !objeto.Recolectable)
            return false;

        DefinicionObjeto definicion = objeto.Definicion;

        if (definicion == null ||
            string.IsNullOrWhiteSpace(definicion.IdObjeto))
        {
            Debug.LogWarning("El objeto no tiene una definición válida.");
            return false;
        }

        if (definicion.Acumulable)
        {
            EntradaInventario existente = objetos.Find(
                x => x.idObjeto == definicion.IdObjeto
            );

            if (existente != null)
            {
                existente.cantidad += objeto.Cantidad;
                InventarioCambiado?.Invoke();
                return true;
            }
        }

        EntradaInventario nuevaEntrada = new EntradaInventario
        {
            definicion = definicion,
            idObjeto = definicion.IdObjeto,
            nombre = definicion.NombreObjeto,
            descripcion = definicion.Descripcion,
            icono = definicion.Icono,
            cantidad = objeto.Cantidad,
            acumulable = definicion.Acumulable
        };

        objetos.Add(nuevaEntrada);

        InventarioCambiado?.Invoke();

        return true;
    }

    public EntradaInventario ObtenerObjetoEnIndice(int indice)
    {
        if (indice < 0 || indice >= objetos.Count)
            return null;

        return objetos[indice];
    }

    public bool Contiene(EntradaInventario entrada)
    {
        return entrada != null && objetos.Contains(entrada);
    }

    public bool QuitarCantidad(
        EntradaInventario entrada,
        int cantidad)
    {
        if (entrada == null ||
            cantidad <= 0 ||
            !objetos.Contains(entrada))
        {
            return false;
        }

        if (entrada.cantidad < cantidad)
            return false;

        entrada.cantidad -= cantidad;

        if (entrada.cantidad <= 0)
            objetos.Remove(entrada);

        InventarioCambiado?.Invoke();

        return true;
    }

    public void MostrarInventario()
    {
        Debug.Log("===== INVENTARIO =====");

        foreach (EntradaInventario objeto in objetos)
        {
            Debug.Log(
                $"{objeto.nombre} | Cantidad: {objeto.cantidad}"
            );
        }
    }
}
