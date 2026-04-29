using Attributes.Source.Infrastructure.Inspector;
using Modules.Road;
using Modules.Road.UI;
using System;
using System.Collections;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Modules.Pepe
{
    public class MegaWinScreen : MonoBehaviour
    {
        public event Action Hidden;

        [SerializeField, SpineAnimation] private string _inAnimationName;
        [SerializeField, SpineAnimation] private string _midAnimationName;
        [SerializeField] private bool _midAnimationLoop;
        [SerializeField, SpineAnimation] private string _idleAnimationName;
        [SerializeField] private bool _idleAnimationLoop = true;
        [SerializeField, SpineAnimation] private string _outAnimationName;
        [SerializeField] private TMP_Text _winValue;
        [SerializeField] private TMP_Text _coefficientLabel;
        [SerializeField] private SkeletonGraphic _skeletonGraphic;
        [SerializeField] private MegaWinCoinsBurstEffect _coinsBurstEffect;

        [SerializeField] private TMP_Text _pressAnywhereToContinueLabel;
        [SerializeField, Range(0f, 1f)] private float _fadeOutPressAnywhereMinAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _pressAnywhereMinScale = 0.9f;
        [SerializeField, Min(0.01f)] private float _pressAnywherePulseHalfDuration = 1f;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0f)] private float _canvasFadeInDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeInDelay = 0f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDuration = 0.3f;
        [SerializeField, Min(0f)] private float _canvasFadeOutDelay = 0f;
        [SerializeField, Range(0f, 1f)] private float _canvasFadeOutStartProgress = 0.5f;

        [SerializeField, Min(0f)] private float _valueAnimDuration = 0.6f;
        [SerializeField] private float _valueAnimStartOffset = -0.3f;

        [Header("Coefficient Merge Animation")]
        [SerializeField, Min(0f)] private float _betDisplayDuration = 0.8f;
        [SerializeField, Min(0f)] private float _coefficientMergeDelay = 0.3f;
        [SerializeField, Min(1f)] private float _coefficientBounceScale = 2f;
        [SerializeField, Min(0f)] private float _coefficientBounceDuration = 0.4f;
        [SerializeField, Min(0f)] private float _coefficientFlyDuration = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _coefficientFlyEndScale = 0.3f;
        [SerializeField, Min(0f)] private float _coefficientReappearDuration = 0.3f;
        [SerializeField, Min(0f)] private float _coefficientReappearDelay = 0.1f;


        [SerializeField, Min(1f)] private float _coinDropGravityMultiplier = 4f;
        [SerializeField, WebBridgeSound] private string _bonusWinSound;

        private Coroutine _valueStartRoutine;
        private Coroutine _valueRoutine;
        private Coroutine _mergeRoutine;
        private Coroutine _hideRoutine;
        private Coroutine _canvasFadeRoutine;
        private TrackEntry _openTrackEntry;
        private TrackEntry _outTrackEntry;
        private float _targetValue;
        private string _valuePrefix = "";
        private string _valueSuffix = "$";
        private int _valueDecimals;
        private bool _canAnimateValue;
        private bool _outAnimationFinished;
        private bool _isWaitingForCloseClick;
        private bool _isClosing;
        private bool _isUiHiddenByMegaWinScreen;
        private PressAnywhereAnimation _pressAnywhereAnimation;

        private float _betAmount;
        private float _coefficient;
        private bool _hasCoefficientData;
        private Vector3 _coefficientLabelBasePosition;
        private Vector3 _coefficientLabelBaseScale;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_coefficientLabel != null)
            {
                _coefficientLabelBasePosition = _coefficientLabel.transform.localPosition;
                _coefficientLabelBaseScale = _coefficientLabel.transform.localScale;
            }

            ResolvePressAnywhereLabel();
        }

        private void OnDisable()
        {
            StopValueAnimation();
            StopValueStartRoutine();
            StopMergeAnimation();
            StopHideRoutine();
            StopCanvasFadeRoutine();
            StopPressAnywherePulse();
            _coinsBurstEffect.StopAndReset();
            UnsubscribeOpenTrackEntry();
            UnsubscribeOutTrackEntry();
            _isWaitingForCloseClick = false;
            _isClosing = false;
            SetCanvasAlpha(0f);
            ResetCoefficientLabel();
            RestoreGameUiIfNeeded();
        }

        [Button]
        public void Show()
        {
            HideGameUi();

            StopHideRoutine();
            StopCanvasFadeRoutine();
            StopPressAnywherePulse();
            UnsubscribeOutTrackEntry();
            gameObject.SetActive(true);
            EnsurePressAnywhereAnimation();
            _isWaitingForCloseClick = false;
            _isClosing = false;
            SetCanvasAlpha(0f);
            _pressAnywhereAnimation?.PrepareForShow();
            StartCanvasFade(0f, 1f, _canvasFadeInDuration, _canvasFadeInDelay);
            _coinsBurstEffect.Play();

            TrackEntry openAnim = null;
            if (_skeletonGraphic != null && _skeletonGraphic.AnimationState != null)
            {
                if (!string.IsNullOrEmpty(_inAnimationName))
                    openAnim = _skeletonGraphic.AnimationState.SetAnimation(0, _inAnimationName, false);

                TrackEntry queuedTrack = openAnim;

                if (!string.IsNullOrEmpty(_midAnimationName))
                {
                    if (queuedTrack != null)
                        queuedTrack = _skeletonGraphic.AnimationState.AddAnimation(0, _midAnimationName, _midAnimationLoop, 0f);
                    else
                        queuedTrack = _skeletonGraphic.AnimationState.SetAnimation(0, _midAnimationName, _midAnimationLoop);
                }

                if (!string.IsNullOrEmpty(_idleAnimationName))
                {
                    if (queuedTrack != null)
                        _skeletonGraphic.AnimationState.AddAnimation(0, _idleAnimationName, _idleAnimationLoop, 0f);
                    else
                        _skeletonGraphic.AnimationState.SetAnimation(0, _idleAnimationName, _idleAnimationLoop);
                }
            }

            StartValueAnimationAfterOpen(openAnim);
            StartPressAnywherePulse();
            _isWaitingForCloseClick = true;
        }

        public void SetValue(float amount)
        {
            _targetValue = Mathf.Max(0f, amount);
            _valuePrefix = "";
            _valueSuffix = "$";
            _valueDecimals = AmountTextUtility.GetDecimalPlaces(_targetValue);
            _canAnimateValue = true;
            _hasCoefficientData = false;
            SetValueText(_targetValue);

            if (isActiveAndEnabled)
                StartValueAnimationAfterOpen(_openTrackEntry);
        }

        public void SetValue(string amount)
        {
            _winValue.text = amount;
            _hasCoefficientData = false;

            if (AmountTextUtility.TryParseAmount(amount, out float value, out string prefix, out string suffix, out int decimals))
            {
                _targetValue = value;
                _valuePrefix = prefix;
                _valueSuffix = suffix;
                _valueDecimals = decimals;
                _canAnimateValue = true;
            }
            else
            {
                _canAnimateValue = false;
            }

            if (isActiveAndEnabled)
                StartValueAnimationAfterOpen(_openTrackEntry);
        }

        public void SetValue(float betAmount, float coefficient, float totalWin, string currency)
        {
            _betAmount = Mathf.Max(0f, betAmount);
            _coefficient = Mathf.Max(0f, coefficient);
            _targetValue = Mathf.Max(0f, totalWin);
            _valuePrefix = currency ?? "";
            _valueSuffix = "";
            _valueDecimals = AmountTextUtility.GetDecimalPlaces(_targetValue);
            _canAnimateValue = true;
            _hasCoefficientData = _coefficientLabel != null && _coefficient > 0f;

            int betDecimals = AmountTextUtility.GetDecimalPlaces(_betAmount);
            _winValue.text = AmountTextUtility.FormatAmount(_betAmount, _valuePrefix, _valueSuffix, betDecimals);

            if (_hasCoefficientData)
            {
                _coefficientLabel.text = $"{_coefficient:0.##}x";
            }

            if (isActiveAndEnabled)
                StartValueAnimationAfterOpen(_openTrackEntry);
        }

        private void SetValueText(float value)
        {
            _winValue.text = AmountTextUtility.FormatAmount(value, _valuePrefix, _valueSuffix, _valueDecimals);
        }

        private void StartValueAnimationAfterOpen(TrackEntry openAnim)
        {
            AudioWebBridge.Instance.PlaySound(_bonusWinSound);

            StopValueAnimation();
            StopValueStartRoutine();
            StopMergeAnimation();

            if (!_canAnimateValue)
                return;

            if (!_hasCoefficientData)
                SetValueText(0f);

            if (openAnim == null || openAnim.Animation == null || openAnim.Animation.Duration <= 0f)
            {
                StartFullAnimationSequence();
                return;
            }

            UnsubscribeOpenTrackEntry();
            _openTrackEntry = openAnim;
            _openTrackEntry.Complete += OnOpenAnimationComplete;
            _openTrackEntry.End += OnOpenAnimationEnd;

            float timeScale = Mathf.Abs(openAnim.TimeScale) > 0.0001f ? Mathf.Abs(openAnim.TimeScale) : 1f;
            float openDurationRealtime = openAnim.Animation.Duration / timeScale;
            float delay = openDurationRealtime + _valueAnimStartOffset;
            if (delay <= 0f)
                StartFullAnimationSequence();
            else
                _valueStartRoutine = StartCoroutine(DelayStartValue(delay));
        }

        private void OnOpenAnimationComplete(TrackEntry trackEntry)
        {
            HandleOpenAnimationFinished(trackEntry);
        }

        private void OnOpenAnimationEnd(TrackEntry trackEntry)
        {
            HandleOpenAnimationFinished(trackEntry);
        }

        private void HandleOpenAnimationFinished(TrackEntry trackEntry)
        {
            trackEntry.Complete -= OnOpenAnimationComplete;
            trackEntry.End -= OnOpenAnimationEnd;
            if (_openTrackEntry == trackEntry)
                _openTrackEntry = null;
            StartFullAnimationSequence();
        }

        private IEnumerator DelayStartValue(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _valueStartRoutine = null;
            StartFullAnimationSequence();
        }

        private void StartFullAnimationSequence()
        {
            if (_hasCoefficientData)
            {
                if (_mergeRoutine != null)
                    return;

                _mergeRoutine = StartCoroutine(CoefficientMergeSequence());
            }
            else
            {
                StartValueAnimationIfNotStarted();
            }
        }

        private IEnumerator CoefficientMergeSequence()
        {
            CanvasGroup cg = _coefficientLabel != null ? _coefficientLabel.GetComponent<CanvasGroup>() : null;

            // Фаза 1: показываем ставку + коэффициент
            int betDecimals = AmountTextUtility.GetDecimalPlaces(_betAmount);
            _winValue.text = AmountTextUtility.FormatAmount(_betAmount, _valuePrefix, _valueSuffix, betDecimals);

            if (_coefficientLabel != null)
            {
                _coefficientLabel.gameObject.SetActive(true);
                _coefficientLabel.text = $"{_coefficient:0.##}x";
                _coefficientLabel.transform.localPosition = _coefficientLabelBasePosition;
                _coefficientLabel.transform.localScale = _coefficientLabelBaseScale;

                if (cg != null)
                    cg.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(_betDisplayDuration);

            // Фаза 2: подпрыг (scale up) + полёт к Label_WinValue (scale down + fade out)
            if (_coefficientLabel != null)
            {
                yield return new WaitForSecondsRealtime(_coefficientMergeDelay);

                Vector3 baseScale = _coefficientLabelBaseScale;
                Vector3 bounceScale = baseScale * _coefficientBounceScale;

                // 2a: подпрыг — увеличение
                float elapsed = 0f;
                float bounceDur = Mathf.Max(0.01f, _coefficientBounceDuration);
                while (elapsed < bounceDur)
                {
                    float t = Mathf.Clamp01(elapsed / bounceDur);
                    float eased = t * t * (3f - 2f * t);
                    _coefficientLabel.transform.localScale = Vector3.LerpUnclamped(baseScale, bounceScale, eased);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                _coefficientLabel.transform.localScale = bounceScale;

                // 2b: полёт к Label_WinValue с уменьшением и fade out
                Vector3 startPos = _coefficientLabel.transform.localPosition;
                Vector3 targetPos = _winValue.transform.localPosition;
                Vector3 flyEndScale = baseScale * _coefficientFlyEndScale;

                elapsed = 0f;
                float flyDur = Mathf.Max(0.01f, _coefficientFlyDuration);
                while (elapsed < flyDur)
                {
                    float t = Mathf.Clamp01(elapsed / flyDur);
                    float eased = t * t * (3f - 2f * t);

                    _coefficientLabel.transform.localPosition = Vector3.LerpUnclamped(startPos, targetPos, eased);
                    _coefficientLabel.transform.localScale = Vector3.LerpUnclamped(bounceScale, flyEndScale, eased);

                    if (cg != null)
                        cg.alpha = Mathf.LerpUnclamped(1f, 0f, eased);

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                _coefficientLabel.gameObject.SetActive(false);
                _coefficientLabel.transform.localPosition = _coefficientLabelBasePosition;
                _coefficientLabel.transform.localScale = _coefficientLabelBaseScale;

                if (cg != null)
                    cg.alpha = 1f;
            }

            // Фаза 3: подсчёт от ставки до выигрыша
            _mergeRoutine = null;
            StartValueAnimationIfNotStarted();
        }

        private void StartValueAnimationIfNotStarted()
        {
            if (_valueRoutine != null)
                return;

            _valueRoutine = StartCoroutine(AnimateValue());
        }

        private IEnumerator AnimateValue()
        {
            float startValue = _hasCoefficientData ? _betAmount : 0f;

            if (_valueAnimDuration <= 0f)
            {
                SetValueText(_targetValue);
                _valueRoutine = null;
                yield return ReappearCoefficientLabel();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _valueAnimDuration)
            {
                float t = Mathf.Clamp01(elapsed / _valueAnimDuration);
                float value = Mathf.LerpUnclamped(startValue, _targetValue, t);
                SetValueText(value);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetValueText(_targetValue);
            _valueRoutine = null;

            // Фаза 4: коэффициент возвращается на место
            yield return ReappearCoefficientLabel();
        }

        private IEnumerator ReappearCoefficientLabel()
        {
            if (!_hasCoefficientData || _coefficientLabel == null)
                yield break;

            yield return new WaitForSecondsRealtime(_coefficientReappearDelay);

            _coefficientLabel.text = $"{_coefficient:0.##}x";
            _coefficientLabel.transform.localPosition = _coefficientLabelBasePosition;

            Vector3 startScale = _coefficientLabelBaseScale * 0.5f;
            Vector3 endScale = _coefficientLabelBaseScale;
            _coefficientLabel.transform.localScale = startScale;

            CanvasGroup cg = _coefficientLabel.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = 0f;

            _coefficientLabel.gameObject.SetActive(true);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, _coefficientReappearDuration);
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);

                _coefficientLabel.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

                if (cg != null)
                    cg.alpha = Mathf.LerpUnclamped(0f, 1f, eased);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _coefficientLabel.transform.localScale = endScale;

            if (cg != null)
                cg.alpha = 1f;
        }

        private void StopMergeAnimation()
        {
            if (_mergeRoutine == null)
                return;

            StopCoroutine(_mergeRoutine);
            _mergeRoutine = null;
            ResetCoefficientLabel();
        }

        private void ResetCoefficientLabel()
        {
            if (_coefficientLabel == null)
                return;

            _coefficientLabel.transform.localPosition = _coefficientLabelBasePosition;
            _coefficientLabel.transform.localScale = _coefficientLabelBaseScale;
            _coefficientLabel.gameObject.SetActive(false);

            CanvasGroup cg = _coefficientLabel.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = 1f;
        }

        private void StopValueAnimation()
        {
            if (_valueRoutine == null)
                return;
            StopCoroutine(_valueRoutine);
            _valueRoutine = null;
        }

        private void StopValueStartRoutine()
        {
            if (_valueStartRoutine == null)
                return;
            StopCoroutine(_valueStartRoutine);
            _valueStartRoutine = null;
        }

        private void UnsubscribeOpenTrackEntry()
        {
            if (_openTrackEntry == null)
                return;

            _openTrackEntry.Complete -= OnOpenAnimationComplete;
            _openTrackEntry.End -= OnOpenAnimationEnd;
            _openTrackEntry = null;
        }

        [Button]
        public void Hide()
        {
            StopValueAnimation();
            StopValueStartRoutine();
            StopMergeAnimation();
            UnsubscribeOpenTrackEntry();
            _coinsBurstEffect.StopWithGravityDrop(_coinDropGravityMultiplier);

            if (!gameObject.activeSelf)
            {
                _isWaitingForCloseClick = false;
                _isClosing = false;
                SetCanvasAlpha(0f);
                RestoreGameUiIfNeeded();
                return;
            }

            if (_isClosing)
                return;

            _isClosing = true;
            _isWaitingForCloseClick = false;
            StopPressAnywherePulse();
            StopHideRoutine();
            _hideRoutine = StartCoroutine(HideAfterOutAnimation());
        }

        public IEnumerator HideAndWait()
        {
            yield return WaitForValueAnimationCompletion();
            Hide();
            while (_hideRoutine != null || gameObject.activeSelf)
                yield return null;
        }

        private IEnumerator WaitForValueAnimationCompletion()
        {
            if (!gameObject.activeSelf)
                yield break;

            const float maxWaitSeconds = 10f;
            float elapsed = 0f;
            while ((_valueStartRoutine != null || _valueRoutine != null || _mergeRoutine != null) && elapsed < maxWaitSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator HideAfterOutAnimation()
        {
            if (_skeletonGraphic == null || _skeletonGraphic.AnimationState == null || string.IsNullOrEmpty(_outAnimationName))
            {
                StartHideCanvasFade(Mathf.Max(0f, _canvasFadeOutDelay));
                while (_canvasFadeRoutine != null)
                    yield return null;

                _hideRoutine = null;
                DeactivateAfterHide();
                yield break;
            }

            TrackEntry outAnim = _skeletonGraphic.AnimationState.SetAnimation(0, _outAnimationName, false);
            if (outAnim == null || outAnim.Animation == null)
            {
                StartHideCanvasFade(Mathf.Max(0f, _canvasFadeOutDelay));
                while (_canvasFadeRoutine != null)
                    yield return null;

                _hideRoutine = null;
                DeactivateAfterHide();
                yield break;
            }

            UnsubscribeOutTrackEntry();
            _outTrackEntry = outAnim;
            _outTrackEntry.Complete += OnOutAnimationComplete;
            _outTrackEntry.End += OnOutAnimationEnd;
            _outAnimationFinished = false;

            float timeScale = Mathf.Abs(outAnim.TimeScale) > 0.0001f ? Mathf.Abs(outAnim.TimeScale) : 1f;
            float outDurationRealtime = outAnim.Animation.Duration / timeScale;
            float fadeStartProgress = Mathf.Clamp01(_canvasFadeOutStartProgress);
            float fadeDelay = Mathf.Max(0f, _canvasFadeOutDelay) + outDurationRealtime * fadeStartProgress;
            StartHideCanvasFade(fadeDelay);

            float timeout = outDurationRealtime + 0.1f;
            float elapsed = 0f;
            while (!_outAnimationFinished && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            while (_canvasFadeRoutine != null)
                yield return null;

            UnsubscribeOutTrackEntry();
            _hideRoutine = null;
            DeactivateAfterHide();
        }

        private void StartHideCanvasFade(float delay)
        {
            StartCanvasFade(GetCanvasAlpha(), 0f, _canvasFadeOutDuration, delay);
        }

        private void OnOutAnimationComplete(TrackEntry trackEntry)
        {
            HandleOutAnimationFinished(trackEntry);
        }

        private void OnOutAnimationEnd(TrackEntry trackEntry)
        {
            HandleOutAnimationFinished(trackEntry);
        }

        private void HandleOutAnimationFinished(TrackEntry trackEntry)
        {
            if (_outTrackEntry != trackEntry)
                return;

            _outAnimationFinished = true;
        }

        private void UnsubscribeOutTrackEntry()
        {
            if (_outTrackEntry == null)
                return;

            _outTrackEntry.Complete -= OnOutAnimationComplete;
            _outTrackEntry.End -= OnOutAnimationEnd;
            _outTrackEntry = null;
        }

        private void StopHideRoutine()
        {
            if (_hideRoutine == null)
                return;

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        private void Update()
        {
            if (!_isWaitingForCloseClick || _isClosing)
                return;

            if (GetCanvasAlpha() <= 0.001f)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            Hide();
        }

        private void EnsurePressAnywhereAnimation()
        {
            ResolvePressAnywhereLabel();

            if (_pressAnywhereAnimation != null)
                return;

            _pressAnywhereAnimation = new PressAnywhereAnimation(
                this,
                _pressAnywhereToContinueLabel,
                _fadeOutPressAnywhereMinAlpha,
                _pressAnywhereMinScale,
                _pressAnywherePulseHalfDuration);
        }

        private void ResolvePressAnywhereLabel()
        {
            if (IsPressAnywhereLabelBoundToThisScreen(_pressAnywhereToContinueLabel))
                return;

            _pressAnywhereToContinueLabel = FindPressAnywhereLabel();
            _pressAnywhereAnimation = null;

            if (_pressAnywhereToContinueLabel == null)
                Debug.LogWarning("[MegaWinScreen] PressAnywhere label is missing or not under MegaWinScreen hierarchy.");
        }

        private bool IsPressAnywhereLabelBoundToThisScreen(TMP_Text label)
        {
            return label != null && label.transform != null && label.transform.IsChildOf(transform);
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

        private void StartCanvasFade(float fromAlpha, float toAlpha, float duration, float delay = 0f, Action onCompleted = null)
        {
            StopCanvasFadeRoutine();
            _canvasFadeRoutine = StartCoroutine(FadeCanvas(fromAlpha, toAlpha, duration, delay, onCompleted));
        }

        private IEnumerator FadeCanvas(float fromAlpha, float toAlpha, float duration, float delay, Action onCompleted = null)
        {
            if (_canvasGroup == null)
            {
                _canvasFadeRoutine = null;
                onCompleted?.Invoke();
                yield break;
            }

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (duration <= 0f)
            {
                SetCanvasAlpha(toAlpha);
                _canvasFadeRoutine = null;
                onCompleted?.Invoke();
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
        }

        private void StopCanvasFadeRoutine()
        {
            if (_canvasFadeRoutine == null)
                return;

            StopCoroutine(_canvasFadeRoutine);
            _canvasFadeRoutine = null;
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

        private TMP_Text FindPressAnywhereLabel()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == "Label_PressAnywhereToContinue")
                    return labels[i];
            }

            return null;
        }

        private void DeactivateAfterHide()
        {
            RestoreGameUiIfNeeded();
            Hidden?.Invoke();

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void HideGameUi()
        {
            LayoutWebBridge layout = LayoutWebBridge.Instance;

            if (layout == null)
                return;

            layout.SetHideDesktopBetBar(true);
            layout.SetHideMobileBetBar(true);
            layout.SetHideMobileLastWin(true);
            layout.SetHideSettingsMenuButton(true);
            _isUiHiddenByMegaWinScreen = true;
        }

        private void RestoreGameUiIfNeeded()
        {
            if (!_isUiHiddenByMegaWinScreen)
                return;

            LayoutWebBridge layout = LayoutWebBridge.Instance;
            if (layout != null)
            {
                layout.SetHideDesktopBetBar(false);
                layout.SetHideMobileBetBar(false);
                layout.SetHideMobileLastWin(false);
                layout.SetHideSettingsMenuButton(false);
            }

            _isUiHiddenByMegaWinScreen = false;
        }
    }
}
