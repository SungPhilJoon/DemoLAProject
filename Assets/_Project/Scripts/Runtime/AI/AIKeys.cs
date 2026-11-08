using Framework.StateTree;

namespace DemoLA.AI
{
    [StateTreeBlackboardEnum]
    public enum EAIBlackboard
    {
        None = 0,
        Target = 1,
        TargetDistance = 2,
        HealthPercent = 3,
        HpLine = 4,
        IsBusy = 5,
        IsGroggy = 6,
        InDanger = 7,

        /// <summary>1회성 플래그용 키 (SetFlagTask / FlagCondition). 100 이후를 자유롭게 쓴다.</summary>
        FlagStart = 100,
        Flag1 = 101,
        Flag2 = 102,
        Flag3 = 103,
        Flag4 = 104,
        Flag5 = 105,
        Flag6 = 106,
        Flag7 = 107,
        Flag8 = 108,
    }

    [StateTreeEventEnum]
    public enum EAIEvent
    {
        None = 0,
        HpLineChanged = 1,
        Countered = 2,
        StaggerBroken = 3,
        GimmickSucceeded = 4,
        GimmickFailed = 5,
        PatternEnded = 6,
        PhaseChanged = 7,
        StructureCollision = 8,
        Died = 9,
    }
}
