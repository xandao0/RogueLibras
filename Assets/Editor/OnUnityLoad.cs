using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

    [InitializeOnLoad]
public class OnUnityLoad
{
    private static string scenePath
    {
        get { return EditorPrefs.GetString("LoaderScene"); }
        set {EditorPrefs.SetString("LoeaderScene", value);}
    }
    
    static OnUnityLoad()
    {
        EditorApplication.playmodeStateChanged = () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    //Console debug
                    Debug.Log("Auto-Saved opened scenes before entering Play mode");
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveOpenScenes();
                }
            }
        };
    }
}
