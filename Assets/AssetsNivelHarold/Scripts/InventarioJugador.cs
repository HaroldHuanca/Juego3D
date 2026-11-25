
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EntradaInventario
{
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
    private List<EntradaInventario> objetos = new();

    public IReadOnlyList<EntradaInventario> Objetos => objetos;

    public event Action InventarioCambiado;

    public bool AgregarObjeto(ObjetoInteractuable objeto)
    {
        if (objeto == null || !objeto.Recolectable)
            return false;

        if (string.IsNullOrWhiteSpace(objeto.IdObjeto))
        {
            Debug.LogWarning("El objeto no tiene ID.");
            return false;
        }

        if (objeto.Acumulable)
        {
            EntradaInventario existente = objetos.Find(
                x => x.idObjeto == objeto.IdObjeto
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
            idObjeto = objeto.IdObjeto,
            nombre = objeto.NombreObjeto,
            descripcion = objeto.Descripcion,
            icono = objeto.Icono,
            cantidad = objeto.Cantidad,
            acumulable = objeto.Acumulable
        };

        objetos.Add(nuevaEntrada);

        InventarioCambiado?.Invoke();

        return true;
    }

    public void MostrarInventario()
    {
        foreach (EntradaInventario objeto in objetos)
        {
            Debug.Log(
                $"{objeto.nombre} | Cantidad: {objeto.cantidad}"
            );
        }
    }
}
