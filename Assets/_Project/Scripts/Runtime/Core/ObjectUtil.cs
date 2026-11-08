using UnityEngine;

namespace DemoLA.Core
{
    public static class ObjectUtil
    {
        /// <summary>플레이 중이면 Destroy, 에디터 정지 상태(EditMode 테스트, 에디터 셋업)면 DestroyImmediate.</summary>
        public static void SafeDestroy(Object InObject)
        {
            if (InObject == null)
            {
                return;
            }

            if (Application.isPlaying == true)
            {
                Object.Destroy(InObject);
            }
            else
            {
                Object.DestroyImmediate(InObject);
            }
        }
    }
}
