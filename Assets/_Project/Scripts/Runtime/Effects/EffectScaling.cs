using System.Collections.Generic;
using Framework.Ability;

namespace DemoLA.Effects
{
    /// <summary>
    /// 정의의 모디파이어 크기만 InScale 배 한 복사본 (트라이포드 레벨, 보석 레벨처럼 같은 효과의 세기만 다른 경우).
    /// (원본, 배율) 쌍마다 한 번만 만든다. 정의는 런타임에 수정하지 않는다는 프레임워크 규칙을 지키기 위함.
    /// </summary>
    public static class EffectScaling
    {
        private struct Key
        {
            public GameplayEffectDef Def;
            public int ScaleKey;
        }

        private class KeyComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key InA, Key InB)
            {
                return ReferenceEquals(InA.Def, InB.Def) && InA.ScaleKey == InB.ScaleKey;
            }

            public int GetHashCode(Key InKey)
            {
                return (InKey.Def != null ? InKey.Def.GetHashCode() : 0) * 397 ^ InKey.ScaleKey;
            }
        }

        private static readonly Dictionary<Key, GameplayEffectDef> Cache = new Dictionary<Key, GameplayEffectDef>(new KeyComparer());

        public static GameplayEffectDef Scaled(GameplayEffectDef InDef, float InScale)
        {
            if (InDef == null || System.Math.Abs(InScale - 1.0f) < 0.0001f)
            {
                return InDef;
            }

            var key = new Key();
            key.Def = InDef;
            key.ScaleKey = (int)System.Math.Round(InScale * 10000.0f);
            GameplayEffectDef scaled;
            if (Cache.TryGetValue(key, out scaled) == true)
            {
                return scaled;
            }

            scaled = new GameplayEffectDef();
            scaled.Id = EffectBuilder.AllocateId();
            scaled.DurationPolicy = InDef.DurationPolicy;
            scaled.Duration = InDef.Duration;
            scaled.Period = InDef.Period;
            scaled.ExecutePeriodicOnApply = InDef.ExecutePeriodicOnApply;
            scaled.ExecutionId = InDef.ExecutionId;
            scaled.GrantedTags = InDef.GrantedTags;
            scaled.ApplicationRequiredTags = InDef.ApplicationRequiredTags;
            scaled.ApplicationBlockedTags = InDef.ApplicationBlockedTags;
            scaled.RemoveEffectsWithTags = InDef.RemoveEffectsWithTags;
            scaled.StackPolicy = InDef.StackPolicy;
            scaled.MaxStack = InDef.MaxStack;
            scaled.CueId = InDef.CueId;
            scaled.IsCooldown = InDef.IsCooldown;
            scaled.Modifiers = new ModifierDef[InDef.Modifiers.Length];
            for (int i = 0; i < InDef.Modifiers.Length; ++i)
            {
                var modifier = InDef.Modifiers[i];
                modifier.Value *= InScale;
                scaled.Modifiers[i] = modifier;
            }

            Cache.Add(key, scaled);
            return scaled;
        }
    }
}
