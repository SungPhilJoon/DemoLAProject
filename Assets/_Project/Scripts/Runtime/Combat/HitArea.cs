using System;
using DemoLA.Attributes;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Combat
{
    /// <summary>판정 모양 (xlsx 목록 시트의 '판정모양' 과 대응).</summary>
    public enum EHitShape
    {
        Circle = 0,
        /// <summary>부채꼴. Angle = 전체 각도.</summary>
        Fan = 1,
        /// <summary>직선(사각형). 원점에서 정면으로 Length, 폭 Width.</summary>
        Rect = 2,
        /// <summary>도넛. InnerRadius ~ Radius.</summary>
        Donut = 3,
        /// <summary>전체 범위 (맵 전체).</summary>
        Global = 4,
    }

    [Serializable]
    public struct HitArea
    {
        public EHitShape Shape;
        public float Radius;
        public float InnerRadius;
        [Range(0.0f, 360.0f)] public float Angle;
        public float Width;
        public float Length;
        [Tooltip("시전자 정면 방향으로 판정 중심을 옮기는 거리")]
        public float ForwardOffset;

        public static HitArea Circle(float InRadius, float InForwardOffset = 0.0f)
        {
            var area = new HitArea();
            area.Shape = EHitShape.Circle;
            area.Radius = InRadius;
            area.ForwardOffset = InForwardOffset;
            return area;
        }

        public static HitArea Fan(float InRadius, float InAngle)
        {
            var area = new HitArea();
            area.Shape = EHitShape.Fan;
            area.Radius = InRadius;
            area.Angle = InAngle;
            return area;
        }

        public static HitArea Rect(float InLength, float InWidth)
        {
            var area = new HitArea();
            area.Shape = EHitShape.Rect;
            area.Length = InLength;
            area.Width = InWidth;
            return area;
        }

        public static HitArea Donut(float InInner, float InOuter)
        {
            var area = new HitArea();
            area.Shape = EHitShape.Donut;
            area.InnerRadius = InInner;
            area.Radius = InOuter;
            return area;
        }

        public static HitArea Global()
        {
            var area = new HitArea();
            area.Shape = EHitShape.Global;
            return area;
        }

        /// <summary>범위 배율 적용 (스킬 RangePercent).</summary>
        public HitArea Scaled(float InScale)
        {
            var area = this;
            area.Radius *= InScale;
            area.InnerRadius *= InScale;
            area.Width *= InScale;
            area.Length *= InScale;
            return area;
        }

        /// <summary>판정 대략 크기 (AI 사거리 판단용).</summary>
        public float Reach
        {
            get
            {
                switch (Shape)
                {
                    case EHitShape.Rect:
                        return Length + ForwardOffset;
                    case EHitShape.Global:
                        return 999.0f;
                    default:
                        return Radius + ForwardOffset;
                }
            }
        }
    }

    /// <summary>판정 기하 (XZ 평면, 순수 함수 — 테스트 대상).</summary>
    public static class HitGeometry
    {
        /// <summary>InPoint(반지름 InTargetRadius 원) 가 판정 안에 있는가. InForward 는 정규화하지 않아도 된다.</summary>
        public static bool Contains(in HitArea InArea, Vector3 InOrigin, Vector3 InForward, Vector3 InPoint, float InTargetRadius)
        {
            var forward = flat(InForward);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var center = flat(InOrigin) + forward * InArea.ForwardOffset;
            var delta = flat(InPoint) - center;
            float distance = delta.magnitude;

            switch (InArea.Shape)
            {
                case EHitShape.Global:
                    return true;
                case EHitShape.Circle:
                    return distance <= InArea.Radius + InTargetRadius;
                case EHitShape.Donut:
                    return distance <= InArea.Radius + InTargetRadius && distance >= InArea.InnerRadius - InTargetRadius;
                case EHitShape.Fan:
                    if (distance > InArea.Radius + InTargetRadius)
                    {
                        return false;
                    }

                    if (distance <= InTargetRadius)
                    {
                        return true;
                    }

                    float angle = Vector3.Angle(forward, delta);
                    // 대상 반지름만큼 각도 여유를 준다.
                    float slack = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp01(InTargetRadius / Mathf.Max(0.0001f, distance)));
                    return angle <= InArea.Angle * 0.5f + slack;
                case EHitShape.Rect:
                    float along = Vector3.Dot(delta, forward);
                    var right = new Vector3(forward.z, 0.0f, -forward.x);
                    float side = Mathf.Abs(Vector3.Dot(delta, right));
                    return along >= -InTargetRadius && along <= InArea.Length + InTargetRadius && side <= InArea.Width * 0.5f + InTargetRadius;
            }

            return false;
        }

        /// <summary>공격자 위치가 대상 기준 백/헤드 어느 쪽인가.</summary>
        public static EPositional ResolvePositional(Vector3 InTargetPosition, Vector3 InTargetForward, Vector3 InAttackerPosition)
        {
            var toAttacker = flat(InAttackerPosition - InTargetPosition);
            var forward = flat(InTargetForward);
            if (toAttacker.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                return EPositional.None;
            }

            float angle = Vector3.Angle(forward, toAttacker);
            var settings = StatFormula.Settings;
            if (angle >= settings.BackAttackAngle)
            {
                return EPositional.BackAttack;
            }

            if (angle <= settings.HeadAttackAngle)
            {
                return EPositional.HeadAttack;
            }

            return EPositional.None;
        }

        public static Vector3 Flat(Vector3 InVector)
        {
            return flat(InVector);
        }

        private static Vector3 flat(Vector3 InVector)
        {
            return new Vector3(InVector.x, 0.0f, InVector.z);
        }
    }
}
