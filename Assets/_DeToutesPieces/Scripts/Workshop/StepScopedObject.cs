using DeToutesPieces.Core;
using UnityEngine;

namespace DeToutesPieces.Workshop
{
    /// <summary>
    /// Active un objet uniquement pendant une plage d'étapes (ex. cales de maintien de la tablette
    /// tant que les quatre pieds ne sont pas fixés). Indices d'étape à partir de 0.
    /// </summary>
    public class StepScopedObject : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private int fromStep;
        [SerializeField] private int toStep;

        private void Start()
        {
            if (target == null && transform.childCount > 0) target = transform.GetChild(0).gameObject;
            var m = AssemblyManager.Instance;
            if (m == null) return;
            m.StepStarted += (i, s) => Apply(i);
            m.AssemblyCompleted += () => Apply(int.MaxValue);
            Apply(m.CurrentStepIndex);
        }

        private void Apply(int index)
        {
            if (target != null) target.SetActive(index >= fromStep && index <= toStep);
        }
    }
}
