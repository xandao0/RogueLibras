using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Screen Space - Overlay canvases always paint over the Scene view, which makes
// the battlefield impossible to arrange while the hand and the HUD are in the
// way. These toggles switch a canvas off for editing and back on for playing;
// they only touch Canvas.enabled, so nothing about the layout is disturbed.
//
// Tools > RogueLibras > ... (or the shortcuts below).
public static class SceneViewUIToggles
{
    private const string Menu = "Tools/RogueLibras/";

    [MenuItem(Menu + "Toggle Battle UI in Scene _%#u")]   // Ctrl/Cmd + Shift + U
    private static void ToggleBattleUI()
    {
        Toggle(new[] { "GameUI", "UserUI" }, "Battle UI");
    }

    [MenuItem(Menu + "Toggle Settings Panel in Scene")]
    private static void ToggleSettings()
    {
        var panel = Object.FindObjectOfType<SettingsPanel>(true);
        if (panel == null) { Debug.Log("[RogueLibras] No SettingsPanel in this scene."); return; }

        var backdrop = panel.transform.Find("Backdrop");
        if (backdrop == null) { Debug.Log("[RogueLibras] SettingsPanel has no Backdrop child."); return; }

        Undo.RecordObject(backdrop.gameObject, "Toggle Settings Panel");
        bool on = !backdrop.gameObject.activeSelf;
        backdrop.gameObject.SetActive(on);
        EditorUtility.SetDirty(backdrop.gameObject);
        Debug.Log("[RogueLibras] Settings panel " + (on ? "shown" : "hidden") + " in the Scene view.");
    }

    [MenuItem(Menu + "Show All UI in Scene")]
    private static void ShowAll()
    {
        SetAll(true);
    }

    [MenuItem(Menu + "Hide All UI in Scene")]
    private static void HideAll()
    {
        SetAll(false);
    }

    private static void SetAll(bool on)
    {
        int n = 0;
        foreach (var c in Object.FindObjectsOfType<Canvas>(true))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;   // world-space UI belongs in the scene
            Undo.RecordObject(c, on ? "Show UI" : "Hide UI");
            c.enabled = on;
            EditorUtility.SetDirty(c);
            n++;
        }
        Debug.Log("[RogueLibras] " + (on ? "Showed " : "Hid ") + n + " overlay canvas(es) in the Scene view.");
    }

    private static void Toggle(string[] names, string label)
    {
        bool anyOn = false;
        var found = new System.Collections.Generic.List<Canvas>();

        foreach (var c in Object.FindObjectsOfType<Canvas>(true))
        {
            foreach (var n in names)
            {
                if (c.gameObject.name != n) continue;
                found.Add(c);
                if (c.enabled) anyOn = true;
            }
        }

        if (found.Count == 0) { Debug.Log("[RogueLibras] No " + label + " canvas in this scene."); return; }

        foreach (var c in found)
        {
            Undo.RecordObject(c, "Toggle " + label);
            c.enabled = !anyOn;
            EditorUtility.SetDirty(c);
        }
        Debug.Log("[RogueLibras] " + label + " " + (!anyOn ? "shown" : "hidden") + " in the Scene view.");
    }
}
