using Common;
using Cysharp.Threading.Tasks;
using InGame.Context;
using System;
using System.Collections.Generic;
using System.Threading;
using Generated.Table;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using static Common.GameDefine;
using static Common.AssetKeys;

namespace InGame.Component
{
    public class AnimationPlayer : MonoBehaviour
    {
        #region Fields
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationClipPlayable _currentPlayable;
        //에셋 이름(1001/idle, 1001/attack1 …)으로 들고 있는다. 공격은 커맨드 단계마다 클립이 달라진다
        private readonly Dictionary<string, AnimationClip> _clips = new();
        private AnimationType _current;
        private CancellationTokenSource _animationEndCts;
        private ObjectContext _objectContext;
        private ObjectData _objectData;
        //공격 예고가 없을 때의 스프라이트 색 (Object 표의 Tint, 없으면 흰색). 예고가 끝나면 이 색으로 돌아간다
        private Color _baseColor = Color.white;
        #endregion

        #region Init
        public async UniTask Init(ObjectContext objectContext, ObjectData objectData)
        {
            _objectContext = objectContext;
            _objectData = objectData;

            if (!TryGetComponent(out _animator))
                _animator = gameObject.AddComponent<Animator>();
            _animator.applyRootMotion = false;

            if (!TryGetComponent(out _spriteRenderer))
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.flipX = _objectContext.Direction == Direction.Left;
            //같은 그림을 쓰는 오브젝트를 색으로 구분한다 (Object 표의 Tint, r,g,b 0~1). 빈칸(0,0,0)이면 원래 색 그대로 둔다
            if (objectData.Tint != Vector3.zero)
                _baseColor = new Color(objectData.Tint.x, objectData.Tint.y, objectData.Tint.z);
            _spriteRenderer.color = _baseColor;

            _graph = PlayableGraph.Create($"AnimGraph_{objectData.Id}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            await LoadClips(objectData);

            _graph.Play();
            Play(AnimationType.Idle);

            //그래프와 클립이 준비된 뒤에 구독한다. 먼저 구독하면 로드를 기다리는 동안 들어온 요청이 빈 그래프로 들어간다
            _objectContext.OnDirectionChanged += OnDirectionChanged;
            _objectContext.OnAnimationChanged += Play;
            _objectContext.OnTelegraphChanged += OnTelegraphChanged;
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            if (_objectContext != null)
            {
                _objectContext.OnDirectionChanged -= OnDirectionChanged;
                _objectContext.OnAnimationChanged -= Play;
                _objectContext.OnTelegraphChanged -= OnTelegraphChanged;
            }

            _animationEndCts?.Cancel();
            _animationEndCts?.Dispose();

            if (_graph.IsValid())
                _graph.Destroy();
        }
        #endregion

        #region Public
        /// <summary>Order in Layer</summary>
        public void SetSortingOrder(int sortingOrder) => _spriteRenderer.sortingOrder = sortingOrder;
        #endregion

        #region Event Handlers
        private void OnDirectionChanged(Direction direction)
        {
            _spriteRenderer.flipX = direction == Direction.Left;
        }

        /// <summary>
        /// 공격 예고를 색으로 알린다. 예고 모션이 따로 없어서, 예고하는 동안 스프라이트에 공격 종류의 색을 섞어 둔다.
        /// 원래 색을 조금 남겨서 같은 그림을 쓰는 오브젝트끼리의 구분(Tint)이 예고 중에도 보이게 한다
        /// </summary>
        private void OnTelegraphChanged(AttackTelegraph telegraph)
        {
            _spriteRenderer.color = telegraph == AttackTelegraph.None
                ? _baseColor
                : Color.Lerp(_baseColor, GetTelegraphColor(telegraph), TelegraphTintStrength);
        }

        public void Play(AnimationType animType, int variant = 0, Action notifyAnimationEnd = null)
        {
            string assetName = GetAssetName(animType, variant);
            if (!_clips.TryGetValue(assetName, out var clip))
            {
                //번호가 붙은 클립은 테이블이 쓰겠다고 지정한 것이므로 조용히 넘기지 않는다
                if (variant > 0)
                    Debug.LogError($"Animation {AssetPathAnimation}{assetName}{AssetExtensionAnimation} not found");
                //클립이 없으면 기다릴 것이 없으므로 바로 끝난 것으로 알린다.
                //알리지 않으면 끝나기를 기다리는 쪽이 멈춘다 (쓰러지는 모션이 없는 오브젝트가 사라지지 않는다)
                notifyAnimationEnd?.Invoke();
                return;
            }

            if (_currentPlayable.IsValid())
                _currentPlayable.Destroy();

            _animationEndCts?.Cancel();
            _animationEndCts?.Dispose();
            _animationEndCts = null;

            if (!clip.isLooping && notifyAnimationEnd != null)
            {
                _animationEndCts = new CancellationTokenSource();
                WaitForAnimationEnd(clip.length, notifyAnimationEnd, _animationEndCts.Token).Forget();
            }

            _current = animType;
            _currentPlayable = AnimationClipPlayable.Create(_graph, clip);
            _output.SetSourcePlayable(_currentPlayable);
        }
        #endregion

        #region Private
        /// <summary>이 오브젝트가 쓸 클립을 전부 미리 받아둔다</summary>
        private async UniTask LoadClips(ObjectData objectData)
        {
            var assetManager = Global.Instance.AssetManager;
            var loadTasks = new List<UniTask>();
            //애니메이션이 없는 오브젝트도 있으므로 번호 없는 기본 클립은 없어도 에러로 보지 않는다
            foreach (AnimationType animType in Enum.GetValues(typeof(AnimationType)))
                loadTasks.Add(LoadClip(assetManager, GetAssetName(animType, 0), logOnMissing: false));

            //공격은 커맨드 단계마다 다른 클립을 쓰므로 테이블에 적힌 번호만큼 더 받아둔다.
            //테이블이 번호를 지정했는데 클립이 없으면 만들다 만 것이므로 에러로 알린다
            var animations = Global.Instance.TableManager.AttackCommandRecord.GetAnimations(objectData);
            if (animations != null)
            {
                foreach (int variant in animations)
                    loadTasks.Add(LoadClip(assetManager, GetAssetName(AnimationType.Attack, variant), logOnMissing: true));
            }

            await UniTask.WhenAll(loadTasks);
        }

        private async UniTask LoadClip(AssetManager assetManager, string assetName, bool logOnMissing)
        {
            var clip = await assetManager.LoadAssetAsync<AnimationClip>(LoadTarget.Animation, assetName, logOnMissing);
            if (clip == null) return;
            _clips[assetName] = clip;
        }

        /// <summary>Attack1, Attack2처럼 번호가 붙은 클립의 에셋 이름. 번호가 0이면 번호 없는 기본 클립</summary>
        private string GetAssetName(AnimationType animType, int variant)
        {
            string name = animType.ToString().ToLower();
            return variant > 0 ? $"{_objectData.Id}/{name}{variant}" : $"{_objectData.Id}/{name}";
        }

        private async UniTaskVoid WaitForAnimationEnd(float duration, Action notifyAnimationEnd, CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
            notifyAnimationEnd?.Invoke();
        }
        #endregion
    }
}
