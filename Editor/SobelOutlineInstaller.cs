using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class SobelOutlineInstaller
{
    [MenuItem("Tools/Sobel Outline/Add Feature To All URP Renderers")]
    public static void AddToAllRenderers()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        int added = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (AddFeature(renderer))
            {
                added++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(added > 0
            ? $"Sobel Outline: added the renderer feature to {added} URP renderer(s)."
            : "Sobel Outline: every URP renderer already has the feature.");
    }

    [MenuItem("Tools/Sobel Outline/Select Transparent Material")]
    public static void SelectTransparentMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("Mat_SobelTransparent t:Material");
        if (guids.Length == 0)
        {
            Debug.LogWarning("Sobel Outline: Mat_SobelTransparent was not found.");
            return;
        }

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static bool AddFeature(UniversalRendererData renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            if (renderer.rendererFeatures[i] is SobelOutlineFeature)
            {
                return false;
            }
        }

        SobelOutlineFeature feature = ScriptableObject.CreateInstance<SobelOutlineFeature>();
        feature.name = "Sobel Outline";
        feature.AssignDefaultShaders();
        AssetDatabase.AddObjectToAsset(feature, renderer);

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
        SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        if (featureMap != null)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
        }

        serializedRenderer.ApplyModifiedProperties();
        EditorUtility.SetDirty(renderer);
        return true;
    }
}
