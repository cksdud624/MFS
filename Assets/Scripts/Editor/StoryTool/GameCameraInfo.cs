using InGame;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 게임 카메라의 처음 상태. 게임은 Main 씬에 놓인 카메라의 처음 화각을 배율 1로 보므로(CameraDirector), 툴도 그 카메라에서 읽는다.
    /// 배치 화면에 "게임에서 이만큼 보인다"는 범위를 그릴 때 쓴다
    /// </summary>
    public readonly struct GameCameraInfo
    {
        #region Fields
        private const string ScenePathFormat = "Assets/Scenes/{0}.unity";
        private const string CameraTag = "MainCamera";
        //화면 비율은 실행하는 창의 크기에 달려 있어서 씬에는 없다. UI의 기준 해상도(1920×1080)와 같은 비율로 본다
        private const float Aspect = 16f / 9f;

        //Main 씬에서 카메라를 찾았는지. 못 찾았으면 화면 범위를 그리지 않는다
        public bool IsValid { get; }
        //플레이어가 아직 없을 때 카메라가 있는 자리
        public Vector2 Position { get; }

        private readonly bool _isOrthographic;
        private readonly float _orthographicSize;
        //카메라에서 게임 평면(z = 0)까지의 거리
        private readonly float _distance;
        private readonly float _fieldOfView;
        #endregion

        #region Init
        private GameCameraInfo(Camera camera)
        {
            IsValid = true;
            Position = camera.transform.position;
            _isOrthographic = camera.orthographic;
            _orthographicSize = camera.orthographicSize;
            _distance = Mathf.Abs(camera.transform.position.z);
            _fieldOfView = camera.fieldOfView;
        }
        #endregion

        #region Public
        /// <summary>
        /// Main 씬의 카메라를 읽는다. 씬을 여는 것이 아니라 저장되지 않는 임시 씬으로 잠깐 불러서 읽고 닫으므로, 열려 있는 씬은 그대로다.
        /// 카메라가 없으면 에러를 남기고 IsValid가 false인 값을 돌려준다
        /// </summary>
        public static GameCameraInfo Load()
        {
            string path = string.Format(ScenePathFormat, SceneType.Main);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (camera.CompareTag(CameraTag)) return new GameCameraInfo(camera);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            Debug.LogError($"{path} has no camera tagged {CameraTag}. The camera range is not shown.");
            return default;
        }

        /// <summary>배율 zoom일 때 게임 평면에서 보이는 영역의 절반 크기. 게임과 같은 식으로 계산한다</summary>
        public Vector2 GetHalfView(float zoom)
            => CameraDirector.GetHalfView(_isOrthographic, _orthographicSize, _distance, _fieldOfView, Aspect, zoom);
        #endregion
    }
}
