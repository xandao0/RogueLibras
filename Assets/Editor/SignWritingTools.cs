using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.VectorGraphics.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// The Unity half of the SignWriting pipeline. The Node script
// (RogueLibras_Resources/cards/generate_signwriting/svg.js) looks the game's
// words up on SignPuddle and writes their SVGs into the sign language's locale
// folder (Assets/Locales/libras/signs); this takes it from there:
//
//   RogueLibras > SignWriting > Buscar sinais novos (SignPuddle)
//     runs svg.js, then everything below.
//
//   RogueLibras > SignWriting > Atualizar sinais
//     1. imports the new SVGs with the same settings as the existing ones,
//     2. rebuilds the SignWritingLibrary (sign rows on cards, tooltips, Ajuda),
//     3. rebuilds the TMP sprite atlas the descriptions draw inline signs from,
//     4. checks what is still missing - see LocalizationTools.
public static class SignWritingTools
{
    private const string SpriteAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/SignWriting.asset";
    private const string AtlasPath = "Assets/TextMesh Pro/Resources/Sprite Assets/SignWritingAtlas_Padded.png";

    // Layout of the hand-built atlas this replaces: 128px glyphs on a 154px
    // grid with a 13px margin, metrics at twice the pixel size.
    private const int GlyphMax = 128;
    private const int Padding = 13;
    private const int Cell = GlyphMax + 2 * Padding;
    private const float MetricScale = 2f;
    private const float Ascent = 0.85f;
    private const float Advance = 1.05f;

    [MenuItem("RogueLibras/SignWriting/Atualizar sinais")]
    public static void UpdateAll()
    {
        string summary;
        List<string> missing = Rebuild(out summary);
        if (missing == null) EditorUtility.DisplayDialog("SignWriting", summary, "OK");
        else Report(summary, missing);
    }

    /// Steps 1-4 without any dialog. Returns the words still missing a sign,
    /// or null when there is no library to rebuild.
    public static List<string> Rebuild(out string summary)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        FixSignImportSettings();

        SignWritingLibrary library = FindLibrary();
        if (library == null)
        {
            summary = "Nenhum SignWritingLibrary encontrado (veja Assets/Locales/Resources/LocaleSettings).";
            return null;
        }

        int words = library.RebuildFromFolder();
        int glyphs = RebuildInlineAtlas(library);
        AssetDatabase.SaveAssets();
        Loc.Reload();

        summary = words + " palavras na biblioteca, " + glyphs + " sinais no atlas.";
        return LocalizationTools.MissingSigns(library);
    }

    // ---- running svg.js from here ------------------------------------------------

    private const string ScriptPathPref = "RogueLibras.SignWriting.SvgJsPath";
    private static System.Diagnostics.Process _node;
    private static StringBuilder _nodeOutput;

    /// Runs the Node script (SignPuddle lookup + SVGs) and then everything
    /// Atualizar sinais does, so new words go from the cards to the game in
    /// one click.
    [MenuItem("RogueLibras/SignWriting/Buscar sinais novos (SignPuddle)")]
    public static void FetchNewSigns()
    {
        if (_node != null) { Debug.Log("[SignWriting] svg.js is already running."); return; }

        string script = FindScript();
        if (script == null) return;

        string folder = Path.GetDirectoryName(script);
        if (!Directory.Exists(Path.Combine(folder, "node_modules")))
        {
            EditorUtility.DisplayDialog("SignWriting",
                "Falta instalar as dependências do svg.js. No terminal, rode uma vez:\n\ncd \"" + folder + "\"\nnpm install",
                "OK");
            return;
        }

        var info = new System.Diagnostics.ProcessStartInfo("node",
            "\"" + script + "\" --unity \"" + Path.GetFullPath(Application.dataPath) + "\"")
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _nodeOutput = new StringBuilder();
        try
        {
            _node = new System.Diagnostics.Process { StartInfo = info };
            _node.OutputDataReceived += (s, e) => { if (e.Data != null) lock (_nodeOutput) _nodeOutput.AppendLine(e.Data); };
            _node.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (_nodeOutput) _nodeOutput.AppendLine(e.Data); };
            _node.Start();
            _node.BeginOutputReadLine();
            _node.BeginErrorReadLine();
        }
        catch (System.Exception e)
        {
            _node = null;
            EditorUtility.DisplayDialog("SignWriting",
                "Não consegui rodar o Node.js (" + e.Message + ").\nInstale o Node.js 18 ou mais novo e reinicie o Unity.", "OK");
            return;
        }

        Debug.Log("[SignWriting] Running " + script + " ...");
        EditorApplication.update += WatchNode;
    }

    private static void WatchNode()
    {
        if (_node == null) { EditorApplication.update -= WatchNode; return; }

        if (!_node.HasExited)
        {
            if (EditorUtility.DisplayCancelableProgressBar("SignWriting",
                    "Procurando palavras novas no SignPuddle...", 0.5f))
            {
                try { _node.Kill(); } catch { }
            }
            return;
        }

        EditorApplication.update -= WatchNode;
        EditorUtility.ClearProgressBar();
        _node.WaitForExit();   // flushes the last of the output
        int code = _node.ExitCode;
        _node.Dispose();
        _node = null;

        string output;
        lock (_nodeOutput) output = Regex.Replace(_nodeOutput.ToString(), @"\x1b\[[0-9;]*m", "");
        if (code == 0 || code == 1) Debug.Log("[SignWriting] svg.js:\n" + output);   // 1 = some words still missing
        else
        {
            Debug.LogError("[SignWriting] svg.js failed (" + code + "):\n" + output);
            EditorUtility.DisplayDialog("SignWriting", "O svg.js falhou. Veja o Console.", "OK");
            return;
        }

        UpdateAll();
    }

    // Next to the Unity project in the usual checkout; asks once otherwise.
    private static string FindScript()
    {
        string saved = EditorPrefs.GetString(ScriptPathPref, "");
        if (File.Exists(saved)) return saved;

        string guess = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../../RogueLibras_Resources/cards/generate_signwriting/svg.js"));
        if (File.Exists(guess))
        {
            EditorPrefs.SetString(ScriptPathPref, guess);
            return guess;
        }

        string picked = EditorUtility.OpenFilePanel("Onde está o svg.js?", Path.GetDirectoryName(guess), "js");
        if (string.IsNullOrEmpty(picked)) return null;
        EditorPrefs.SetString(ScriptPathPref, picked);
        return picked;
    }

    private static void Report(string summary, List<string> missing)
    {
        var sb = new StringBuilder();
        if (summary != null) sb.AppendLine(summary).AppendLine();

        if (missing.Count == 0)
        {
            sb.Append("Tudo o que o jogo mostra tem sinal.");
            Debug.Log("[SignWriting] " + sb.ToString().Replace("\n", " "));
        }
        else
        {
            sb.AppendLine("Palavras sem sinal (crie no SignMaker ou rode o svg.js):");
            foreach (var w in missing) sb.AppendLine("  • " + w);
            Debug.LogWarning("[SignWriting] Sem sinal: " + string.Join(", ", missing));
        }
        sb.AppendLine().Append("Relatório completo: RogueLibras > Localização > Relatório de tradução.");

        EditorUtility.DisplayDialog("SignWriting", sb.ToString(), "OK");
    }

    /// The default sign language's library, as LocaleSettings names it.
    public static SignWritingLibrary FindLibrary()
    {
        var settings = Resources.Load<LocaleSettings>("LocaleSettings");
        var locale = settings != null ? settings.FindSign(settings.defaultSign) : null;
        if (locale != null && locale.library != null) return locale.library;

        string guid = AssetDatabase.FindAssets("t:SignWritingLibrary").FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<SignWritingLibrary>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // ---- import settings for new SVGs ---------------------------------------

    // A new sign SVG comes in as a vector sprite (the package default). Once
    // the import is over, switch it to the sign settings and import it again -
    // a scripted importer's settings only reach its .meta through a dirty
    // SaveAndReimport, not by being changed mid-import.
    private class SvgImportSettings : AssetPostprocessor
    {
        private static bool _fixing;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_fixing) return;
            foreach (string path in imported)
            {
                if (!IsSignSvg(path)) continue;
                _fixing = true;
                try { FixSignImportSettings(); }
                finally { _fixing = false; }
                return;
            }
        }
    }

    // Any sign language's SVGs: Assets/Locales/<id>/signs/...
    private static bool IsSignSvg(string path)
    {
        return path.StartsWith(LocalesFolder + "/") && path.Contains("/signs/") && path.EndsWith(".svg");
    }

    private const string LocalesFolder = "Assets/Locales";

    private static void ApplySignSettings(SVGImporter importer)
    {
        importer.SvgType = SVGType.TexturedSprite;
        importer.KeepTextureAspectRatio = true;
        importer.TextureSize = 512;
        importer.PredefinedResolutionIndex = 1;
        importer.TargetResolution = 1080;
        importer.SampleCount = 8;
        importer.FilterMode = FilterMode.Bilinear;
        importer.WrapMode = TextureWrapMode.Clamp;
    }

    // A sign still imported as a vector sprite would be missing from the atlas
    // and look different on the cards; put it on the same settings as the rest.
    // Only ever touches ones that are not textured sprites, so hand-tuned
    // resolution or sampling on the others is left alone.
    private static int FixSignImportSettings()
    {
        int fixedCount = 0;
        if (!AssetDatabase.IsValidFolder(LocalesFolder)) return 0;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { LocalesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsSignSvg(path)) continue;
            var importer = AssetImporter.GetAtPath(path) as SVGImporter;
            if (importer == null || importer.SvgType == SVGType.TexturedSprite) continue;

            ApplySignSettings(importer);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            fixedCount++;
        }
        if (fixedCount > 0) Debug.Log("[SignWriting] Fixed the import settings of " + fixedCount + " SVG(s).");
        return fixedCount;
    }

    // ---- inline atlas -------------------------------------------------------

    private struct Glyph
    {
        public string name;
        public Color32[] pixels;
        public int width, height;
    }

    /// Redraws the TMP sprite atlas from the sign language's SVGs and rebuilds
    /// the sprite asset's tables to match. Returns how many signs it holds.
    ///
    /// There is one atlas (TMP's default sprite asset), so it holds the
    /// default sign language; a second sign language would need its own
    /// sprite asset, picked when that language is active.
    public static int RebuildInlineAtlas(SignWritingLibrary library)
    {
        var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
        if (spriteAsset == null)
        {
            Debug.LogError("[SignWriting] No TMP sprite asset at " + SpriteAssetPath);
            return 0;
        }

        var glyphs = new List<Glyph>();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { library.SignFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".svg")) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            Glyph g;
            if (TryRasterize(sprite, Path.GetFileNameWithoutExtension(path), out g)) glyphs.Add(g);
            else Debug.LogWarning("[SignWriting] Could not rasterize " + path);
        }
        glyphs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (glyphs.Count == 0) return 0;

        // 1024 wide (six columns) and as tall as it needs to be; wider only
        // once it would get more than twice as tall as it is wide.
        int width = 1024, cols, height;
        while (true)
        {
            cols = (width - Padding) / Cell;
            int rows = Mathf.CeilToInt(glyphs.Count / (float)cols);
            height = Mathf.NextPowerOfTwo(rows * Cell + Padding);
            if (height <= width * 2) break;
            width *= 2;
        }
        int size = Mathf.Max(width, height);

        var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false);
        atlas.SetPixels32(new Color32[width * height]);   // transparent

        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.spriteCharacterTable.Clear();

        for (int i = 0; i < glyphs.Count; i++)
        {
            Glyph g = glyphs[i];
            int x = Padding + (i % cols) * Cell;
            int y = Padding + (i / cols) * Cell;
            atlas.SetPixels32(x, y, g.width, g.height, g.pixels);

            float w = g.width * MetricScale, h = g.height * MetricScale;
            var spriteGlyph = new TMP_SpriteGlyph((uint)i,
                new GlyphMetrics(w, h, 0f, h * Ascent, w * Advance),
                new GlyphRect(x, y, g.width, g.height), 1f, 0);
            spriteAsset.spriteGlyphTable.Add(spriteGlyph);

            var character = new TMP_SpriteCharacter(0xFFFE, spriteAsset, spriteGlyph) { name = g.name };
            spriteAsset.spriteCharacterTable.Add(character);
        }
        atlas.Apply();

        File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);

        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer != null && importer.maxTextureSize < size)
        {
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
        }

        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        spriteAsset.spriteSheet = sheet;
        if (spriteAsset.material != null) spriteAsset.material.mainTexture = sheet;
        spriteAsset.UpdateLookupTables();
        GiveOwnHashCodes(spriteAsset);
        EditorUtility.SetDirty(spriteAsset);
        if (spriteAsset.material != null) EditorUtility.SetDirty(spriteAsset.material);
        SyncPlainSpriteAsset(spriteAsset, sheet);

        Debug.Log("[SignWriting] Inline atlas rebuilt: " + glyphs.Count + " signs on " + width + "x" + height);
        return glyphs.Count;
    }

    // The same signs without the bold stroke and ring: the outline material
    // keeps small description signs readable on a card, but on the bigger HUD
    // numbers (piles, block, enemy health) it smears them. Same atlas and
    // tables, TMP's plain sprite shader. SignText.UsePlainSigns picks it.
    private const string PlainSpriteAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/" + SignText.PlainSpriteAssetName + ".asset";

    private static void SyncPlainSpriteAsset(TMP_SpriteAsset source, Texture2D sheet)
    {
        var plain = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(PlainSpriteAssetPath);
        if (plain == null)
        {
            AssetDatabase.CopyAsset(SpriteAssetPath, PlainSpriteAssetPath);
            plain = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(PlainSpriteAssetPath);
            if (plain == null) { Debug.LogError("[SignWriting] Could not create " + PlainSpriteAssetPath); return; }
        }

        Shader shader = Shader.Find("TextMeshPro/Sprite");
        if (plain.material != null && shader != null && plain.material.shader != shader)
        {
            plain.material.shader = shader;
            plain.material.name = SignText.PlainSpriteAssetName + " Material";
        }
        if (plain.material != null) plain.material.mainTexture = sheet;
        plain.spriteSheet = sheet;

        plain.spriteGlyphTable.Clear();
        plain.spriteCharacterTable.Clear();
        foreach (var g in source.spriteGlyphTable)
        {
            var glyph = new TMP_SpriteGlyph(g.index, g.metrics, g.glyphRect, g.scale, g.atlasIndex);
            plain.spriteGlyphTable.Add(glyph);
        }
        foreach (var c in source.spriteCharacterTable)
        {
            var glyph = plain.spriteGlyphTable[(int)c.glyphIndex];
            plain.spriteCharacterTable.Add(new TMP_SpriteCharacter(c.unicode, plain, glyph) { name = c.name });
        }
        plain.UpdateLookupTables();
        GiveOwnHashCodes(plain);
        EditorUtility.SetDirty(plain);
        if (plain.material != null) EditorUtility.SetDirty(plain.material);
    }

    // TMP caches sprite assets and their materials by these codes. Left at 0
    // (as the hand-made asset was) two sprite assets collide, and a text can
    // be drawn from the wrong one - or find no sprite and print the tag.
    private static void GiveOwnHashCodes(TMP_SpriteAsset asset)
    {
        asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
        if (asset.material != null)
            asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(asset.material.name);
    }

    // Reads the sign through the GPU (its texture is not readable), trims it
    // to its strokes, fits it in a 128px box and turns it into a white mask,
    // which inline sprites tint to the surrounding text colour.
    private static bool TryRasterize(Sprite sprite, string spriteName, out Glyph glyph)
    {
        glyph = default(Glyph);
        Texture2D tex = sprite.texture;
        if (tex == null) return false;

        Rect r = sprite.textureRect;
        int sw = Mathf.RoundToInt(r.width), sh = Mathf.RoundToInt(r.height);
        if (sw <= 0 || sh <= 0) return false;

        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var src = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
        src.ReadPixels(new Rect(r.x, r.y, sw, sh), 0, 0);
        src.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] px = src.GetPixels32();
        Object.DestroyImmediate(src);

        // Trim to the strokes.
        int minX = sw, minY = sh, maxX = -1, maxY = -1;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (px[y * sw + x].a > 4)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) return false;

        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        float scale = Mathf.Min(1f, GlyphMax / (float)Mathf.Max(cw, ch));
        int dw = Mathf.Max(1, Mathf.RoundToInt(cw * scale));
        int dh = Mathf.Max(1, Mathf.RoundToInt(ch * scale));

        // Box filter: each target pixel averages the source pixels it covers.
        var dst = new Color32[dw * dh];
        float fx = cw / (float)dw, fy = ch / (float)dh;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int x0 = minX + Mathf.FloorToInt(x * fx), x1 = minX + Mathf.Max(Mathf.FloorToInt(x * fx) + 1, Mathf.CeilToInt((x + 1) * fx));
                int y0 = minY + Mathf.FloorToInt(y * fy), y1 = minY + Mathf.Max(Mathf.FloorToInt(y * fy) + 1, Mathf.CeilToInt((y + 1) * fy));
                x1 = Mathf.Min(x1, minX + cw);
                y1 = Mathf.Min(y1, minY + ch);

                int sum = 0, n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++) { sum += px[yy * sw + xx].a; n++; }
                dst[y * dw + x] = new Color32(255, 255, 255, (byte)(n > 0 ? sum / n : 0));
            }

        glyph = new Glyph { name = spriteName, pixels = dst, width = dw, height = dh };
        return true;
    }
}
