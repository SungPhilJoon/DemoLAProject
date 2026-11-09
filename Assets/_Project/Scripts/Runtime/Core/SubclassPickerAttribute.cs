using UnityEngine;

namespace DemoLA.Core
{
    /// <summary>
    /// [SerializeReference] 필드에 붙이면 인스펙터에 "구현 클래스 고르기" 드롭다운이 생긴다 (DemoLA.Editor.SubclassPickerDrawer).
    /// 필드 타입을 상속한 [Serializable] · 비추상 · 기본 생성자 있는 클래스가 목록에 나온다. "(없음)" = null.
    /// </summary>
    public class SubclassPickerAttribute : PropertyAttribute
    {
    }
}
