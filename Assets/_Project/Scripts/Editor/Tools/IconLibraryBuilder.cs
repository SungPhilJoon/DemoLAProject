using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DemoLA.Data;
using DemoLA.UI;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// IconManifest.csv (Kind,Key,Icon) → IconLibrary.asset.
    ///  Kind = EIconKind 이름, Key = 스킬 · 각인 · 카드 · 아이템 이름 또는 "*"(기본) / "?"(이름 해시 풀),
    ///  Icon = game-icons 경로 "저작자/이름" → Glyphs/저작자/이름.png (Tools/data/fetch_icons.py 가 받는다).
    /// </summary>
    public static class IconLibraryBuilder
    {
        public const string Root = "Assets/_Project/Art/Icons";
        public const string ManifestPath = Root + "/IconManifest.csv";
        public const string GlyphRoot = Root + "/Glyphs";

        [MenuItem("DemoLA/Data/아이콘 라이브러리 갱신 (IconManifest.csv)", priority = 22)]
        public static void BuildMenu()
        {
            var library = Build(true);
            Selection.activeObject = library;
        }

        /// <summary>InRebuild = false 면 에셋이 이미 있을 때 그대로 돌려준다 (셋업용).</summary>
        public static IconLibrary Build(bool InRebuild)
        {
            var library = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconFactory.LibraryPath);
            if (library != null && InRebuild == false)
            {
                return library;
            }

            if (File.Exists(ManifestPath) == false)
            {
                Debug.LogWarning($"[DemoLA] {ManifestPath} 없음 — 절차적 아이콘을 쓴다.");
                return library;
            }

            if (library == null)
            {
                library = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(library, IconFactory.LibraryPath);
            }

            ensureGlyphImportSettings();
            var missing = new List<string>();
            var entries = new List<IconLibrary.Entry>();
            var lines = File.ReadAllLines(ManifestPath, Encoding.UTF8);
            for (int i = 1; i < lines.Length; ++i)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) == true)
                {
                    continue;
                }

                var cells = line.Split(',');
                EIconKind kind;
                if (cells.Length < 3 || Enum.TryParse(cells[0].Trim(), out kind) == false)
                {
                    Debug.LogWarning($"[DemoLA] IconManifest {i + 1}행 형식 오류: {line}");
                    continue;
                }

                string glyphPath = $"{GlyphRoot}/{cells[2].Trim()}.png";
                var glyph = AssetDatabase.LoadAssetAtPath<Texture2D>(glyphPath);
                if (glyph == null)
                {
                    missing.Add(cells[2].Trim());
                    continue;
                }

                entries.Add(new IconLibrary.Entry { Kind = kind, Key = cells[1].Trim(), Glyph = glyph });
            }

            Undo.RecordObject(library, "아이콘 라이브러리 갱신");
            library.Entries = entries;
            library.Invalidate();
            EditorUtility.SetDirty(library);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database != null && database.Icons != library)
            {
                database.Icons = library;
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssets();
            IconFactory.Library = library;
            IconFactory.ClearCache();
            Debug.Log($"[DemoLA] 아이콘 라이브러리: {entries.Count}개" + (missing.Count > 0 ? $" · PNG 없음 {missing.Count}개 (python Tools/data/fetch_icons.py): {string.Join(", ", missing)}" : string.Empty));
            return library;
        }

        /// <summary>IconGlyphImporter 가 생기기 전에 가져온 PNG 는 Read/Write 가 꺼져 있다 → 다시 가져온다.</summary>
        private static void ensureGlyphImportSettings()
        {
            if (AssetDatabase.IsValidFolder(GlyphRoot) == false)
            {
                return;
            }

            bool stale = false;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { GlyphRoot }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer != null && (importer.isReadable == false || importer.maxTextureSize != 128))
                {
                    stale = true;
                    break;
                }
            }

            if (stale == true)
            {
                AssetDatabase.ImportAsset(GlyphRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            }
        }
    }

    /// <summary>Glyphs 폴더 PNG: IconFactory 가 픽셀을 읽어 합성하므로 Read/Write · 무압축 · 128px 로 가져온다.</summary>
    public class IconGlyphImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(IconLibraryBuilder.GlyphRoot + "/", StringComparison.Ordinal) == false)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
