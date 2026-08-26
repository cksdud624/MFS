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

        public async UniTask Init(ObjectContext objectContext, ObjectData objectData)
        {
            _objectContext = objectContext;
            _objectData = objectData;
            _objectContext.OnDirectionChanged += OnDirectionChanged;
            _objectContext.OnAnimationChanged += Play;

            var animator = GetComponent<Animator>();
            _animator = animator != null ? animator : gameObject.AddComponent<Animator>();
            _animator.applyRootMotion = false;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer = spriteRenderer != null ? spriteRenderer : gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.flipX = _objectContext.Direction == Direction.Left;

            _graph = PlayableGraph.Create($"AnimGraph_{objectData.Id}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            var assetManager = Global.Instance.AssetManager;
            var loadTasks = new List<UniTask>();
            //애니메이션이 없는 오브젝트도 있으므로 번호 없는 기본 클립은 없어도 에러로 보지 않는다
            foreach (AnimationType animType in System.Enum.GetValues(typeof(AnimationType)))
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

            _graph.Play();
            Play(AnimationType.Idle);
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

        private void OnDirectionChanged(Direction direction)
        {
            _spriteRenderer.flipX = direction == Direction.Left;
        }

        /// <summary>Order in Layer</summary>
        public void SetSortingOrder(int sortingOrder) => _spriteRenderer.sortingOrder = sortingOrder;

        public void Play(AnimationType animType, int variant = 0, Action notifyAnimationEnd = null)
        {
            string assetName = GetAssetName(animType, variant);
            if (!_clips.TryGetValue(assetName, out var clip))
            {
                //번호가 붙은 클립은 테이블이 쓰겠다고 지정한 것이므로 조용히 넘기지 않는다
                if (variant > 0)
                    Debug.LogError($"Animation {AssetPathAnimation}{assetName}{AssetExtensionAnimation} not found");
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

        private async UniTaskVoid WaitForAnimationEnd(float duration, Action notifyAnimationEnd, CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
            notifyAnimationEnd?.Invoke();
        }

        private void OnDestroy()
        {
            if (_objectContext != null)
            {
                _objectContext.OnDirectionChanged -= OnDirectionChanged;
                _objectContext.OnAnimationChanged -= Play;
            }

            _animationEndCts?.Cancel();
            _animationEndCts?.Dispose();

            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
