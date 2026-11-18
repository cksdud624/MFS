using Generated.Table;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오나 퀘스트 표의 한 줄. 두 표는 칸 구성(Command, Actor, Zone, Param1~2)이 같아서 명령이 어느 표에서 왔는지 모르고 읽게 한다.
    /// 번호 칸(Scenario, Quest)은 줄을 묶는 데만 쓰므로 여기에 담지 않는다
    /// </summary>
    public readonly struct StoryRow
    {
        #region Fields
        public const string ScenarioTable = "Scenario";
        public const string QuestTable = "Quest";

        //에러와 로그에 붙이는 표 이름
        public string Table { get; }
        public long Id { get; }
        public string Command { get; }
        public string Actor { get; }
        //위치 칸(Param)의 기준 Zone. 위치를 쓰는 명령(Spawn, Move, Reach)만 읽는다
        public long Zone { get; }
        public string Param1 { get; }
        public string Param2 { get; }
        //플레이어에게 보여주는 글. 퀘스트 표에만 있고, 목표 줄이 화면의 퀘스트 안내에 띄울 내용을 적는다. 시나리오 줄은 늘 비어 있다
        public string Text { get; }
        #endregion

        #region Init
        private StoryRow(string table, long id, string command, string actor, long zone, string param1, string param2, string text)
        {
            Table = table;
            Id = id;
            Command = command;
            Actor = actor;
            Zone = zone;
            Param1 = param1;
            Param2 = param2;
            Text = text;
        }
        #endregion

        #region Public
        public static StoryRow From(ScenarioData data)
            => new(ScenarioTable, data.Id, data.Command, data.Actor, data.Zone, data.Param1, data.Param2, string.Empty);

        public static StoryRow From(QuestData data)
            => new(QuestTable, data.Id, data.Command, data.Actor, data.Zone, data.Param1, data.Param2, data.Text);

        public override string ToString() => $"{Table} {Id} {Command}";
        #endregion
    }
}
