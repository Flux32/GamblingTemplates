using System;
using System.Collections;
using Attributes.Source.Infrastructure.Inspector;
using Modules.Road;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Modules.GamblingTemplates.GamblingTemplates.Runtime.RecievedFreeGames
{
    public class RecievedFreeGamesUI : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField, WebBridgeSound] private string _bonusPurchaseSound;

        [Header("Animations")]
        [SerializeField, SpineAnimation] private string _startAnim;
        [SerializeField, SpineAnimation] private string _idleAnim;
        [SerializeField, SpineAnimation] private string _endAnim;

        [Header("Press Anywhere")]
        [SerializeField, Range(0f, 1f)] private float _fadeOutPressAnywhereMinAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _pressAnywhereMinScale = 0.9f;
        [SerializeField, Min(0.01f)] private float _pressAnywherePulseHalfDuration = 1f;

        [Header("Canvas Fade")]
        [SerializeField, Min(0f)] private float _canvasFadeInDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeInDelay = 0f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDelay = 0f;

        [Header("Label Fade")]
        [SerializeField, Min(0f)] private float _labelFadeDuration = 0.3f;
        [SerializeField, Min(0f)] private float _labelFadeDelay = 0f;

        [Header("Dependencies")]
        [SerializeField] private SkeletonGraphic _skeletonGraphic;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _freeGamesAmountLabel;
        [SerializeField] private TMP_Text _pressAnywhereToContinueLabel;

        public event Action Hidden;

        private PressAnywhereAnimation _pressAnywhereAnimation;
        private Coroutine _showRoutine;
        private Coroutine _labelFadeRoutine;
        private Coroutine _canvasFadeRoutine;
        private bool _isWaitingForCloseClick;
        private bool _isClosing;
        private int _currentFreeGamesAmount = 3;

        private void Awake()
        {
            _pressAnywhereAnimation = new PressAnywhereAnimation(
                this,
                _pressAnywhereToContinueLabel,
                _fadeOutPressAnywhereMinAlpha,
                _pressAnywhereMinScale,
                _pressAnywherePulseHalfDuration);
        }

        [Button]
        public void TestShow() => Show(3f, 3);

        public void Show(float duration) => Show(duration, ResolveFreeGamesAmount());

        public void Show(float duration, int freeGamesAmount)
        {
            CancelRoutines();

            gameObject.SetActive(true);
            AudioWebBridge.Instance.PlaySound(_bonusPurchaseSound);
            SetWebUiHidden(true);

            _currentFreeGamesAmount = Mathf.Max(0, freeGamesAmount);
            _freeGamesAmountLabel.text = _currentFreeGamesAmount.ToString();

            _isClosing = false;
            _isWaitingForCloseClick = false;
            _showRoutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            SetCanvasAlpha(0f);
            SetLabelAlpha(_freeGamesAmountLabel, 0f);
            _pressAnywhereAnimation.PrepareForShow();
            PlayStartThenIdle();
            StartCanvasFade(0f, 1f, _canvasFadeInDuration, _canvasFadeInDelay);

            if (_labelFadeDelay > 0f)
                yield return new WaitForSeconds(_labelFadeDelay);

            StartLabelFade(_freeGamesAmountLabel);
            _pressAnywhereAnimation.StartPulse();
            _isWaitingForCloseClick = true;
            _showRoutine = null;
        }

        private void Update()
        {
            if (!_isWaitingForCloseClick || _isClosing)
                return;
            if (_canvasGroup.alpha <= 0.001f)
                return;
            if (!Input.GetMouseButtonDown(0))
                return;

            CloseByClick();
        }

        private void CloseByClick()
        {
            _isClosing = true;
            _isWaitingForCloseClick = false;
            _pressAnywhereAnimation.StopPulse();
            PlayEnd();

            StartCanvasFade(_canvasGroup.alpha, 0f, _canvasFadeOutDuration, _canvasFadeOutDelay, onCompleted: () =>
            {
                SetWebUiHidden(false);
                LayoutWebBridge.Instance.SetHideLogo(false);
                gameObject.SetActive(false);
            });
        }

        private void PlayStartThenIdle()
        {
            AnimationState animationState = _skeletonGraphic.AnimationState;
            animationState.ClearTracks();

            TrackEntry start = animationState.SetAnimation(0, _startAnim, false);
            start.MixDuration = 0f;

            TrackEntry idle = animationState.AddAnimation(0, _idleAnim, true, 0f);
            idle.MixDuration = 0f;
        }

        private void PlayEnd()
        {
            TrackEntry end = _skeletonGraphic.AnimationState.SetAnimation(0, _endAnim, false);
            end.MixDuration = 0f;
        }

        private int ResolveFreeGamesAmount()
        {
            if (GameWebBridge.Instance == null)
                return _currentFreeGamesAmount;

            int[] bonusPositions = GameWebBridge.Instance.ResolveBonusPositionsForAutoPlay();
            return bonusPositions != null && bonusPositions.Length > 0
                ? bonusPositions.Length
                : _currentFreeGamesAmount;
        }

        private static void SetWebUiHidden(bool hidden)
        {
            LayoutWebBridge layout = LayoutWebBridge.Instance;
            layout.SetHideDesktopBetBar(hidden);
            layout.SetHideMobileBetBar(hidden);
            layout.SetHideMobileLastWin(hidden);
            layout.SetHideSettingsMenuButton(hidden);
        }

        private void StartCanvasFade(float from, float to, float duration, float delay, Action onCompleted = null)
        {
            if (_canvasFadeRoutine != null)
                StopCoroutine(_canvasFadeRoutine);
            _canvasFadeRoutine = StartCoroutine(FadeCanvas(from, to, duration, delay, onCompleted));
        }

        private IEnumerator FadeCanvas(float from, float to, float duration, float delay, Action onCompleted)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetCanvasAlpha(Mathf.LerpUnclamped(from, to, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetCanvasAlpha(to);
            _canvasFadeRoutine = null;
            onCompleted?.Invoke();

            if (to <= 0.001f)
                Hidden?.Invoke();
        }

        private void StartLabelFade(TMP_Text label)
        {
            if (_labelFadeRoutine != null)
                StopCoroutine(_labelFadeRoutine);
            _labelFadeRoutine = StartCoroutine(FadeLabel(label, 0f, 1f, _labelFadeDuration));
        }

        private IEnumerator FadeLabel(TMP_Text label, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetLabelAlpha(label, Mathf.LerpUnclamped(from, to, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetLabelAlpha(label, to);
            _labelFadeRoutine = null;
        }

        private void SetCanvasAlpha(float alpha)
        {
            float value = Mathf.Clamp01(alpha);
            _canvasGroup.alpha = value;
            bool visible = value > 0.001f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private static void SetLabelAlpha(TMP_Text label, float alpha)
        {
            Color color = label.color;
            color.a = Mathf.Clamp01(alpha);
            label.color = color;
        }

        private void CancelRoutines()
        {
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            if (_labelFadeRoutine != null) StopCoroutine(_labelFadeRoutine);
            if (_canvasFadeRoutine != null) StopCoroutine(_canvasFadeRoutine);
            _showRoutine = null;
            _labelFadeRoutine = null;
            _canvasFadeRoutine = null;
            _pressAnywhereAnimation?.StopPulse();
        }
    }
}
