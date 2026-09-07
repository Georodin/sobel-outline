# Sobel Outline

URP fullscreen Sobel outline with a per-object **include** flag. Only objects you opt in receive outlines.

Requires **Unity 6** / **URP 17**.

Package id: `com.georodin.sobel-outline`

## Install from Git

In the other Unity project: **Window → Package Manager → + → Add package from git URL…**

```text
https://github.com/Georodin/sobel-outline.git
```

You can also copy this folder into another project's `Packages/` directory.

## Setup in a project

1. **Tools → Sobel Outline → Add Feature To All URP Renderers**
2. Open your URP Renderer asset and confirm **Sobel Outline** is listed
3. Camera: enable **Post Processing**
4. Add **Rendering / Sobel Outline Include** to objects that should get outlines
5. Optional: assign `Mat_SobelTransparent` for a fully invisible mesh that still contributes a silhouette

### Include component

| Field | Meaning |
| --- | --- |
| **Include** | This object is outlined |
| **Include Children** | Child renderers are outlined too |

Uncheck **Include** to skip an object without removing the component.

## Package contents

- `SobelOutlineFeature` — URP renderer feature
- `SobelOutlineInclude` — opt-in component
- `Mat_SobelTransparent` — invisible material that still draws a Sobel silhouette
