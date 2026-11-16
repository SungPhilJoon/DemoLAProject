using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 패턴 태그(ETag) 필드 · 리스트 (StateTree 노드의 PatternTag, 패턴의 후속 패턴 등).
    /// 에디터에서 Pattern 하위 태그만 계층 드롭다운(Pattern/G1/OrbStagger, ◆ = 카운터 가능)과
    /// "GAS" 버튼(GAS Tool 에서 그 패턴 어빌리티 열기)으로 그린다 — DemoLA.Editor.Gas.PatternTagDrawer.
    /// </summary>
    public class PatternTagAttribute : PropertyAttribute
    {
    }
}
