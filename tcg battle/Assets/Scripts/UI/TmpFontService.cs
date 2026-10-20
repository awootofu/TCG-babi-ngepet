using System;
using UnityEngine;
using TMPro;

namespace TcgBattle.UI
{
    /// <summary>
    /// Resolves a TMP font asset at runtime. Prefers the project's TMP essentials
    /// default font; if essentials were never imported, generates a dynamic font
    /// asset from Unity's built-in LegacyRuntime.ttf so text always renders.
    /// </summary>
    public static class TmpFontService
    {
        private static TMP_FontAsset _font;
        private static bool _resolved;

        public static TMP_FontAsset Get()
        {
            if (_resolved)
                return _font;
            _resolved = true;

            // 1) Prefer the project's TMP essentials default font (LiberationSans SDF,
            //    imported via Window > TextMeshPro > Import TMP Essential Resources or
            //    automatically by BattleSceneBuilder). TMP_Settings access can throw NREs
            //    when essentials are missing, so everything is guarded.
            try
            {
                if (TMP_Settings.instance != null)
                {
                    var settingsAsset = TMP_Settings.defaultFontAsset;
                    if (settingsAsset != null)
                    {
                        _font = settingsAsset;
                        return _font;
                    }
                }
            }
            catch
            {
                // Essentials missing for now - continue below.
            }

            // 2) Last-resort: generate a dynamic font asset from Unity's built-in font.
            //    This only works if the engine can read the font face; otherwise leave
            //    the font unset so TMP logs a warning instead of crashing.
            try
            {
                var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null)
                {
                    var generated = TMP_FontAsset.CreateFontAsset(legacy);
                    if (generated != null)
                    {
                        _font = generated;
                        _font.name = "RuntimeFallback";
                        Debug.LogWarning("[TmpFontService] Using runtime fallback font. " +
                                         "Tip: Window > TextMeshPro > Import TMP Essential Resources.");
                        return _font;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TmpFontService] Runtime fallback font failed: " + e.Message);
            }

            Debug.LogError("[TmpFontService] No usable TMP font found. " +
                           "Run Window > TextMeshPro > Import TMP Essential Resources.");
            return null;
        }
    }
}
