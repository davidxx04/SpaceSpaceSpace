#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Pipeline de FUENTE del proyecto (editor). Una sola fuente de verdad que usan los builders:
//
//   - EnsureFont(): devuelve el TMP_FontAsset de Art/Fonts. Si no existe pero hay un .ttf/.otf,
//     lo CREA por código (SDF, atlas dinámico: los glifos se hornean bajo demanda, no hace falta
//     pasar por el Font Asset Creator). Para CAMBIAR de fuente: deja UN solo fichero fuente en
//     Art/Fonts, borra el ".asset" viejo y re-ejecuta el builder que toque.
//
//   - EnsureGlowPreset(): preset de material TMP con halación MUY ligera (efecto "pantalla vieja"
//     de las letras blancas de botones/título), compartiendo el atlas del font asset.
//
// Nota de licencia: la fuente incluida por defecto es Archivo Black (OFL, libre — el clon de
// Helvetica Black). Si se quiere Helvetica Neue real, hay que poseer el .otf y su licencia.
public static class FontLibrary
{
    public const string FontDir = "Assets/_Project/Art/Fonts";
    private const string GlowPresetPath = FontDir + "/MenuFontGlow.mat";

    // TMP_FontAsset de Art/Fonts; se crea desde el primer .ttf/.otf si aún no existe. Null si la
    // carpeta está vacía (los builders caen a la fuente por defecto de TMP con un warning).
    public static TMP_FontAsset EnsureFont()
    {
        if (!AssetDatabase.IsValidFolder(FontDir))
        {
            Debug.LogWarning($"[Fonts] No existe {FontDir}. Suelta ahí un .ttf/.otf y re-ejecuta.");
            return null;
        }

        // 1) ¿Ya hay un TMP_FontAsset? (el mismo que recogen los FindPixelFont de los builders)
        var assetGuids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir });
        if (assetGuids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(assetGuids[0]));

        // 2) ¿Hay un fichero fuente del que crearlo?
        var fontGuids = AssetDatabase.FindAssets("t:Font", new[] { FontDir });
        if (fontGuids.Length == 0)
        {
            Debug.LogWarning($"[Fonts] No hay ni TMP_FontAsset ni .ttf/.otf en {FontDir}. " +
                "Suelta una fuente y re-ejecuta el builder.");
            return null;
        }

        string srcPath = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
        var srcFont = AssetDatabase.LoadAssetAtPath<Font>(srcPath);

        // SDF con atlas DINÁMICO: no hay que pre-hornear glifos; TMP añade los que se usen. El
        // .ttf origen queda referenciado, así que debe permanecer en el proyecto (entra en build).
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            srcFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        if (fontAsset == null)
        {
            Debug.LogError($"[Fonts] TMP no pudo crear el font asset desde '{srcPath}'.");
            return null;
        }

        string assetPath = FontDir + "/" + srcFont.name + " SDF.asset";
        fontAsset.name = srcFont.name + " SDF";
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        // Material y atlas viven como SUB-ASSETS del font asset (igual que los crea el Creator).
        fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
        fontAsset.material.name = fontAsset.name + " Material";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Fonts] Creado {assetPath} desde {srcPath} (SDF dinámico). Para cambiar de " +
            "fuente: deja un único .ttf/.otf en Art/Fonts, borra este .asset y re-ejecuta.");
        return fontAsset;
    }

    // Preset con GLOW blanco muy sutil (halación CRT en las letras). Comparte el atlas del font
    // asset; si la fuente cambió, se re-enlaza el atlas al re-ejecutar el builder.
    public static Material EnsureGlowPreset(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return null;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(GlowPresetPath);
        if (mat == null)
        {
            mat = new Material(fontAsset.material) { name = "MenuFontGlow" };
            AssetDatabase.CreateAsset(mat, GlowPresetPath);
        }

        // Re-enlaza atlas/shader por si la fuente es otra desde la última vez.
        mat.shader = fontAsset.material.shader;
        mat.SetTexture("_MainTex", fontAsset.atlasTexture);

        mat.EnableKeyword("GLOW_ON");
        mat.SetColor("_GlowColor", new Color(1f, 1f, 1f, 0.35f));
        mat.SetFloat("_GlowOffset", 0f);
        mat.SetFloat("_GlowInner", 0.05f);
        mat.SetFloat("_GlowOuter", 0.18f);   // halo corto: "muy muy ligerito"
        mat.SetFloat("_GlowPower", 0.75f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif
