
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ElementoInventarioUI : MonoBehaviour
{
    [Header("Elementos")]
    [SerializeField] private Image imagenIcono;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoCantidad;

    public void Configurar(EntradaInventario objeto)
    {
        if (imagenIcono != null)
        {
            imagenIcono.sprite = objeto.icono;
            imagenIcono.enabled = objeto.icono != null;
        }

        if (textoNombre != null)
            textoNombre.text = objeto.nombre;

        if (textoCantidad != null)
        {
            textoCantidad.text = objeto.acumulable
                ? "x" + objeto.cantidad
                : "";
        }
    }
}
