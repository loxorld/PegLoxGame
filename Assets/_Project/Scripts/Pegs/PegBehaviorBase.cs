using UnityEngine;

public abstract class PegBehaviorBase : ScriptableObject
{
    /// <summary>
    /// Permite inicializar estado runtime del peg al arrancar un encounter.
    /// </summary>
    public virtual void OnResetForEncounter(Peg peg) { }

    /// <summary>
    /// Se llama cuando la bola golpea el peg.
    /// Devuelve true si el peg debe consumirse en este hit.
    /// </summary>
    public abstract bool OnBallHit(Peg peg);
}
