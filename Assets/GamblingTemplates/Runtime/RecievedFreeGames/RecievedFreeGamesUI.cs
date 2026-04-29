using Attributes.Source.Infrastructure.Inspector;
using Spine;
using Spine.Unity;
using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Modules.Road
{
    public class RecievedFreeGamesUI : MonoBehaviour
    {
        public event Action Hidden;

        [SerializeField] private SkeletonGraphic _skeletonGraphic;

        [SerializeField] private TMP_Text _youHaveWonLabel;
        [SerializeField] private TMP_Text _freeGamesAmountLabel;
        [SerializeField] private TMP_Text _freeGamesLabel;
        [SerializeField] private TMP_Text _pressAnywhereToContinueLabel;
        
        [SerializeField, Range(0f, 1f)] private float _fadeOutPressAnywhereMinAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _pressAnywhereMinScale = 0.9f;
        [SerializeField, Min(0.01f)] private float _pressAnywherePulseHalfDuration = 1f;
        
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0f)] private float _canvasFadeInDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeInDelay = 0f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDelay = 0f;
        
        [SerializeField, Min(0f)] private float _labelFadeDuration = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _descentTrigger = 0.5f;
        [SerializeField, Min(0f)] private float _youHaveWonLabelFadeDelay = 0f;
        [SerializeField, Min(0f)] private float _freeGamesAmountLabelFadeDelay = 0f;
        [SerializeField, Min(0f)] private float _freeGamesLabelFadeDelay = 0f;
        
        [SerializeField, SpineAnimation] private string _startAnim;
        [SerializeField, SpineAnimation] private string _idleAnim;
        [SerializeField, SpineAnimation] private string _endAnim;

        [SerializeField, WebBridgeSound] private string _bonusPurchaseSound;

        private Coroutine _showRoutine;
        private Coroutine _youHaveWonFadeRoutine;
        private Coroutine _freeGamesAmountFadeRoutine;
        private Coroutine _freeGamesFadeRoutine;
        private Coroutine _canvasFadeRoutine;
        private PressAnywhereAnimation _pressAnywhereAnimation;
        private bool _isWaitingForCloseClick;
        private bool _isClosing;
        private int _currentFreeGamesAmount = 3;

        [Button]
        public void TestShow()
        {
            Show(3f, 3);
        }
        
        public void Show(float duration)
        {
            int resolved = ResolveFreeGamesAmount();
            Show(duration, resolved);
        }

        private int ResolveFreeGamesAmount()
        {
            if (GameWebBridge.Instance == null)
                return _currentFreeGamesAmount;

            int[] bonusPositions = GameWebBridge.Instance.ResolveBonusPositionsForAutoPlay();
            if (bonusPositions != null && bonusPositions.Length > 0)
                return bonusPositions.Length;

            return _currentFreeGamesAmount;
        }

        public void Show(float duration, int freeGamesAmount)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            AudioWebBridge.Instance.PlaySound(_bonusPurchaseSound);

            LayoutWebBridge.Instance.SetHideDesktopBetBar(true);
            LayoutWebBridge.Instance.SetHideMobileBetBar(true);
            LayoutWebBridge.Instance.SetHideMobileLastWin(true);
            LayoutWebBridge.Instance.SetHideSettingsMenuButton(true);

            _currentFreeGamesAmount = Mathf.Max(0, freeGamesAmount);
            ApplyFreeGamesAmountLabel();
            
            _ = duration;
            StopShowRoutine();
            StopFadeRoutines();
            EnsurePressAnywhereAnimation();
            _isWaitingForCloseClick = false;
            _isClosing = false;
            _showRoutine = StartCoroutine(ShowRoutine(duration));
        }

        private void ApplyFreeGamesAmountLabel()
        {
            if (_freeGamesAmountLabel == null)
                return;

            _freeGamesAmountLabel.text = _currentFreeGamesAmount.ToString();
        }

        private IEnumerator ShowRoutine(float duration)
        {
            _ = duration;
            SetCanvasAlpha(0f);
            SetLabelAlpha(_youHaveWonLabel, 0f);
            SetLabelAlpha(_freeGamesAmountLabel, 0f);
            SetLabelAlpha(_freeGamesLabel, 0f);
            _pressAnywhereAnimation?.PrepareForShow();
            PrepareTotemForShow();
            StartCanvasFade(0f, 1f, _canvasFadeInDuration, _canvasFadeInDelay);

            if (_youHaveWonLabelFadeDelay > 0f)
                yield return new WaitForSeconds(_youHaveWonLabelFadeDelay);
            StartLabelFade(_youHaveWonLabel, ref _youHaveWonFadeRoutine);

            yield return PlayTotemUntilDescent();

            StartLabelFade(_freeGamesAmountLabel, ref _freeGamesAmountFadeRoutine);

            if (_freeGamesLabelFadeDelay > 0f)
                yield return new WaitForSeconds(_freeGamesLabelFadeDelay);
            StartLabelFade(_freeGamesLabel, ref _freeGamesFadeRoutine);
            StartPressAnywherePulse();
            _isWaitingForCloseClick = true;

            _showRoutine = null;
        }

        private void Update()
        {
            if (!_isWaitingForCloseClick || _isClosing)
                return;

            if (GetCanvasAlpha() <= 0.001f)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            CloseByClick();
        }

        private void PrepareTotemForShow()
        {
            if (_skeletonGraphic == null || _skeletonGraphic.AnimationState == null || string.IsNullOrEmpty(_startAnim))
                return;

            TrackEntry entry = _skeletonGraphic.AnimationState.SetAnimation(0, _startAnim, false);
            if (entry == null)
                return;

            entry.MixDuration = 0f;
            entry.TrackTime = 0f;
            entry.TimeScale = 0f;
        }

        private void StartCanvasFade(float fromAlpha, float toAlpha, float duration, float delay = 0f, Action onCompleted = null)
        {
            if (_canvasFadeRoutine != null)
                StopCoroutine(_canvasFadeRoutine);
            _canvasFadeRoutine = StartCoroutine(FadeCanvas(fromAlpha, toAlpha, duration, delay, onCompleted));
        }

        private IEnumerator FadeCanvas(float fromAlpha, float toAlpha, float duration, float delay,  Action onCompleted = null)
        {
            if (_canvasGroup == null)
            {
                _canvasFadeRoutine = null;
                onCompleted?.Invoke();
                NotifyHiddenIfNeeded(toAlpha);
                yield break;
            }

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (duration <= 0f)
            {
                SetCanvasAlpha(toAlpha);
                _canvasFadeRoutine = null;
                onCompleted?.Invoke();
                NotifyHiddenIfNeeded(toAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetCanvasAlpha(Mathf.LerpUnclamped(fromAlpha, toAlpha, t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetCanvasAlpha(toAlpha);
            _canvasFadeRoutine = null;
            onCompleted?.Invoke();
            NotifyHiddenIfNeeded(toAlpha);
        }

        private void NotifyHiddenIfNeeded(float alpha)
        {
            if (alpha <= 0.001f)
                Hidden?.Invoke();
        }

        private void CloseByClick()
        {
            _isClosing = true;
            _isWaitingForCloseClick = false;
            StopPressAnywherePulse();
            PlayTotemEnd();
            StartCanvasFade(GetCanvasAlpha(), 0f, _canvasFadeOutDuration, _canvasFadeOutDelay, onCompleted: () =>
            {
                LayoutWebBridge.Instance.SetHideDesktopBetBar(false);
                LayoutWebBridge.Instance.SetHideMobileBetBar(false);
                LayoutWebBridge.Instance.SetHideMobileLastWin(false);
                LayoutWebBridge.Instance.SetHideSettingsMenuButton(false);
                LayoutWebBridge.Instance.SetHideLogo(false);

                if (gameObject.activeSelf)
                    gameObject.SetActive(false);
            });
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (_canvasGroup == null)
                return;

            float value = Mathf.Clamp01(alpha);
            _canvasGroup.alpha = value;

            bool isVisible = value > 0.001f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;
        }

        private float GetCanvasAlpha()
        {
            return _canvasGroup == null ? 0f : _canvasGroup.alpha;
        }

        private void StartLabelFade(TMP_Text label, ref Coroutine fadeRoutine)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeLabel(label, 0f, 1f, _labelFadeDuration));
        }

        private void StartPressAnywherePulse()
        {
            EnsurePressAnywhereAnimation();
            _pressAnywhereAnimation?.StartPulse();
        }

        private void StopPressAnywherePulse()
        {
            _pressAnywhereAnimation?.StopPulse();
        }

        private IEnumerator FadeLabel(TMP_Text label, float fromAlpha, float toAlpha, float duration)
        {
            if (label == null)
                yield break;

            if (duration <= 0f)
            {
                SetLabelAlpha(label, toAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetLabelAlpha(label, Mathf.LerpUnclamped(fromAlpha, toAlpha, t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetLabelAlpha(label, toAlpha);
        }

        private static void SetLabelAlpha(TMP_Text label, float alpha)
        {
            if (label == null)
                return;

            var color = label.color;
            color.a = Mathf.Clamp01(alpha);
            label.color = color;
        }

        private void EnsurePressAnywhereAnimation()
        {
            if (_pressAnywhereAnimation != null)
                return;

            _pressAnywhereAnimation = new PressAnywhereAnimation(
                this,
                _pressAnywhereToContinueLabel,
                _fadeOutPressAnywhereMinAlpha,
                _pressAnywhereMinScale,
                _pressAnywherePulseHalfDuration);
        }

        private void StopShowRoutine()
        {
            if (_showRoutine == null)
                return;

            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        private void StopFadeRoutines()
        {
            if (_youHaveWonFadeRoutine != null)
            {
                StopCoroutine(_youHaveWonFadeRoutine);
                _youHaveWonFadeRoutine = null;
            }

            if (_freeGamesAmountFadeRoutine != null)
            {
                StopCoroutine(_freeGamesAmountFadeRoutine);
                _freeGamesAmountFadeRoutine = null;
            }

            if (_freeGamesFadeRoutine != null)
            {
                StopCoroutine(_freeGamesFadeRoutine);
                _freeGamesFadeRoutine = null;
            }

            StopPressAnywherePulse();

            if (_canvasFadeRoutine != null)
            {
                StopCoroutine(_canvasFadeRoutine);
                _canvasFadeRoutine = null;
            }
        }

        private IEnumerator PlayTotemUntilDescent()
        {
            if (_skeletonGraphic == null || _skeletonGraphic.AnimationState == null || string.IsNullOrEmpty(_startAnim))
                yield break;

            TrackEntry startEntry = _skeletonGraphic.AnimationState.SetAnimation(0, _startAnim, false);
            if (startEntry != null)
                startEntry.MixDuration = 0f;

            if (!string.IsNullOrEmpty(_idleAnim))
            {
                TrackEntry idleEntry = _skeletonGraphic.AnimationState.AddAnimation(0, _idleAnim, true, 0f);
                if (idleEntry != null)
                    idleEntry.MixDuration = 1f;
            }

            if (startEntry == null)
                yield break;

            float animDuration = startEntry.Animation != null ? startEntry.Animation.Duration : 0f;
            float triggerTime = animDuration * _descentTrigger;
            float elapsed = 0f;

            while (elapsed < triggerTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void PlayTotemEnd()
        {
            if (_skeletonGraphic == null || _skeletonGraphic.AnimationState == null || string.IsNullOrEmpty(_endAnim))
                return;

            TrackEntry endEntry = _skeletonGraphic.AnimationState.SetAnimation(0, _endAnim, false);
            if (endEntry != null)
                endEntry.MixDuration = 1f;
        }
    }
}
