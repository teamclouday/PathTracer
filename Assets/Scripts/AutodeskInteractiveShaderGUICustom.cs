using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class AutodeskInteractiveShaderGUICustom : MaterialEditor
{
    public override void OnInspectorGUI()
    {
        // render the default inspector
        base.OnInspectorGUI();

        //// if we are not visible... return
        //if (!isVisible)
        //    return;

        //// get the current keywords from the material
        //Material targetMat = target as Material;
        //string[] keyWords = targetMat.shaderKeywords;

        //// see if redify is set, then show a checkbox
        //bool redify = keyWords.Contains("REDIFY_ON");
        //EditorGUI.BeginChangeCheck();
        //redify = EditorGUILayout.Toggle("Redify material", redify);
        //if (EditorGUI.EndChangeCheck())
        //{
        //    // if the checkbox is changed, reset the shader keywords
        //    var keywords = new List<string> { redify ? "REDIFY_ON" : "REDIFY_OFF" };
        //    targetMat.shaderKeywords = keywords.ToArray();
        //    EditorUtility.SetDirty(targetMat);
        //}
    }
}
