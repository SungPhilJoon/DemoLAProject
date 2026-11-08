using System;
using UnityEngine;

namespace Framework.Testing
{
    /// <summary>
    /// [SerializeReference] 필드(또는 목록)에 붙이면 인스펙터에서 구현 형식을 고르는 드롭다운이 생긴다
    /// (Framework.Testing.Editor 의 SubclassSelectorDrawer). 단계 · 검증처럼 형식을 섞어 담는 목록에 쓴다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
