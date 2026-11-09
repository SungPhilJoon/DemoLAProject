using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 패턴 ID 문자열 필드 (StateTree 노드의 PatternId 등). 에디터에서 패턴 목록 드롭다운(◆ = 카운터 가능)과
    /// "GAS" 버튼(GAS Tool 에서 그 패턴 어빌리티 열기)으로 그린다 — DemoLA.Editor.Gas.PatternIdDrawer.
    /// </summary>
    public class PatternIdAttribute : PropertyAttribute
    {
    }
}
