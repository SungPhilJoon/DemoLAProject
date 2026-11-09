using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemoLA.UI
{
    /// <summary>
    /// 아이콘 그림(글리프) 목록. IconFactory 가 (종류, 키) 로 글리프를 찾아 종류별 틀 · 등급 테두리를 입혀 스프라이트를 만든다.
    /// 에셋: Assets/_Project/Art/Icons/IconLibrary.asset — IconManifest.csv 를 고친 뒤 DemoLA/Data/아이콘 라이브러리 갱신 으로 다시 만든다.
    /// 키 규칙: 정확한 이름(스킬 · 각인 · 카드 이름 등) &gt; "*" (그 종류의 기본 글리프) &gt; "?" 풀 (이름 해시로 하나 고름).
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/UI/Icon Library", fileName = "IconLibrary")]
    public class IconLibrary : ScriptableObject
    {
        public const string DefaultKey = "*";
        public const string PoolKey = "?";

        [Serializable]
        public class Entry
        {
            public EIconKind Kind;
            public string Key;
            [Tooltip("흰 글리프 + 투명 배경 (Read/Write 켜짐)")]
            public Texture2D Glyph;
        }

        public List<Entry> Entries = new List<Entry>();

        [NonSerialized] private Dictionary<string, Texture2D> exact;
        [NonSerialized] private Dictionary<EIconKind, List<Texture2D>> pools;

        public Texture2D Find(EIconKind InKind, string InKey)
        {
            ensureIndex();
            Texture2D glyph;
            if (InKey != null && exact.TryGetValue(indexKey(InKind, InKey), out glyph) == true)
            {
                return glyph;
            }

            List<Texture2D> pool;
            if (InKey != null && pools.TryGetValue(InKind, out pool) == true && pool.Count > 0)
            {
                return pool[(int)((uint)stableHash(InKey) % (uint)pool.Count)];
            }

            return exact.TryGetValue(indexKey(InKind, DefaultKey), out glyph) == true ? glyph : null;
        }

        /// <summary>에디터에서 Entries 를 바꾼 뒤 호출 (런타임 캐시 초기화).</summary>
        public void Invalidate()
        {
            exact = null;
            pools = null;
        }

        private void OnValidate()
        {
            Invalidate();
        }

        private void ensureIndex()
        {
            if (exact != null)
            {
                return;
            }

            exact = new Dictionary<string, Texture2D>();
            pools = new Dictionary<EIconKind, List<Texture2D>>();
            for (int i = 0; i < Entries.Count; ++i)
            {
                var entry = Entries[i];
                if (entry == null || entry.Glyph == null || string.IsNullOrEmpty(entry.Key) == true)
                {
                    continue;
                }

                if (entry.Key == PoolKey)
                {
                    List<Texture2D> pool;
                    if (pools.TryGetValue(entry.Kind, out pool) == false)
                    {
                        pool = new List<Texture2D>();
                        pools[entry.Kind] = pool;
                    }

                    pool.Add(entry.Glyph);
                    continue;
                }

                exact[indexKey(entry.Kind, entry.Key)] = entry.Glyph;
            }
        }

        private static string indexKey(EIconKind InKind, string InKey)
        {
            return $"{(int)InKind}|{InKey.Trim()}";
        }

        /// <summary>string.GetHashCode 는 실행마다 달라질 수 있어 직접 계산한다.</summary>
        private static int stableHash(string InText)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < InText.Length; ++i)
                {
                    hash = hash * 31 + InText[i];
                }

                return hash & 0x7FFFFFFF;
            }
        }
    }
}
