namespace StoryTool
{
    /// <summary>명령이 Actor 칸을 쓰는지. 명령 클래스의 Parse와 같아야 한다</summary>
    public enum ActorUse
    {
        //쓰지 않는다. 값이 적혀 있으면 검사에 걸린다
        None,
        //비워도 된다 (대화 상대, 카메라가 볼 대상)
        Optional,
        //꼭 적어야 한다
        Required,
    }

    /// <summary>Param 칸에 무엇을 적는지. 툴이 고르는 칸을 무엇으로 보여줄지 정한다</summary>
    public enum ParamKind
    {
        //쓰지 않는다
        None,
        //위치. 배치 화면에서 끌거나 위치 입력 칸으로 고친다
        Position,
        //숫자 (초, 배율, 반경)
        Number,
        //글자 그대로 (이펙트 이름)
        Text,
        //Dialogue 표의 대화 번호 (DialogueId)
        Dialogue,
        //Dialogue 표의 대사 한 줄 (Id)
        DialogueLine,
        //Scenario 표의 시나리오 번호
        Scenario,
    }

    /// <summary>Param 칸 하나의 쓰임새. 고르는 칸의 이름표와 새 줄을 만들 때 넣어 둘 값을 든다</summary>
    public readonly struct ParamSpec
    {
        #region Fields
        public ParamKind Kind { get; }
        //고르는 칸에 붙이는 이름
        public string Label { get; }
        //명령을 골랐을 때 넣어 두는 값. 비워도 되는 칸은 빈 문자열이다
        public string Default { get; }
        #endregion

        #region Init
        public ParamSpec(ParamKind kind, string label, string defaultValue = "")
        {
            Kind = kind;
            Label = label;
            Default = defaultValue;
        }
        #endregion
    }
}
