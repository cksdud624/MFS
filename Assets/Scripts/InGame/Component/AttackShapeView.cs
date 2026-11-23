using System.Collections.Generic;
using Common;
using Common.Template.Interface;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 공격의 판정을 기본 도형(네모, 세모, 원)으로 보여준다. 공격 이펙트의 그림이 없는 동안 쓰는 임시 표시다.
    /// - 타격 : 판정이 켜져 있는 동안 그 자리에 그 크기로 띄운다. 판정이 꺼지면 잠깐 남았다가 사라진다.
    /// - 예고 : 곧 나갈 공격의 판정이 생길 자리를 예고 색으로 옅게 띄운다 (ObjectContext.SetAttackPreview).
    /// 어떤 도형으로 보일지는 AttackHitBox 표의 Shape 칸이 정하고, 비워둔 판정은 띄우지 않는다.
    /// 도형은 본체의 자식이라 본체를 따라 움직인다. 이펙트가 들고 다니는 판정은 그 이펙트가 보여주므로 여기서는 다루지 않는다
    /// </summary>
    public class AttackShapeView : MonoBehaviour, IUpdateable
    {
        #region Types
        //판정 하나의 타격 도형. 판정이 꺼진 뒤 사라지는 동안의 남은 시간을 같이 든다
        private class StrikeShape
        {
            public SpriteRenderer Renderer;
            //판정이 켜져 있는지. 꺼진 판정을 또 끄라는 신호에 다시 사라지기 시작하지 않게 한다
            public bool IsActive;
            //다 사라질 때까지 남은 시간(초)
            public float FadeTimer;
        }
        #endregion

        #region Fields
        private ObjectContext _objectContext;

        //판정마다 만들어둔 타격 도형. 한 번 만들면 켜고 끄면서 계속 다시 쓴다
        private readonly Dictionary<AttackHitBoxData, StrikeShape> _strikes = new();
        //사라지는 중인 타격 도형. 다 사라지면 뺀다
        private readonly List<StrikeShape> _fading = new();
        //예고 도형. 예고하는 공격의 판정 수만큼 앞에서부터 쓰고 나머지는 꺼둔다
        private readonly List<SpriteRenderer> _previews = new();
        //지금 예고하는 공격. 없으면 null. 돌아서거나 예고 색이 바뀌면 다시 그려야 해서 들고 있는다
        private IReadOnlyList<AttackCommandData> _previewCommands;
        #endregion

        #region Init
        public void Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;

            _objectContext.OnAttackHitBoxActiveChanged += HandleHitBoxActiveChanged;
            _objectContext.OnAttackPreviewChanged += HandleAttackPreviewChanged;
            _objectContext.OnDirectionChanged += HandleDirectionChanged;
            _objectContext.OnTelegraphChanged += HandleTelegraphChanged;
            Global.Instance.BindUpdate(this);
        }
        #endregion

        #region LifeCycle
        //판정이 꺼진 타격 도형을 서서히 지운다. 그리는 것이라 물리 스텝이 아니라 프레임마다 한다
        public void OnUpdate()
        {
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                var strike = _fading[i];
                strike.FadeTimer -= Time.deltaTime;
                if (strike.FadeTimer <= 0f)
                {
                    strike.Renderer.enabled = false;
                    _fading.RemoveAt(i);
                    continue;
                }

                var color = AttackShapeColor;
                color.a *= strike.FadeTimer / AttackShapeFadeDuration;
                strike.Renderer.color = color;
            }
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindUpdate(this);
            if (_objectContext == null) return;

            _objectContext.OnAttackHitBoxActiveChanged -= HandleHitBoxActiveChanged;
            _objectContext.OnAttackPreviewChanged -= HandleAttackPreviewChanged;
            _objectContext.OnDirectionChanged -= HandleDirectionChanged;
            _objectContext.OnTelegraphChanged -= HandleTelegraphChanged;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 판정이 켜지면 그 자리에 도형을 띄우고, 꺼지면 사라지게 한다.
        /// 판정은 아주 짧게 켜지는 것이 많아서(0.1초 남짓) 켜져 있는 동안만 보이면 어디를 쳤는지 읽기 어렵다
        /// </summary>
        private void HandleHitBoxActiveChanged(AttackHitBoxData hitBox, AttackType attackType, bool active, bool ownedByEffect)
        {
            if (hitBox == null || ownedByEffect) return;

            var shape = Global.Instance.TableManager.AttackHitBoxRecord.GetShape(hitBox);
            if (shape == AttackShape.None) return;

            var strike = GetStrike(hitBox, shape);
            if (active)
            {
                //공격 중에는 방향이 잠기므로 켜는 시점에 한 번만 자리를 잡으면 된다
                Place(strike.Renderer, hitBox.HitBoxOffset, hitBox.HitBoxSize);
                strike.Renderer.color = AttackShapeColor;
                strike.Renderer.enabled = true;
                strike.IsActive = true;
                _fading.Remove(strike);
                return;
            }

            if (!strike.IsActive) return;
            strike.IsActive = false;
            strike.FadeTimer = AttackShapeFadeDuration;
            _fading.Add(strike);
        }

        private void HandleAttackPreviewChanged(IReadOnlyList<AttackCommandData> attackCommands)
        {
            _previewCommands = attackCommands;
            RefreshPreview();
        }

        //예고하는 동안에는 대상을 따라 돌아설 수 있다. 판정은 보는 쪽에 생기므로 예고도 다시 그린다
        private void HandleDirectionChanged(Direction direction) => RefreshPreview();

        //예고의 색은 예고 종류를 따른다. 위장하던 카운터 자세가 진짜 색을 보일 때처럼 도중에 바뀔 수 있다
        private void HandleTelegraphChanged(AttackTelegraph telegraph) => RefreshPreview();
        #endregion

        #region Private
        /// <summary>
        /// 예고하는 공격의 판정이 생길 자리를 다시 그린다. 예고가 없으면 모두 끈다.
        /// 전진하는 공격은 나아간 뒤에 판정이 생기므로 그만큼 앞에 그린다.
        /// 전진하는 내내 판정이 켜져 있는 공격(돌진)은 판정이 지나가는 길 전체를 그린다
        /// </summary>
        private void RefreshPreview()
        {
            int count = 0;
            if (_previewCommands != null)
            {
                var record = Global.Instance.TableManager.AttackHitBoxRecord;
                var color = GetTelegraphColor(_objectContext.Telegraph);
                color.a = AttackShapePreviewAlpha;
                FindAdvance(_previewCommands, out float advanceDistance, out bool isSweep);

                for (int i = 0; i < _previewCommands.Count; i++)
                {
                    foreach (long hitBoxId in _previewCommands[i].AttackHitBox)
                    {
                        var hitBox = record.GetRecord(hitBoxId);
                        if (hitBox == null || hitBox.Duration <= 0f) continue;
                        var shape = record.GetShape(hitBox);
                        if (shape == AttackShape.None) continue;

                        var offset = hitBox.HitBoxOffset;
                        var size = hitBox.HitBoxSize;
                        if (isSweep)
                        {
                            offset.x += advanceDistance * 0.5f;
                            size.x += Mathf.Abs(advanceDistance);
                        }
                        else
                        {
                            offset.x += advanceDistance;
                        }

                        var preview = GetPreview(count++);
                        preview.sprite = AttackShapeSprite.Get(shape);
                        Place(preview, offset, size);
                        preview.color = color;
                        preview.enabled = true;
                    }
                }
            }

            for (int i = count; i < _previews.Count; i++)
                _previews[i].enabled = false;
        }

        /// <summary>
        /// 그 공격이 얼마나 전진하는지 (오른쪽을 보는 기준). 전진은 거리를 적어둔 첫 줄의 것을 쓴다 (ActionState와 같다).
        /// isSweep은 시간을 들여 전진하는 공격인지다. 시간이 없으면 한 번에 나아간 뒤에 친다
        /// </summary>
        private static void FindAdvance(IReadOnlyList<AttackCommandData> attackCommands, out float distance, out bool isSweep)
        {
            distance = 0f;
            isSweep = false;
            for (int i = 0; i < attackCommands.Count; i++)
            {
                if (attackCommands[i].AdvanceDistance == 0f) continue;

                distance = attackCommands[i].AdvanceDistance;
                isSweep = attackCommands[i].AdvanceTime > 0f;
                return;
            }
        }

        //판정의 타격 도형. 처음 켜지는 판정이면 만들어둔다
        private StrikeShape GetStrike(AttackHitBoxData hitBox, AttackShape shape)
        {
            if (_strikes.TryGetValue(hitBox, out var strike)) return strike;

            strike = new StrikeShape { Renderer = CreateRenderer($"AttackShape_{hitBox.Id}", SortingOrderAttackShape + 1) };
            strike.Renderer.sprite = AttackShapeSprite.Get(shape);
            _strikes[hitBox] = strike;
            return strike;
        }

        //index번째 예고 도형. 모자라면 만들어둔다
        private SpriteRenderer GetPreview(int index)
        {
            while (_previews.Count <= index)
                _previews.Add(CreateRenderer($"AttackPreview_{_previews.Count}", SortingOrderAttackShape));
            return _previews[index];
        }

        private SpriteRenderer CreateRenderer(string objectName, int sortingOrder)
        {
            var shapeObject = new GameObject(objectName);
            shapeObject.transform.SetParent(transform, false);

            var shapeRenderer = shapeObject.AddComponent<SpriteRenderer>();
            shapeRenderer.sortingOrder = sortingOrder;
            shapeRenderer.enabled = false;
            return shapeRenderer;
        }

        /// <summary>
        /// 도형을 판정과 같은 자리, 같은 크기로 둔다. 판정과 같이 본체 콜라이더의 중점을 기준으로 하고,
        /// offset은 오른쪽을 보는 기준이라 보는 방향에 맞춰 뒤집는다. 세모처럼 앞뒤가 있는 도형은 그림도 같이 뒤집는다
        /// </summary>
        private void Place(SpriteRenderer shapeRenderer, Vector2 offset, Vector2 size)
        {
            shapeRenderer.transform.localPosition = _objectContext.ColliderOffset + _objectContext.GetDirectionalOffset(offset);
            shapeRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            shapeRenderer.flipX = _objectContext.Direction == Direction.Left;
        }
        #endregion
    }
}
