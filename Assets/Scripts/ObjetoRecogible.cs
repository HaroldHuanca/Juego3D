using UnityEngine;
using UnityEngine.Events;

/// <summary>Se añade solamente a los objetos que el jugador puede recoger.</summary>
public class ObjetoRecogible : MonoBehaviour
{
    public string nombreObjeto = "Objeto";
    public UnityEvent alRecoger = new UnityEvent();
    public bool Recogido { get; private set; }

    public void Recoger()
    {
        if (Recogido || !isActiveAndEnabled) return;
        Recogido = true;
        alRecoger.Invoke();
        gameObject.SetActive(false);
    }
}
