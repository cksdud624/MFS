using System;
using System.Collections.Generic;
using UnityEngine;
using static Common.GameDefine;

namespace Generated.Table
{
    /// <summary>
    /// 판정을 화면에 보여줄 도형(Shape 칸)을 실행할 때 쓰는 모양으로 읽어둔 것.
    /// 표에는 이름(문자열)으로 적혀 있어서 불러올 때 한 번만 enum으로 바꿔둔다. 판정이 켜질 때마다 문자열을 비교하지 않게 한다.
    /// 칸에 적힌 이름이 틀린 줄은 에러를 남기고 도형이 없는 것으로 둔다
    /// </summary>
    public partial class AttackHitBoxRecord
    {
        #region Fields
        //도형을 적어둔 판정만 들어 있다. 없는 판정은 도형을 띄우지 않는다
        private readonly Dictionary<long, AttackShape> _shapes = new();
        #endregion

        #region Init
        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                if (string.IsNullOrEmpty(data.Shape)) continue;

                //숫자도 Enum.TryParse를 통과하므로 정의된 이름인지 따로 확인한다
                if (Enum.TryParse(data.Shape, out AttackShape shape) && Enum.IsDefined(typeof(AttackShape), shape))
                    _shapes[data.Id] = shape;
                else
                    Debug.LogError($"AttackHitBox {data.Id} : Shape must be Square, Triangle or Circle : '{data.Shape}'");
            }
        }
        #endregion

        #region Public
        /// <summary>판정을 보여줄 도형. Shape 칸을 비운 판정이면 None</summary>
        public AttackShape GetShape(AttackHitBoxData hitBox)
            => _shapes.TryGetValue(hitBox.Id, out var shape) ? shape : AttackShape.None;
        #endregion
    }
}
