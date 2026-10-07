using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Editor-only helper: one click that runs all the separate setup steps in the right order.
// Run it from the menu bar: Tools > Sim Racing > Quick Setup
public static class QuickSetup
{
    [MenuItem("Tools/Sim Racing/Quick Setup/Formula Car on Americas Circuit")]
    private static void FormulaOnAmericas()
    {
        Run(CircuitSetup.SetupAmericasCircuit);
    }

    [MenuItem("Tools/Sim Racing/Quick Setup/Formula Car on Marina Bay Circuit")]
    private static void FormulaOnMarina()
    {
        Run(CircuitSetup.SetupMarinaCircuit);
    }

    private static void Run(System.Action buildCircuit)
    {
        if (CarSetup.BlockedByPlayMode()) return;

        // 1. Camera, then the circuit (which also puts the car on the start line).
        CarSetup.SetupCameraFollow();
        buildCircuit();

        // 2. The on-screen display needs TextMeshPro's fonts. Import them first if this project
        //    has never had them, and carry on automatically once the import has finished.
        if (!FontsReady())
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            giveUpTime = EditorApplication.timeSinceStartup + 90.0;
            EditorApplication.update += WaitForFonts;
            return;
        }
        FinishSetup();
    }

    private const string FontSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private static double giveUpTime;

    private static bool FontsReady()
    {
        return File.Exists(FontSettingsPath) && AssetDatabase.LoadAssetAtPath<TMP_Settings>(FontSettingsPath) != null;
    }

    // Called by the editor many times a second until the fonts have arrived (or 90 seconds have passed).
    private static void WaitForFonts()
    {
        bool ready = FontsReady() && !EditorApplication.isUpdating;
        if (!ready && EditorApplication.timeSinceStartup < giveUpTime) return;

        EditorApplication.update -= WaitForFonts;
        if (ready) FinishSetup();
        else Debug.LogError("TextMeshPro's fonts did not import. Click Window > TextMeshPro > Import TMP Essential Resources, then run Quick Setup again.");
    }

    // 3. Lap display, dashboard, and last the car (so its 360 km/h scale lands on the new speedometer).
    private static void FinishSetup()
    {
        CarSetup.SetupRaceUI();
        DriveSetup.SetupDashboard();
        CarSetup.SetupFormulaCarModel();
        Debug.Log("Quick setup finished. Press Ctrl+S, then Play.");
    }
}
