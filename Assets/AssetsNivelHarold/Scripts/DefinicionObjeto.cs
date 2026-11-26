
using UnityEngine;

[CreateAssetMenu(
    fileName = "NuevoObjeto",
    menuName = "Juego/Inventario/Definicion de objeto"
)]
public class DefinicionObjeto : ScriptableObject
{
    [Header("Identificación")]
    [SerializeField] private string idObjeto;
    [SerializeField] private string nombreObjeto;
    [TextArea(2, 4)]
    [SerializeField] private string descripcion;

    [Header("Representación")]
    [SerializeField] private Sprite icono;
    [SerializeField] private GameObject prefabVisual;

    [Header("Inventario")]
    [SerializeField] private bool acumulable;

    [Header("Ajustes de equipamiento")]
    [SerializeField] private Vector3 rotacionEquipamiento;

    public Vector3 RotacionEquipamiento => rotacionEquipamiento;

    public string IdObjeto => idObjeto;
    public string NombreObjeto => nombreObjeto;
    public string Descripcion => descripcion;
    public Sprite Icono => icono;
    public GameObject PrefabVisual => prefabVisual;
    public bool Acumulable => acumulable;
}
