using System.Globalization;

namespace StoryTool
{
    /// <summary>
    /// 줄이 어느 표의 것인지. 이름은 표 이름과 같다. 시나리오와 퀘스트 줄은 칸 구성이 같고(명령 하나),
    /// Actor 줄은 대상 하나의 등록, Talk 줄은 일반 대화 하나(누구에게, 어느 퀘스트부터, 어떤 대화)다
    /// </summary>
    public enum StoryLineKind
    {
        Actor,
        Quest,
        Scenario,
        Talk,
    }

    /// <summary>
    /// 이야기 표(Scenario, Quest, Actor, Talk)의 한 줄. 칸을 복사해 두지 않고 문서의 표를 그때그때 읽고 고친다.
    /// 문서의 표는 Undo와 도메인 리로드 때 새 객체로 바뀔 수 있어서, 표를 직접 들지 않고 문서 안의 순번으로 찾아간다
    /// </summary>
    public class StoryLine
    {
        #region Fields
        private readonly StoryToolDocument _document;

        public StoryLineKind Kind { get; }
        //문서의 표 목록에서 몇 번째 표인지
        public int TableIndex { get; }
        //그 표에서 몇 번째 데이터 줄인지
        public int Row { get; }

        //화면이 고른 줄을 기억할 때 쓰는 키. 도메인 리로드 뒤에도 같은 줄을 다시 찾는다
        public string Key => $"{Kind}:{IdText}";
        public string IdText => Get(StoryToolDocument.ColumnId);
        public long Id => ParseLong(IdText);
        //명령은 시나리오와 퀘스트 줄에만 있다
        public string Command => Kind == StoryLineKind.Scenario || Kind == StoryLineKind.Quest ? Get(StoryToolDocument.ColumnCommand) : string.Empty;
        //줄이 가리키는 대상 (Player 또는 Actor 번호). Actor 줄은 자기 자신이고, Talk 줄은 말을 걸 Npc다
        public string Actor => Kind == StoryLineKind.Actor ? IdText : Get(StoryToolDocument.ColumnActor);
        public string Param1 => Get(StoryToolDocument.ColumnParam1);
        public string Param2 => Get(StoryToolDocument.ColumnParam2);
        public string Text => Get(StoryToolDocument.ColumnText);
        public string Note => Get(StoryToolDocument.ColumnNote);
        //에러와 화면에 붙이는 표 이름
        public string TableName => _document.Tables[TableIndex].Name;
        #endregion

        #region Init
        public StoryLine(StoryToolDocument document, StoryLineKind kind, int tableIndex, int row)
        {
            _document = document;
            Kind = kind;
            TableIndex = tableIndex;
            Row = row;
        }
        #endregion

        #region Public
        public string Get(string column) => _document.Tables[TableIndex].Get(Row, column);

        /// <summary>칸을 고친다. 값이 실제로 바뀌었으면 true. Undo에 남기는 것은 StoryEditor가 한다</summary>
        public bool Set(string column, string value) => _document.SetCell(TableIndex, Row, column, value);

        /// <summary>표의 번호 칸을 읽는다. 숫자가 아니면 0 (형식이 틀린 칸은 검사가 에러로 알린다)</summary>
        public static long ParseLong(string raw)
            => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
        #endregion
    }
}
