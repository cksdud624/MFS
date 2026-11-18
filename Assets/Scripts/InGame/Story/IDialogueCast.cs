using Generated.Table;
using InGame.Object;

namespace InGame.Story
{
    /// <summary>
    /// 대사의 화자를 월드에서 찾아주는 쪽. 말풍선을 누구 머리 위에 띄울지 알려준다.
    /// DialoguePlayer가 이야기 전체(StoryContext)를 알지 않고 화자만 물어보게 하려고 인터페이스로 둔다
    /// </summary>
    public interface IDialogueCast
    {
        /// <summary>
        /// 대사를 말하는 캐릭터와 말풍선에 띄울 이름. 캐릭터를 찾지 못하면 false이고 speaker는 null이다.
        /// 이름은 표에서 오므로 찾지 못해도 돌려준다
        /// </summary>
        bool TryGetSpeaker(DialogueData line, out ObjectBase speaker, out string name);
    }
}
