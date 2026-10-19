using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeToutesPieces.Core
{
    /// <summary>
    /// Étape de montage, alignée sur une étape numérotée de la notice KNARREVIK (AA-2547698-1).
    /// </summary>
    [Serializable]
    public class AssemblyStep
    {
        public string title = "Étape";

        [TextArea(2, 5)]
        public string instruction;

        [Tooltip("Page de la notice IKEA correspondante (affichée dans la notice virtuelle).")]
        public int noticePage;

        [Tooltip("Nombre de vis prévu par la notice pour ce groupe de fixations (2, 2, 4, 4, 4 d'après le document V1).")]
        public int expectedScrewCount;

        [Tooltip("Orientation (lacet, degrés) du support tournant au début de l'étape : transcrit les retournements de la notice.")]
        public float turntableYaw;

        [Tooltip("Si vrai, le support tourne automatiquement pour présenter face à l'utilisateur l'opération désignée par la flèche.")]
        public bool turntableFollowsGuidance;

        [Tooltip("Conditions à remplir, dans l'ordre suggéré par la flèche de guidage.")]
        public List<StepRequirement> requirements = new List<StepRequirement>();

        [TextArea(1, 3)]
        [Tooltip("Indices progressifs (texte). Niveau 2 = surbrillance, niveau 3 = animation.")]
        public List<string> textHints = new List<string>();

        [Tooltip("Faux tant que le contenu de l'étape n'a pas été vérifié sur la notice papier.")]
        public bool verifiedAgainstNotice;

        public int SatisfiedCount
        {
            get
            {
                int n = 0;
                foreach (var r in requirements) if (r != null && r.IsSatisfied) n++;
                return n;
            }
        }

        public int RequirementCount
        {
            get
            {
                int n = 0;
                foreach (var r in requirements) if (r != null) n++;
                return n;
            }
        }

        public bool IsComplete => RequirementCount > 0 && SatisfiedCount == RequirementCount;

        public StepRequirement FirstUnsatisfied
        {
            get
            {
                foreach (var r in requirements) if (r != null && !r.IsSatisfied) return r;
                return null;
            }
        }
    }
}
