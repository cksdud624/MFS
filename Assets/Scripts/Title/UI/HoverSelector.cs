using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Title.UI
{
    /// <summary>
    /// 마우스를 올리면 그 버튼을 선택한다.
    /// 그냥 두면 키보드로 고른 버튼과 마우스를 올린 버튼이 따로 강조되어 메뉴 두 개가 고른 것처럼 보인다. 선택을 하나로 맞춘다
    /// </summary>
    public class HoverSelector : MonoBehaviour, IPointerEnterHandler
    {
        #region Fields
        [SerializeField] private Selectable _selectable;
        #endregion

        #region Event Handlers
        public void OnPointerEnter(PointerEventData eventData)
        {
            //고를 수 없는 버튼은 마우스를 올려도 선택하지 않는다
            if (_selectable.IsInteractable())
                _selectable.Select();
        }
        #endregion
    }
}
