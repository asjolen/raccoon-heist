using UnityEditor;
using UnityEngine;

namespace RaccoonHeist.World.Editor
{
    public sealed class EnvironmentReviewWindow : EditorWindow
    {
        [MenuItem("Raccoon Heist/Environment Review")]
        static void Open()
        {
            var window = GetWindow<EnvironmentReviewWindow>("Environment Review");
            window.minSize = new Vector2(340f, 344f);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Raccoon Heist Environment Review", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Regenerate the code-owned scene, capture the authored route views, "
                + "inspect a 32-angle district sweep, and audit all 12 player-facing perimeter sightlines.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Generate + Capture Everything", GUILayout.Height(34f)))
                ShopGreyboxGenerator.GenerateSaveAndCapture();

            if (GUILayout.Button("Capture Route Views", GUILayout.Height(28f)))
                ShopGreyboxGenerator.CaptureEnvironmentPreviews();

            if (GUILayout.Button("Capture 32-Angle Perimeter Sweep", GUILayout.Height(28f)))
                ShopGreyboxGenerator.CapturePerimeterSweep();

            if (GUILayout.Button("Capture 12-Direction Sightline Audit", GUILayout.Height(28f)))
                ShopGreyboxGenerator.CapturePerimeterSightlineAudit();

            if (GUILayout.Button("Capture Synty City Reference Views", GUILayout.Height(28f)))
                ShopGreyboxGenerator.CaptureSyntyCityReferenceViews();

            if (GUILayout.Button("Capture Synty Building Catalog", GUILayout.Height(28f)))
                ShopGreyboxGenerator.CaptureSyntyBuildingCatalog();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Reveal Captures in Finder"))
                EditorUtility.RevealInFinder("/tmp/RaccoonHeist_Block.png");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Files: /tmp/RaccoonHeist_*.png",
                EditorStyles.miniLabel);
        }
    }
}
