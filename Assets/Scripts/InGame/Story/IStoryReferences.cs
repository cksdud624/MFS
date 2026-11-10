namespace InGame.Story
{
    /// <summary>
    /// 이야기 검사가 이야기 밖의 표(Object, Character, Dialogue)와 에셋을 확인하는 창구.
    /// 이야기 시작 직전에는 TableManager로, 에디터 메뉴에서는 .bytes를 직접 읽어서 확인한다.
    /// 검사기가 어디서 데이터를 가져오는지 몰라도 되게 하려고 인터페이스로 둔다
    /// </summary>
    public interface IStoryReferences
    {
        bool HasObject(long objectId);
        //Object 표의 IsCharacter
        bool IsCharacter(long objectId);
        //Character 표에 같은 Id가 있는지. 캐릭터는 생성할 때 이 줄을 찾는다
        bool HasCharacter(long characterId);
        bool HasDialogue(long dialogueId);
        //오브젝트에 그 이름의 이펙트 프리팹이 있는지. 확인할 수 없는 곳(런타임)에서는 null을 돌려주고 검사를 건너뛴다
        bool? HasEffect(long objectId, string effectName);
    }
}
