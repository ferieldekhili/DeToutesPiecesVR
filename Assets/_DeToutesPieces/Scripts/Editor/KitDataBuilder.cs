using DeToutesPieces.Data;
using UnityEditor;
using UnityEngine;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Définitions du kit KNARREVIK d'après la notice AA-2547698-1 (08/08/2024) :
    /// 4 pieds, 1 tablette inférieure, 1 plateau (p. 1 et 12) ; 16 vis réf. 10118490 / 10118469 et
    /// 1 clé Allen réf. 100006 (p. 6). La notice ne donne aucune dimension de vis : longueur laissée à 0.
    /// </summary>
    public static class KitDataBuilder
    {
        public const string PartsFolder = "Assets/_DeToutesPieces/Data/Parts";
        public const string FastenersFolder = "Assets/_DeToutesPieces/Data/Fasteners";

        public static PartDefinition Leg => Load<PartDefinition>(PartsFolder + "/Part_Pied.asset");
        public static PartDefinition LowerShelf => Load<PartDefinition>(PartsFolder + "/Part_TabletteInferieure.asset");
        public static PartDefinition Top => Load<PartDefinition>(PartsFolder + "/Part_Plateau.asset");
        public static FastenerDefinition KitScrew => Load<FastenerDefinition>(FastenersFolder + "/Fastener_VisKit.asset");

        [MenuItem("De Toutes Pièces/Mettre à jour les données du kit KNARREVIK")]
        public static void EnsureKitData()
        {
            // Pièce provisoire de la première version, absente du kit réel.
            const string obsolete = PartsFolder + "/Part_CadreLateral.asset";
            if (AssetDatabase.LoadAssetAtPath<PartDefinition>(obsolete) != null) AssetDatabase.DeleteAsset(obsolete);

            UpsertPart("Part_Pied", "KNV_PIED", "Pied",
                "Tube métallique carré. Trous de fixation sur deux faces voisines, en haut (plateau) et au niveau de la tablette. Embout plastique côté sol.", 4);
            UpsertPart("Part_TabletteInferieure", "KNV_TABLETTE", "Tablette inférieure",
                "Tablette métallique à bords repliés ; les pieds s'y vissent en premier (étapes 1 à 3).", 1);
            UpsertPart("Part_Plateau", "KNV_PLATEAU", "Plateau",
                "Plateau métallique à bords repliés ; vissé en haut des pieds (étapes 4 et 5).", 1);

            string screwPath = FastenersFolder + "/Fastener_VisKit.asset";
            var f = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(screwPath);
            if (f == null)
            {
                f = ScriptableObject.CreateInstance<FastenerDefinition>();
                AssetDatabase.CreateAsset(f, screwPath);
            }
            f.reference = "10118490 / 10118469";
            f.displayName = "Vis à tête six pans creux";
            f.quantityInKit = 16;
            f.lengthMm = 0f;   // non indiquée par la notice
            f.diameterMm = 0f; // non indiqué par la notice
            f.identificationColor = new Color(1f, 0.8f, 0.1f);
            f.verifiedAgainstNotice = true; // référence et quantité vérifiées (p. 6) ; dimensions inconnues
            EditorUtility.SetDirty(f);

            AssetDatabase.SaveAssets();
        }

        private static void UpsertPart(string file, string id, string display, string description, int qty)
        {
            string path = $"{PartsFolder}/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<PartDefinition>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<PartDefinition>();
                AssetDatabase.CreateAsset(p, path);
            }
            p.partId = id;
            p.displayName = display;
            p.description = description;
            p.quantityInKit = qty;
            p.noticeReference = "AA-2547698-1 (p. 1, 7-12)";
            p.verifiedAgainstNotice = true;
            EditorUtility.SetDirty(p);
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
