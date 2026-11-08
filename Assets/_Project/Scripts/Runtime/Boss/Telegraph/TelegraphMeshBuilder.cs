using System.Collections.Generic;
using DemoLA.Combat;
using UnityEngine;

namespace DemoLA.Boss.Telegraph
{
    /// <summary>HitArea → 바닥 장판 메시 (로컬 원점 = 판정 원점, +Z = 정면). 같은 모양은 캐시해서 재사용한다.</summary>
    public static class TelegraphMeshBuilder
    {
        private struct Key
        {
            public EHitShape Shape;
            public int A;
            public int B;
            public int C;

            public override int GetHashCode()
            {
                return ((int)Shape * 73856093) ^ (A * 19349663) ^ (B * 83492791) ^ C;
            }

            public override bool Equals(object InOther)
            {
                if ((InOther is Key) == false)
                {
                    return false;
                }

                var other = (Key)InOther;
                return other.Shape == Shape && other.A == A && other.B == B && other.C == C;
            }
        }

        private const float GlobalRadius = 45.0f;
        private static readonly Dictionary<Key, Mesh> Cache = new Dictionary<Key, Mesh>();

        public static Mesh Get(in HitArea InArea)
        {
            var key = new Key();
            key.Shape = InArea.Shape;
            key.A = Mathf.RoundToInt(InArea.Radius * 100.0f) + Mathf.RoundToInt(InArea.Length * 100.0f) * 10000;
            key.B = Mathf.RoundToInt(InArea.InnerRadius * 100.0f) + Mathf.RoundToInt(InArea.Width * 100.0f) * 10000;
            key.C = Mathf.RoundToInt(InArea.Angle * 10.0f) + Mathf.RoundToInt(InArea.ForwardOffset * 100.0f) * 10000;

            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) == true && mesh != null)
            {
                return mesh;
            }

            mesh = Build(InArea);
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Build(in HitArea InArea)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var offset = new Vector3(0.0f, 0.0f, InArea.ForwardOffset);
            switch (InArea.Shape)
            {
                case EHitShape.Circle:
                    addArc(vertices, triangles, offset, 0.0f, InArea.Radius, 360.0f, 48);
                    break;
                case EHitShape.Global:
                    addArc(vertices, triangles, Vector3.zero, 0.0f, GlobalRadius, 360.0f, 64);
                    break;
                case EHitShape.Donut:
                    addArc(vertices, triangles, offset, InArea.InnerRadius, InArea.Radius, 360.0f, 48);
                    break;
                case EHitShape.Fan:
                    addArc(vertices, triangles, offset, 0.0f, InArea.Radius, Mathf.Clamp(InArea.Angle, 1.0f, 360.0f), 32);
                    break;
                case EHitShape.Rect:
                    float half = InArea.Width * 0.5f;
                    vertices.Add(offset + new Vector3(-half, 0.0f, 0.0f));
                    vertices.Add(offset + new Vector3(-half, 0.0f, InArea.Length));
                    vertices.Add(offset + new Vector3(half, 0.0f, InArea.Length));
                    vertices.Add(offset + new Vector3(half, 0.0f, 0.0f));
                    triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
                    break;
            }

            var mesh = new Mesh();
            mesh.name = $"Telegraph_{InArea.Shape}";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void addArc(List<Vector3> OutVertices, List<int> OutTriangles, Vector3 InCenter, float InInner, float InOuter, float InAngle, int InSegments)
        {
            float start = -InAngle * 0.5f;
            float step = InAngle / InSegments;
            int baseIndex = OutVertices.Count;
            for (int i = 0; i <= InSegments; ++i)
            {
                float radians = (start + step * i) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(radians), 0.0f, Mathf.Cos(radians));
                OutVertices.Add(InCenter + direction * InInner);
                OutVertices.Add(InCenter + direction * InOuter);
            }

            for (int i = 0; i < InSegments; ++i)
            {
                int a = baseIndex + i * 2;
                OutTriangles.Add(a);
                OutTriangles.Add(a + 1);
                OutTriangles.Add(a + 3);
                OutTriangles.Add(a);
                OutTriangles.Add(a + 3);
                OutTriangles.Add(a + 2);
            }
        }
    }
}
