using Generated.Table;

namespace InGame.Story
{
    /// <summary>
    /// 런타임에 불러둔 표(TableManager)로 이야기 밖의 참조를 확인한다.
    /// 이펙트 프리팹은 Addressables 위치를 비동기로 찾아야 해서 여기서는 확인하지 않는다 (에디터 메뉴에서 확인한다)
    /// </summary>
    public class TableStoryReferences : IStoryReferences
    {
        #region Fields
        private readonly TableManager _tableManager;
        #endregion

        #region Init
        public TableStoryReferences(TableManager tableManager)
        {
            _tableManager = tableManager;
        }
        #endregion

        #region Public
        public bool HasObject(long objectId) => _tableManager.ObjectRecord.GetRecord(objectId) != null;
        public bool IsCharacter(long objectId) => _tableManager.ObjectRecord.GetRecord(objectId)?.IsCharacter ?? false;
        public bool HasCharacter(long characterId) => _tableManager.CharacterRecord.GetRecord(characterId) != null;
        public bool HasDialogue(long dialogueId) => _tableManager.DialogueRecord.GetLines(dialogueId) != null;
        public bool? HasEffect(long objectId, string effectName) => null;
        #endregion
    }
}
