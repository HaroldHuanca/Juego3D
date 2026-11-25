
using UnityEngine;

public class ObjetoInteractuable : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField] private string idObjeto;
    [SerializeField] private string nombreObjeto;
    [TextArea(2, 4)]
    [SerializeField] private string descripcion;

    [Header("Presentación")]
    [SerializeField] private Sprite icono;

    [Header("Inventario")]
    [Min(1)]
    [SerializeField] private int cantidad = 1;

    [SerializeField] private bool acumulable = false;

    [Header("Estado")]
    [SerializeField] private bool recolectable = true;

    public string IdObjeto => idObjeto;
    public string NombreObjeto => nombreObjeto;
    public string Descripcion => descripcion;
    public Sprite Icono => icono;
    public int Cantidad => cantidad;
    public bool Acumulable => acumulable;
    public bool Recolectable => recolectable;

    public void Recoger()
    {
        if (!recolectable)
            return;

        recolectable = false;
        Destroy(gameObject);
    }
}
