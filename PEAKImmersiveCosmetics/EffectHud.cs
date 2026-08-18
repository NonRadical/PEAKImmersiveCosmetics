//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zorro.Core;

namespace PEAKImmersiveCosmetics
{
    /**
     * Replicates PEAK's hand-drawn boil on a UI icon. On a stepped clock, applies a small
     * pseudo-random transform nudge and swaps between frayed sprite variants.
     */
    internal class UIBoil : MonoBehaviour
    {
        private const float StepsPerSecond = 3f;
        private const float PositionAmplitude = 0.15f;
        private const float RotationAmplitude = 0.4f;
        private const float ScaleAmplitude = 0.005f;

        internal Sprite[] Variants;

        private RectTransform _rt;
        private Image _image;
        private Vector2 _basePosition;
        private int _lastStep = int.MinValue;
        private int _seed;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _image = GetComponent<Image>();
            _basePosition = _rt.anchoredPosition;
            _seed = GetInstanceID();
        }

        private void Update()
        {
            int step = (int)(Time.unscaledTime * StepsPerSecond);
            if (step == _lastStep)
            {
                return;
            }
            _lastStep = step;
            _rt.anchoredPosition = _basePosition + new Vector2(Hash(step, 1), Hash(step, 2)) * PositionAmplitude;
            _rt.localRotation = Quaternion.Euler(0f, 0f, Hash(step, 3) * RotationAmplitude);
            _rt.localScale = Vector3.one * (1f + Hash(step, 4) * ScaleAmplitude);
            if (_image != null && Variants != null && Variants.Length > 1)
            {
                int index = (int)(HashU(step, 5) % (uint)Variants.Length);
                _image.sprite = Variants[index];
            }
        }

        private uint HashU(int step, int channel)
        {
            uint h = (uint)_seed * 31u + (uint)step * 0x9E3779B9u + (uint)channel * 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        /** Deterministic -1..1 noise per step and channel, unique per instance. */
        private float Hash(int step, int channel)
        {
            return HashU(step, channel) % 2000u / 1000f - 1f;
        }
    }

    /**
     * Renders the local player's active costume effects as icons above the stamina bar,
     * one icon per effect, tier summed across outfit and hat (clamped to 4). A net tier
     * of 0 shows nothing. Icons are PNGs embedded in the plugin DLL, named
     * {EffectKey}.png, with optional {EffectKey}_down.png debuff variants (otherwise
     * the positive icon is tinted red). PNGs of the same names dropped into
     * BepInEx\config\PEAKImmersiveCosmetics.icons\ override the embedded ones. Missing
     * icons fall back to generated placeholder tiles. Edges get procedurally frayed
     * boil variants.
     */
    internal class EffectHud : MonoBehaviour
    {
        private const float IconSize = 72f;
        private const float PreviewIconSize = 46f;
        private const float IconPad = 6f;
        private const int MaxTier = 4;
        private const int BoilVariantCount = 3;

        private static readonly Color DebuffTint = new Color(1f, 0.45f, 0.45f, 1f);

        private RectTransform _container;
        private int _shownFit = int.MinValue;
        private int _shownHat = int.MinValue;
        private bool _shownAirport;

        // The game's own UI material, if its sketchy style is shader-driven. Resolved once per GUI.
        private Material _gameUiMaterial;
        private bool _searchedGameUiMaterial;

        private static readonly Dictionary<string, (Sprite[] Variants, bool TintAsDebuff)> SpriteCache
            = new Dictionary<string, (Sprite[], bool)>();
        private static readonly Dictionary<string, Sprite> CosmeticSpriteCache = new Dictionary<string, Sprite>();

        internal static string IconDir => Path.Combine(Paths.ConfigPath, "PEAKImmersiveCosmetics.icons");

        /** Creates the override icon folder and a README listing every expected filename. */
        internal static void EnsureIconFolder()
        {
            try
            {
                Directory.CreateDirectory(IconDir);
                var keys = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, ItemEffects> kvp in EffectRegistry.Items)
                {
                    foreach (string key in kvp.Value.Tiers.Keys)
                    {
                        keys.Add(key);
                    }
                    foreach (string key in kvp.Value.Markers)
                    {
                        keys.Add(key);
                    }
                }
                var sb = new StringBuilder();
                sb.AppendLine("Icons shown above the stamina bar for your active costume effects.");
                sb.AppendLine("One icon per effect; the tier is the outfit tier + hat tier (max 4).");
                sb.AppendLine();
                sb.AppendLine("The standard icons are built into the plugin DLL. PNG files placed in");
                sb.AppendLine("this folder override them, so the folder can stay empty.");
                sb.AppendLine();
                sb.AppendLine("Name PNG files {EffectKey}.png, e.g. StaminaRegen.png. The tier is shown as");
                sb.AppendLine("pips drawn on top of the icon at runtime, so do not bake pips into the art.");
                sb.AppendLine("Optionally add {EffectKey}_down.png for net-negative (debuff) tiers;");
                sb.AppendLine("without one, the positive icon is shown tinted red.");
                sb.AppendLine("Square images with transparent backgrounds recommended (e.g. 64x64).");
                sb.AppendLine("Edges are procedurally frayed/boiled in-game to match the UI style.");
                sb.AppendLine();
                sb.AppendLine("Effect keys currently used by the registry:");
                foreach (string key in keys)
                {
                    sb.AppendLine($"  {key}");
                }
                File.WriteAllText(Path.Combine(IconDir, "README.txt"), sb.ToString());
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Failed to prepare icon folder: {e}");
            }
        }

        private void Update()
        {
            GUIManager gui = GUIManager.instance;
            Character local = Character.localCharacter;
            if (!Plugin.ShowEffectIcons.Value || gui == null || gui.bar == null || gui.bar.fullBar == null || local == null)
            {
                // The container is destroyed with the game's GUI on scene changes.
                // Forget it and rebuild when the GUI is back.
                _container = null;
                _shownFit = int.MinValue;
                _searchedGameUiMaterial = false;
                return;
            }
            if (!EffectResolver.TryGetActiveEffects(local, out int fit, out int hat, out ItemEffects outfitEffects, out ItemEffects hatEffects, out ItemEffects setBonusEffects))
            {
                return;
            }
            if (_container == null)
            {
                BuildContainer(gui.bar.fullBar);
                FindGameUiMaterial(gui.bar);
                _shownFit = int.MinValue;
            }
            // In the airport lobby, show per-item breakdown rows so cosmetic browsing
            // gives live feedback.
            bool inAirport = SceneManager.GetActiveScene().name == "Airport";
            if (fit != _shownFit || hat != _shownHat || inAirport != _shownAirport)
            {
                _shownFit = fit;
                _shownHat = hat;
                _shownAirport = inAirport;
                RebuildIcons(fit, hat, outfitEffects, hatEffects, setBonusEffects, inAirport);
            }
        }

        private void BuildContainer(RectTransform fullBar)
        {
            var go = new GameObject("CostumeEffectsHud", typeof(RectTransform));
            _container = (RectTransform)go.transform;
            _container.SetParent(fullBar, worldPositionStays: false);
            _container.anchorMin = new Vector2(0f, 1f);
            _container.anchorMax = new Vector2(0f, 1f);
            _container.pivot = new Vector2(0f, 0f);
            _container.anchoredPosition = new Vector2(0f, 8f);
            _container.sizeDelta = new Vector2(0f, IconSize);
        }

        private void RebuildIcons(int fit, int hat, ItemEffects outfitEffects, ItemEffects hatEffects, ItemEffects setBonusEffects, bool inAirport)
        {
            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                Destroy(_container.GetChild(i).gameObject);
            }
            var merged = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            void Merge(ItemEffects effects)
            {
                if (effects == null)
                {
                    return;
                }
                foreach (KeyValuePair<string, int> kvp in effects.Tiers)
                {
                    merged.TryGetValue(kvp.Key, out int existing);
                    merged[kvp.Key] = existing + kvp.Value;
                }
            }
            Merge(outfitEffects);
            Merge(hatEffects);
            Merge(setBonusEffects);
            var markers = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (outfitEffects != null)
            {
                markers.UnionWith(outfitEffects.Markers);
            }
            if (hatEffects != null)
            {
                markers.UnionWith(hatEffects.Markers);
            }
            if (setBonusEffects != null)
            {
                markers.UnionWith(setBonusEffects.Markers);
            }

            float x = 0f;
            foreach (KeyValuePair<string, int> kvp in merged.OrderBy(k => k.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                int tier = Mathf.Clamp(kvp.Value, -MaxTier, MaxTier);
                if (tier == 0)
                {
                    continue;
                }
                AddIcon(kvp.Key, tier, 0f, ref x, IconSize);
            }
            foreach (string marker in markers)
            {
                AddIcon(marker, 0, 0f, ref x, IconSize);
            }

            if (inAirport)
            {
                Customization customization = Singleton<Customization>.Instance;
                if (customization != null)
                {
                    float small = PreviewIconSize;
                    float y1 = IconSize + IconPad;
                    float y2 = y1 + small + IconPad;
                    if (fit >= 0 && fit < customization.fits.Length)
                    {
                        BuildItemRow(customization.fits[fit], outfitEffects, isHat: false, y1, small);
                    }
                    if (hat >= 0 && hat < customization.hats.Length)
                    {
                        BuildItemRow(customization.hats[hat], hatEffects, isHat: true, y2, small);
                    }
                }
            }
        }

        /** One airport-lobby row: the cosmetic's icon followed by the effects it contributes. */
        private void BuildItemRow(CustomizationOption option, ItemEffects effects, bool isHat, float y, float size)
        {
            if (option == null || effects == null || (effects.Tiers.Count == 0 && effects.Markers.Count == 0))
            {
                return;
            }
            float x = 0f;
            AddCosmeticIcon(option, isHat, y, ref x, size);
            foreach (KeyValuePair<string, int> kvp in effects.Tiers.OrderBy(k => k.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                int tier = Mathf.Clamp(kvp.Value, -MaxTier, MaxTier);
                if (tier == 0)
                {
                    continue;
                }
                AddIcon(kvp.Key, tier, y, ref x, size);
            }
            foreach (string marker in effects.Markers.OrderBy(k => k, System.StringComparer.OrdinalIgnoreCase))
            {
                AddIcon(marker, 0, y, ref x, size);
            }
        }

        private void AddIcon(string effectKey, int tier, float y, ref float x, float size)
        {
            Sprite[] variants = GetSpriteVariants(effectKey, tier < 0, out bool tintAsDebuff);
            var go = new GameObject($"{effectKey}_{tier}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_container, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            Image image = go.GetComponent<Image>();
            image.sprite = variants[0];
            image.color = tintAsDebuff ? DebuffTint : Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (_gameUiMaterial != null)
            {
                image.material = _gameUiMaterial;
            }
            go.AddComponent<UIBoil>().Variants = variants;
            // Pips are siblings created after the icon so they draw on top and do not boil.
            AddPips(Mathf.Abs(tier), x, y, size);
            x += size + IconPad;
        }

        /** Static tier pips overlaid along the bottom of an icon. */
        private void AddPips(int count, float iconX, float iconY, float size)
        {
            if (count <= 0)
            {
                return;
            }
            float d = size * 0.14f;
            float gap = d * 0.3f;
            float total = count * d + (count - 1) * gap;
            float px = iconX + (size - total) / 2f;
            float py = iconY + size * 0.10f;
            Sprite pip = GetPipSprite();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_container, worldPositionStays: false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(px, py);
                rt.sizeDelta = new Vector2(d, d);
                Image image = go.GetComponent<Image>();
                image.sprite = pip;
                image.raycastTarget = false;
                px += d + gap;
            }
        }

        private static Sprite _pipSprite;

        /** A small embroidered-style dot: dark outline, cream fill, tiny highlight. */
        private static Sprite GetPipSprite()
        {
            if (_pipSprite != null)
            {
                return _pipSprite;
            }
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.ARGB32, mipChain: false);
            var outline = new Color32(70, 52, 36, 255);
            var fill = new Color32(245, 233, 200, 255);
            var clear = new Color32(0, 0, 0, 0);
            float c = (s - 1) / 2f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color32 col = clear;
                    if (dist <= s * 0.34f)
                    {
                        col = fill;
                    }
                    else if (dist <= s * 0.46f)
                    {
                        col = outline;
                    }
                    tex.SetPixel(x, y, col);
                }
            }
            for (int y = 19; y <= 22; y++)
            {
                for (int x = 10; x <= 14; x++)
                {
                    tex.SetPixel(x, y, new Color32(255, 255, 255, 160));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _pipSprite = Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _pipSprite;
        }

        private void AddCosmeticIcon(CustomizationOption option, bool isHat, float y, ref float x, float size)
        {
            Sprite sprite = GetCosmeticSprite(option, isHat);
            var go = new GameObject($"Cosmetic_{option.name}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_container, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.AddComponent<UIBoil>();
            // Extra gap between the item icon and its effect list.
            x += size + 2f * IconPad;
        }

        /**
         * Icon for a cosmetic item. Priority: {AssetName}.png in the icon folder or
         * embedded in the DLL, the option's own texture, the fit material's texture,
         * then a placeholder tile.
         */
        private static Sprite GetCosmeticSprite(CustomizationOption option, bool isHat)
        {
            string cacheKey = $"cosmetic:{option.name}";
            if (CosmeticSpriteCache.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }
            Sprite sprite = null;
            Texture2D fileTex = TryLoad(Path.Combine(IconDir, option.name + ".png")) ?? TryLoadEmbedded(option.name + ".png");
            if (fileTex != null)
            {
                sprite = Sprite.Create(fileTex, new Rect(0f, 0f, fileTex.width, fileTex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            else if (option.texture is Texture2D optionTex)
            {
                sprite = Sprite.Create(optionTex, new Rect(0f, 0f, optionTex.width, optionTex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            else if (!isHat && option.fitMaterial != null && option.fitMaterial.mainTexture is Texture2D fitTex)
            {
                sprite = Sprite.Create(fitTex, new Rect(0f, 0f, fitTex.width, fitTex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (sprite == null)
            {
                Texture2D tex = MakePlaceholder(option.name, negative: false);
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            CosmeticSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        /** Borrows the stamina bar's material so icons pick up any shader-driven styling. */
        private void FindGameUiMaterial(StaminaBar bar)
        {
            if (_searchedGameUiMaterial)
            {
                return;
            }
            _searchedGameUiMaterial = true;
            _gameUiMaterial = null;
            foreach (Image image in bar.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (image.material != null && image.material != image.defaultMaterial
                    && image.material.shader != null && image.material.shader.name != "UI/Default")
                {
                    _gameUiMaterial = image.material;
                    Plugin.Log.LogInfo($"Effect icons adopting game UI material '{image.material.name}' (shader '{image.material.shader.name}') from '{image.name}'.");
                    return;
                }
            }
            Plugin.Log.LogInfo("Stamina bar images use the default UI material, so effect icons rely on the procedural fray only.");
        }

        private static Sprite[] GetSpriteVariants(string effectKey, bool negative, out bool tintAsDebuff)
        {
            string cacheKey = negative ? $"{effectKey}:down" : effectKey;
            if (SpriteCache.TryGetValue(cacheKey, out (Sprite[] Variants, bool TintAsDebuff) cached))
            {
                tintAsDebuff = cached.TintAsDebuff;
                return cached.Variants;
            }
            Texture2D baseTex = null;
            if (negative)
            {
                baseTex = TryLoad(DownPath(effectKey)) ?? TryLoadEmbedded($"{effectKey}_down.png");
            }
            // No dedicated debuff art anywhere means the positive icon gets tinted red.
            tintAsDebuff = negative && baseTex == null;
            if (baseTex == null)
            {
                baseTex = TryLoad(UpPath(effectKey)) ?? TryLoadEmbedded($"{effectKey}.png");
            }
            if (baseTex == null)
            {
                Plugin.Log.LogInfo($"No icon for {effectKey} (embedded or {UpPath(effectKey)}); using placeholder.");
                baseTex = MakePlaceholder(effectKey, negative);
            }
            var variants = new Sprite[BoilVariantCount];
            int seedBase = cacheKey.GetHashCode();
            for (int v = 0; v < BoilVariantCount; v++)
            {
                Texture2D frayed = FrayEdges(baseTex, seedBase * 31 + v);
                variants[v] = Sprite.Create(frayed, new Rect(0f, 0f, frayed.width, frayed.height), new Vector2(0.5f, 0.5f), 100f);
            }
            Destroy(baseTex);
            SpriteCache[cacheKey] = (variants, tintAsDebuff);
            return variants;
        }

        private static string UpPath(string effectKey) => Path.Combine(IconDir, $"{effectKey}.png");
        private static string DownPath(string effectKey) => Path.Combine(IconDir, $"{effectKey}_down.png");

        private static Texture2D TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false);
                if (ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
                {
                    return tex;
                }
                Destroy(tex);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Failed to load icon {path}: {e}");
            }
            return null;
        }

        /** Loads an icon PNG embedded in the plugin DLL, or null if none is embedded. */
        private static Texture2D TryLoadEmbedded(string fileName)
        {
            try
            {
                using Stream stream = typeof(EffectHud).Assembly
                    .GetManifestResourceStream("PEAKImmersiveCosmetics.icons." + fileName);
                if (stream == null)
                {
                    return null;
                }
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false);
                if (ImageConversion.LoadImage(tex, buffer.ToArray()))
                {
                    return tex;
                }
                Destroy(tex);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Failed to load embedded icon {fileName}: {e}");
            }
            return null;
        }

        /**
         * Produces a copy of the texture with sparse needle-thin teeth bitten out of the
         * top and bottom edges only, like the game's UI fray. Tear depth per column is a
         * faint ragged base plus hash-placed triangular teeth (see SpikeDepth), eroded
         * along the column with a tight alpha feather so the teeth stay crisp; between
         * teeth the edge keeps its normal shape. A final connectivity pass deletes any
         * piece the fray split off the region it belonged to, while regions that were
         * already separate in the source art are left alone.
         */
        private static Texture2D FrayEdges(Texture2D source, int seed)
        {
            int w = source.width;
            int h = source.height;
            Color32[] src = source.GetPixels32();
            var dst = (Color32[])src.Clone();

            float toothSpacing = Mathf.Max(5f, w / 12f);
            float maxDepth = Mathf.Max(1.5f, h * 0.03f);
            const float feather = 0.75f;

            for (int x = 0; x < w; x++)
            {
                float topDepth = SpikeDepth(x, toothSpacing, seed) * maxDepth;
                float bottomDepth = SpikeDepth(x, toothSpacing, seed + 53) * maxDepth;

                // Distance runs along the column from each edge (or from transparency),
                // resetting at gaps so every opaque run frays at both of its ends.
                float dist = 0f;
                for (int y = h - 1; y >= 0; y--)
                {
                    int i = y * w + x;
                    if (src[i].a < 128)
                    {
                        dist = 0f;
                        continue;
                    }
                    dist += 1f;
                    float keep = Mathf.Clamp01((dist - topDepth + feather) / (2f * feather));
                    if (keep < 1f)
                    {
                        dst[i].a = (byte)(dst[i].a * keep);
                    }
                }
                dist = 0f;
                for (int y = 0; y < h; y++)
                {
                    int i = y * w + x;
                    if (src[i].a < 128)
                    {
                        dist = 0f;
                        continue;
                    }
                    dist += 1f;
                    float keep = Mathf.Clamp01((dist - bottomDepth + feather) / (2f * feather));
                    if (keep < 1f)
                    {
                        dst[i].a = (byte)(dst[i].a * keep);
                    }
                }
            }

            RemoveFrayIslands(src, dst, w, h);

            var tex = new Texture2D(w, h, TextureFormat.ARGB32, mipChain: false);
            tex.SetPixels32(dst);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /**
         * Clears fray-created islands. Each result region is matched to the source
         * region it came from; only the largest surviving piece of each source region
         * is kept, so the fray can never leave disconnected pixels behind. Regions
         * that were separate in the source art map to distinct source regions and
         * therefore all survive.
         */
        private static void RemoveFrayIslands(Color32[] src, Color32[] dst, int w, int h)
        {
            int[] srcLabels = LabelComponents(src, w, h, out _);
            int[] dstLabels = LabelComponents(dst, w, h, out List<int> dstSizes);
            var dstToSrc = new int[dstSizes.Count];
            for (int i = 0; i < dst.Length; i++)
            {
                if (dstLabels[i] != 0)
                {
                    dstToSrc[dstLabels[i]] = srcLabels[i];
                }
            }
            var best = new Dictionary<int, int>();
            for (int label = 1; label < dstSizes.Count; label++)
            {
                if (!best.TryGetValue(dstToSrc[label], out int current) || dstSizes[label] > dstSizes[current])
                {
                    best[dstToSrc[label]] = label;
                }
            }
            for (int i = 0; i < dst.Length; i++)
            {
                if (dstLabels[i] != 0 && best[dstToSrc[dstLabels[i]]] != dstLabels[i])
                {
                    dst[i].a = 0;
                }
            }
        }

        /** 4-connected component labels over pixels with any alpha; sizes[label] = pixel count. */
        private static int[] LabelComponents(Color32[] pixels, int w, int h, out List<int> sizes)
        {
            var labels = new int[pixels.Length];
            sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            int next = 1;
            for (int start = 0; start < pixels.Length; start++)
            {
                if (labels[start] != 0 || pixels[start].a == 0)
                {
                    continue;
                }
                labels[start] = next;
                stack.Push(start);
                int size = 0;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % w;
                    int y = i / w;
                    Visit(i - 1, x > 0);
                    Visit(i + 1, x < w - 1);
                    Visit(i - w, y > 0);
                    Visit(i + w, y < h - 1);
                    void Visit(int j, bool inBounds)
                    {
                        if (inBounds && labels[j] == 0 && pixels[j].a != 0)
                        {
                            labels[j] = next;
                            stack.Push(j);
                        }
                    }
                }
                sizes.Add(size);
                next++;
            }
            return labels;
        }

        private static float Lattice(int ix, int iy, int seed)
        {
            uint hash = (uint)seed * 0x9E3779B9u + (uint)ix * 0x85EBCA6Bu + (uint)iy * 0xC2B2AE35u;
            hash ^= hash >> 13;
            hash *= 0x27D4EB2Fu;
            hash ^= hash >> 16;
            return hash % 1000u / 1000f;
        }

        /** Smoothly interpolated lattice noise, 0..1. */
        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float effects = x - ix;
            float fy = y - iy;
            effects = effects * effects * (3f - 2f * effects);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(ix, iy, seed);
            float b = Lattice(ix + 1, iy, seed);
            float c = Lattice(ix, iy + 1, seed);
            float d = Lattice(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, effects), Mathf.Lerp(c, d, effects), fy);
        }

        /**
         * 0..1 tear-depth profile for one edge at column x: a faint ragged base, plus
         * sparse needle-thin teeth. Each spacing-sized cell hash-decides whether it
         * holds one tooth; a tooth is a triangle only a couple of pixels wide at the
         * base, so the edge keeps the icon's normal shape between teeth.
         */
        private static float SpikeDepth(int x, float spacing, int seed)
        {
            const float toothChance = 0.65f;
            const float halfWidth = 1.6f;
            float depth = 0.12f * ValueNoise(x / spacing, 0.37f, seed);
            int cell = Mathf.FloorToInt(x / spacing);
            // A tooth near a cell border can reach into this column from a neighbor cell.
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (Lattice(c, 1, seed) >= toothChance)
                {
                    continue;
                }
                float toothX = (c + 0.15f + 0.7f * Lattice(c, 2, seed)) * spacing;
                float toothDepth = 0.55f + 0.45f * Lattice(c, 3, seed);
                float t = 1f - Mathf.Abs(x - toothX) / halfWidth;
                if (t > 0f)
                {
                    depth = Mathf.Max(depth, toothDepth * t);
                }
            }
            return depth;
        }

        /** A flat colored tile with 1-4 tier pips. Color derives from the effect key, red for debuffs. */
        private static Texture2D MakePlaceholder(string effectKey, bool negative)
        {
            const int size = 96;
            const int borderThickness = 5;
            uint hash = 2166136261u;
            foreach (char c in effectKey)
            {
                hash = (hash ^ c) * 16777619u;
            }
            Color fill = negative
                ? Color.HSVToRGB(0.0f, 0.6f, 0.55f)
                : Color.HSVToRGB(hash % 360u / 360f, 0.55f, 0.85f);
            Color border = fill * 0.5f;
            border.a = 1f;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, mipChain: false);
            for (int y = 0; y < size; y++)
            {
                for (int px = 0; px < size; px++)
                {
                    bool isBorder = px < borderThickness || y < borderThickness
                        || px >= size - borderThickness || y >= size - borderThickness;
                    tex.SetPixel(px, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
