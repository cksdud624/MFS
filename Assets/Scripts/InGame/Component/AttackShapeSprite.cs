using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 공격 판정을 보여주는 도형의 스프라이트. 그림 파일을 두지 않고 처음 쓸 때 텍스처를 그려서 만든다.
    /// 임시 표시에 쓰는 것이라 에셋으로 관리할 것을 늘리지 않으려는 것이다. 이펙트 그림이 생기면 이 클래스째 지운다.
    /// 스프라이트는 가로세로 1U라서, 판정의 크기를 그대로 스케일에 넣으면 판정과 같은 크기로 보인다
    /// </summary>
    public static class AttackShapeSprite
    {
        #region Fields
        //텍스처 한 변의 픽셀 수. 도형은 크게 늘려서 쓰므로 가장자리가 계단져 보이지 않을 만큼만 둔다
        private const int TextureSize = 64;

        //도형마다 하나씩 만들어 같이 쓴다. 순번이 AttackShape다
        private static readonly Sprite[] Sprites = new Sprite[4];
        #endregion

        #region Public
        /// <summary>
        /// 도형의 스프라이트. None이면 null이다.
        /// 런타임에 만든 에셋은 씬이 내려가며 정리될 수 있어서 파괴되었으면 다시 만든다
        /// </summary>
        public static Sprite Get(AttackShape shape)
        {
            if (shape == AttackShape.None) return null;

            int index = (int)shape;
            if (Sprites[index] == null)
                Sprites[index] = Create(shape);
            return Sprites[index];
        }
        #endregion

        #region Private
        private static Sprite Create(AttackShape shape)
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    //픽셀의 가운데를 0 ~ 1의 자리로 본다
                    float u = (x + 0.5f) / TextureSize;
                    float v = (y + 0.5f) / TextureSize;
                    byte alpha = (byte)(GetCoverage(shape, u, v) * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"AttackShape {shape}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            //텍스처 전체가 1U가 되게 한다. 메시는 사각형 그대로 둔다. 윤곽을 따라 메시를 만들 만큼 복잡한 그림이 아니다
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                TextureSize, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>
        /// 그 자리가 도형 안에 얼마나 들어 있는지 (0 ~ 1). 가장자리 한 픽셀은 걸친 만큼만 칠해서 계단이 지지 않게 한다.
        /// 세모는 왼쪽 변이 밑변이고 오른쪽 가운데가 꼭짓점이다
        /// </summary>
        private static float GetCoverage(AttackShape shape, float u, float v)
        {
            switch (shape)
            {
                case AttackShape.Circle:
                    float distance = new Vector2(u - 0.5f, v - 0.5f).magnitude;
                    return Mathf.Clamp01((0.5f - distance) * TextureSize);
                case AttackShape.Triangle:
                    float halfHeight = 0.5f * (1f - u);
                    return Mathf.Clamp01((halfHeight - Mathf.Abs(v - 0.5f)) * TextureSize);
                default:
                    return 1f;
            }
        }
        #endregion
    }
}
