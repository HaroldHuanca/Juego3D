
using UnityEngine;

public class ObjetoInteractuable : MonoBehaviour
{
    [Header("Definición del objeto")]
    [SerializeField] private DefinicionObjeto definicion;

    [Header("Cantidad al recoger")]
    [Min(1)]
    [SerializeField] private int cantidad = 1;

    [Header("Estado")]
    [SerializeField] private bool recolectable = true;

    public DefinicionObjeto Definicion => definicion;

    public string IdObjeto =>
        definicion != null ? definicion.IdObjeto : "";

    public string NombreObjeto =>
        definicion != null ? definicion.NombreObjeto : name;

    public string Descripcion =>
        definicion != null ? definicion.Descripcion : "";

    public Sprite Icono =>
        definicion != null ? definicion.Icono : null;

    public int Cantidad => cantidad;

    public bool Acumulable =>
        definicion != null && definicion.Acumulable;

    public bool Recolectable => recolectable;

    public void Recoger()
    {
        if (!recolectable)
            return;

        recolectable = false;
        Destroy(gameObject);
    }

    void OnValidate()
    {
        cantidad = Mathf.Max(1, cantidad);
    }
}
