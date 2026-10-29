using System.Collections.Generic;
using UnityEngine;

/// <summary>Intensidad VHS ligada a la mezcla del ambiente, solo para el jugador.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class VHSCamara : MonoBehaviour
{
    public AmbienteSonoro ambiente;
    public Camera camaraBrazos;
    [Range(0f, 1f)] public float intensidadExterior = 0.12f;
    [Range(0f, 1f)] public float intensidadInterior = 0.3f;
    private Camera camara;
    internal static readonly List<VHSCamara> Activas = new List<VHSCamara>();
    public float Intensidad => Mathf.Lerp(intensidadExterior, intensidadInterior,
        ambiente != null && ambiente.isActiveAndEnabled ? ambiente.MezclaInterior : 0f);

    private void Awake() { camara = GetComponent<Camera>(); }
    private void OnEnable() { if (!Activas.Contains(this)) Activas.Add(this); }
    private void OnDisable() { Activas.Remove(this); }
    internal bool Aplica(Camera vista) => vista == camara || vista == camaraBrazos;
}
