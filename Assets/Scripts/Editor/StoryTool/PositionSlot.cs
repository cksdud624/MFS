namespace StoryTool
{
    /// <summary>명령이 위치 칸을 어떻게 쓰는지. 명령 클래스의 Parse와 같아야 한다</summary>
    public enum PositionKind
    {
        //위치를 쓰지 않는다
        None,
        //Zone + Param1에 x,y를 적는다 (Place, Reach)
        Point,
        //Zone + Param1에 x,y를 적거나, 둘 다 비워서 Actor 표의 자리를 쓴다 (Spawn)
        OptionalPoint,
        //Zone + Param1에 x만 적는다. y는 대상이 서 있던 높이 그대로다 (Move)
        X,
    }

    /// <summary>
    /// 줄의 위치가 실제로 적혀 있는 칸. 배치 화면에서 끌어 놓으면 이 칸을 고친다.
    /// 줄의 칸이 아닐 수도 있다. 위치를 비운 Spawn은 Actor 표의 자리에 서므로, 그 줄을 옮기면 Actor 표의 칸이 바뀐다
    /// </summary>
    public readonly struct PositionSlot
    {
        #region Fields
        //문서의 표 목록에서 몇 번째 표인지
        public int TableIndex { get; }
        public int Row { get; }
        public string ZoneColumn { get; }
        public string ValueColumn { get; }
        //x만 적는 칸인지 (Move)
        public bool IsXOnly { get; }
        //줄이 아니라 Actor 표의 자리를 가리키는지. 같은 대상을 쓰는 다른 줄도 같이 바뀐다는 것을 화면이 알려야 한다
        public bool IsActorTable { get; }
        #endregion

        #region Init
        public PositionSlot(int tableIndex, int row, string zoneColumn, string valueColumn, bool isXOnly, bool isActorTable)
        {
            TableIndex = tableIndex;
            Row = row;
            ZoneColumn = zoneColumn;
            ValueColumn = valueColumn;
            IsXOnly = isXOnly;
            IsActorTable = isActorTable;
        }
        #endregion
    }
}
