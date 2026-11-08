using System.Collections.Generic;
using UnityEngine;

namespace DemoLA.Build
{
    /// <summary>
    /// 각인 포인트 합산 → 레벨. 출처: 각인서(슬롯 수 제한) + 악세사리 2줄 + 어빌리티 스톤 2줄 (+ 감소 각인).
    /// 레벨 = min(MaxLevel, 포인트 / PointsPerLevel).
    /// </summary>
    public static class EngravingCalculator
    {
        public static Dictionary<int, int> ComputePoints(CharacterBuildData InBuild, EngravingRules InRules)
        {
            var points = new Dictionary<int, int>();
            int books = 0;
            for (int i = 0; i < InBuild.Books.Count && books < InRules.BookSlots; ++i)
            {
                var book = InBuild.Books[i];
                if (book == null || book.EngravingId == 0)
                {
                    continue;
                }

                add(points, book.EngravingId, Mathf.Clamp(book.Points, 0, InRules.MaxBookPoints));
                books += 1;
            }

            for (int i = 0; i < InBuild.Accessories.Count; ++i)
            {
                var accessory = InBuild.Accessories[i];
                add(points, accessory.EngravingA, accessory.PointsA);
                add(points, accessory.EngravingB, accessory.PointsB);
                add(points, accessory.NegativeEngraving, accessory.NegativePoints);
            }

            var stone = InBuild.Stone;
            if (stone != null)
            {
                add(points, stone.EngravingA, Mathf.Clamp(stone.PointsA, 0, InRules.MaxStonePoints));
                add(points, stone.EngravingB, Mathf.Clamp(stone.PointsB, 0, InRules.MaxStonePoints));
                add(points, stone.NegativeEngraving, Mathf.Clamp(stone.NegativePoints, 0, InRules.MaxStonePoints));
            }

            return points;
        }

        public static int LevelOf(int InPoints, EngravingRules InRules)
        {
            if (InRules.PointsPerLevel <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(InPoints / InRules.PointsPerLevel, 0, InRules.MaxLevel);
        }

        public static Dictionary<int, int> ComputeLevels(CharacterBuildData InBuild, EngravingRules InRules)
        {
            var levels = new Dictionary<int, int>();
            foreach (var pair in ComputePoints(InBuild, InRules))
            {
                int level = LevelOf(pair.Value, InRules);
                if (level > 0)
                {
                    levels[pair.Key] = level;
                }
            }

            return levels;
        }

        private static void add(Dictionary<int, int> InOutPoints, int InId, int InPoints)
        {
            if (InId == 0 || InPoints <= 0)
            {
                return;
            }

            int current;
            InOutPoints.TryGetValue(InId, out current);
            InOutPoints[InId] = current + InPoints;
        }
    }
}
