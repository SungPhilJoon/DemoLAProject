using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using Unity.AI.Navigation;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>
    /// 레이드 바닥. 자식 ArenaTile 을 격자로 찾아 "이 위치에 바닥이 있는가" 를 답한다.
    /// 바닥이 없는 곳에 선 플레이어는 낙사(즉사)한다. 타일이 무너지면 NavMesh 를 비동기로 다시 굽는다.
    /// </summary>
    public class ArenaFloor : BaseBehaviour
    {
        public static ArenaFloor Current { get; private set; }

        [SerializeField] private float tileSize = 4.0f;
        [SerializeField] private float fallDelay = 0.6f;
        [SerializeField] private NavMeshSurface navMeshSurface;

        private readonly Dictionary<Vector2Int, ArenaTile> grid = new Dictionary<Vector2Int, ArenaTile>();
        private readonly List<ArenaTile> tiles = new List<ArenaTile>();
        private readonly Dictionary<CombatActor, float> falling = new Dictionary<CombatActor, float>();
        private readonly List<CombatActor> buffer = new List<CombatActor>();
        private readonly List<CombatActor> removeBuffer = new List<CombatActor>();
        private AsyncOperation navMeshUpdate;
        private bool navMeshDirty;

        public Vector3 Center
        {
            get { return transform.position; }
        }

        public float TileSize
        {
            get { return tileSize; }
        }

        public IReadOnlyList<ArenaTile> Tiles
        {
            get { return tiles; }
        }

        public NavMeshSurface Surface
        {
            get { return navMeshSurface; }
            set { navMeshSurface = value; }
        }

        protected override void OnUnityAwake()
        {
            Current = this;
            Rebuild();
        }

        protected override void OnUnityDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Rebuild()
        {
            grid.Clear();
            tiles.Clear();
            GetComponentsInChildren(true, tiles);
            for (int i = 0; i < tiles.Count; ++i)
            {
                grid[toCell(tiles[i].transform.position)] = tiles[i];
            }
        }

        public bool IsSolid(Vector3 InPosition)
        {
            if (grid.Count == 0)
            {
                return true;
            }

            ArenaTile tile;
            return grid.TryGetValue(toCell(InPosition), out tile) == true && tile != null && tile.IsCollapsed == false;
        }

        public void Collapse(ETileGroup InGroup, bool InKeepSurvivors)
        {
            for (int i = 0; i < tiles.Count; ++i)
            {
                var tile = tiles[i];
                if (tile.Group != InGroup || (InKeepSurvivors == true && tile.SurvivesRingCollapse == true))
                {
                    continue;
                }

                tile.Collapse();
            }

            navMeshDirty = true;
            dropUnsupportedStructures();
            checkFalling(0.0f);
        }

        /// <summary>2차 파괴: 중앙(및 SurvivesRingCollapse)을 뺀 전부.</summary>
        public void CollapseAllExceptCenter()
        {
            for (int i = 0; i < tiles.Count; ++i)
            {
                var tile = tiles[i];
                if (tile.Group == ETileGroup.Center || tile.SurvivesRingCollapse == true)
                {
                    continue;
                }

                tile.Collapse();
            }

            navMeshDirty = true;
            dropUnsupportedStructures();
            checkFalling(0.0f);
        }

        /// <summary>바닥이 무너진 자리에 서 있던 기둥 · 벽은 함께 무너진다 (구슬 없음).</summary>
        private void dropUnsupportedStructures()
        {
            var structures = ArenaStructure.Structures;
            for (int i = structures.Count - 1; i >= 0; --i)
            {
                var structure = structures[i];
                if (structure.IsDestroyed == false && IsSolid(structure.transform.position) == false)
                {
                    structure.Break();
                }
            }
        }

        public void RestoreAll()
        {
            for (int i = 0; i < tiles.Count; ++i)
            {
                tiles[i].Restore();
            }

            navMeshDirty = true;
        }

        private void Update()
        {
            if (navMeshDirty == true && navMeshSurface != null && (navMeshUpdate == null || navMeshUpdate.isDone == true))
            {
                navMeshDirty = false;
                if (navMeshSurface.navMeshData != null)
                {
                    navMeshUpdate = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
                }
            }

            checkFalling(Time.deltaTime);
        }

        private void checkFalling(float InDeltaTime)
        {
            CombatRegistry.FindTeam(ETeam.Player, buffer);
            for (int i = 0; i < buffer.Count; ++i)
            {
                var actor = buffer[i];
                // 경기장에서 멀리 떨어진 곳(레이드 입장 대기실)에 있는 캐릭터는 낙하 판정 대상이 아니다.
                var offset = actor.Position - transform.position;
                offset.y = 0.0f;
                if (falling.ContainsKey(actor) == false && offset.magnitude > ArenaBuilder.Radius + 15.0f)
                {
                    continue;
                }

                if (falling.ContainsKey(actor) == false && (actor.HasTag((int)ETag.State_Dashing) == true || IsSolid(actor.Position) == true))
                {
                    falling.Remove(actor);
                    continue;
                }

                // 낙사는 "몬스터 공격에 맞아 위치가 바뀌어 맵 밖으로 날아간" 경우에만.
                //  - 날아가는 중(공중)이면 아직 판정하지 않는다.
                //  - 넉백 없이 발밑이 무너졌거나 바닥 밖에 있으면 가장 가까운 바닥으로 옮긴다.
                if (falling.ContainsKey(actor) == false)
                {
                    var motor = actor.Motor;
                    if (motor != null && motor.IsKnockedBack == true)
                    {
                        continue;
                    }

                    bool knockedOff = motor != null && Time.time - motor.LastKnockbackEndTime < 0.5f;
                    if (knockedOff == false)
                    {
                        rescue(actor);
                        continue;
                    }
                }

                float time;
                if (falling.TryGetValue(actor, out time) == false)
                {
                    // NavMesh 가 다시 구워지면 에이전트가 남은 바닥으로 순간이동하므로, 떨어지기 시작한 순간 멈춘다.
                    actor.AddTag((int)ETag.State_Falling);
                    if (actor.Motor != null && actor.Motor.Agent != null)
                    {
                        actor.Motor.Agent.enabled = false;
                    }
                }

                time += InDeltaTime;
                falling[actor] = time;
                if (time >= fallDelay)
                {
                    falling.Remove(actor);
                    Raid.Message($"{actor.GetName()} 낙사", 2.0f, true);
                    actor.Kill();
                    actor.RemoveTag((int)ETag.State_Falling);
                }
            }

            removeBuffer.Clear();
            foreach (var pair in falling)
            {
                if (pair.Key == null || pair.Key.IsDead == true)
                {
                    removeBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeBuffer.Count; ++i)
            {
                falling.Remove(removeBuffer[i]);
            }
        }

        /// <summary>가장 가까운 무너지지 않은 바닥 타일 위로 옮긴다.</summary>
        private void rescue(CombatActor InActor)
        {
            ArenaTile best = null;
            float bestDistance = float.MaxValue;
            var position = InActor.Position;
            for (int i = 0; i < tiles.Count; ++i)
            {
                var tile = tiles[i];
                if (tile == null || tile.IsCollapsed == true)
                {
                    continue;
                }

                var delta = tile.transform.position - position;
                delta.y = 0.0f;
                float distance = delta.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tile;
                }
            }

            if (best != null && InActor.Motor != null)
            {
                var target = best.transform.position;
                InActor.Motor.Teleport(new Vector3(target.x, 0.0f, target.z));
            }
        }

        private Vector2Int toCell(Vector3 InPosition)
        {
            var local = InPosition - transform.position;
            return new Vector2Int(Mathf.FloorToInt(local.x / tileSize + 0.5f), Mathf.FloorToInt(local.z / tileSize + 0.5f));
        }
    }
}
