#if UNITY_EDITOR
using UnityEditor;

// Set these PNGs to single 2D sprites during first import. No slicing needed.
public sealed class GWMinionSpriteImporter5B3 : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/GraveWardenMinions5B3/Frames/")) return;
        TextureImporter ti = assetImporter as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 256f;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = UnityEngine.FilterMode.Bilinear;
    }
}
#endif
