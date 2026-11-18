using UnityEngine;

namespace InGame.Story
{
    /// <summary>
    /// 지금 기다리는 퀘스트 목표가 가리키는 곳. 목표 명령이 기다리기 시작할 때 StoryContext에 걸어두고, 화면의 목표 아이콘이 읽는다.
    /// 목표마다 가리키는 것이 달라서(대상 하나, 월드의 적 전부, 지점) 종류로 나눈다
    /// </summary>
    public readonly struct StoryGoal
    {
        #region Types
        public enum TargetKind
        {
            //기다리는 목표가 없다
            None,
            //대상 하나 (말을 걸 Npc, 쓰러뜨릴 적)
            Actor,
            //월드의 적 전부
            Enemies,
            //월드의 한 지점 (가야 할 곳)
            Point,
        }
        #endregion

        #region Fields
        public static readonly StoryGoal None = default;

        public TargetKind Target { get; }
        //Target이 Actor일 때 가리키는 대상
        public StoryActor Actor { get; }
        //Target이 Point일 때 가리키는 지점 (월드 좌표, 발밑 기준)
        public Vector2 Point { get; }
        #endregion

        #region Init
        private StoryGoal(TargetKind target, StoryActor actor, Vector2 point)
        {
            Target = target;
            Actor = actor;
            Point = point;
        }
        #endregion

        #region Public
        public static StoryGoal ForActor(StoryActor actor) => new(TargetKind.Actor, actor, Vector2.zero);
        public static StoryGoal ForEnemies() => new(TargetKind.Enemies, StoryActor.None, Vector2.zero);
        public static StoryGoal ForPoint(Vector2 point) => new(TargetKind.Point, StoryActor.None, point);
        #endregion
    }
}
