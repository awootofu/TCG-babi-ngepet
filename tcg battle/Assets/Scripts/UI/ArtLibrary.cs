using System.Collections.Generic;
using UnityEngine;

namespace TcgBattle.UI
{
    /// <summary>
    /// Loads the converted card/element PNGs from Resources (Texture2D) and wraps them
    /// into sprites with center pivots. The original artist SVGs stay untouched.
    /// </summary>
    public static class ArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite GetCardArt(string resourcePath)
        {
            return Load(resourcePath);
        }

        public static Sprite GetElement(Data.Element element)
        {
            return Load(element == Data.Element.Dark ? "Elements/Dark Element" : "Elements/Fire Element");
        }

        public static Sprite GetEnergyOrb()
        {
            return UiSpriteFactory.Circle;
        }

        public static Sprite GetBattleBackground()
        {
            return Load("Arts/Testos");
        }

        private static Sprite Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;
            if (Cache.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                Debug.LogWarning($"[ArtLibrary] Missing texture at Resources/{resourcePath}");
                return null;
            }
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath;
            Cache[resourcePath] = sprite;
            return sprite;
        }
    }
}
