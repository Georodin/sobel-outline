using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Sobel Outline Include")]
public sealed class SobelOutlineInclude : MonoBehaviour
{
    [Tooltip("When enabled, this object is included in Sobel outlines.")]
    public bool include = true;

    [Tooltip("Also include renderers on child objects.")]
    public bool includeChildren = true;

    public static void CopyActiveRenderers(List<Renderer> destination)
    {
        destination.Clear();
        SobelOutlineInclude[] markers = FindObjectsByType<SobelOutlineInclude>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            SobelOutlineInclude marker = markers[i];
            if (marker == null || !marker.isActiveAndEnabled || !marker.include)
            {
                continue;
            }

            if (marker.includeChildren)
            {
                Renderer[] childRenderers = marker.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < childRenderers.Length; r++)
                {
                    AddIfIncluded(destination, childRenderers[r]);
                }
            }
            else
            {
                AddIfIncluded(destination, marker.GetComponent<Renderer>());
            }
        }
    }

    private static void AddIfIncluded(List<Renderer> destination, Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            return;
        }

        SobelOutlineInclude marker = renderer.GetComponent<SobelOutlineInclude>();
        if (marker != null && !marker.include)
        {
            return;
        }

        if (!destination.Contains(renderer))
        {
            destination.Add(renderer);
        }
    }
}
