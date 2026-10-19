using UnityEngine;

namespace DeToutesPieces.Workshop
{
    /// <summary>
    /// Désactive l'objet hors de l'éditeur (ex. simulateur XR utilisé pour tester sans casque).
    /// </summary>
    public class EditorOnlyObject : MonoBehaviour
    {
        private void Awake()
        {
            if (!Application.isEditor) gameObject.SetActive(false);
        }
    }
}
