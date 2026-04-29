using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Modules.Road.UI
{
    public class CashOutWindow : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _cashoutValue;
        [SerializeField, Min(0f)] private float _openDuration = 0.35f;
        [SerializeField, Min(0f)] private float _closeDuration = 0.25f;
        [SerializeField] private SkeletonGraphic _skeletonAnimation;
        [SerializeField, SpineAnimation] private string _openStartAnimationName;
        [SerializeField, SpineAnimation] private string _openIndleAnimationName;
        [SerializeField] private float _openTimeScale = 2f;
        [SerializeField] private float _idleTimeScale = 2f;
        [SerializeField, Min(0f)] private float _valueAnimDuration = 0.6f;
        // Negative values start the counting before the open animation finishes (overlap).
        [SerializeField] private float _valueAnimStartOffset = -0.3f;

        [SerializeField, WebBridgeSound] private string _cashoutSound;
        
        private Coroutine _scaleRoutine;
        private Coroutine _valueStartRoutine;
        private Coroutine _valueRoutine;
        private TrackEntry _openTrackEntry;
        private Coroutine _fadeRoutine;
        private float _targetValue;
        private string _valuePrefix = "";
        private string _valueSuffix = "$";
        private int _valueDecimals;
        private bool _canAnimateValue;

        private void Start()
        {
            SetCanvasAlpha(0f);
        }

        public void SetValue(float amount)
        {
            _targetValue = amount;
            _valuePrefix = "";
            _valueSuffix = "$";
            _valueDecimals = AmountTextUtility.GetDecimalPlaces(amount);
            _canAnimateValue = true;
            _cashoutValue.text = AmountTextUtility.FormatAmount(_targetValue, _valuePrefix, _valueSuffix, _valueDecimals);
        }

        public void SetValue(string amount)
        {
            _cashoutValue.text = amount;
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
        }

        private void SetCanvasAlpha(float value)
        {
            _canvasGroup.alpha = value;
            _canvasGroup.interactable = value > 0.001f;
            _canvasGroup.blocksRaycasts = value > 0.001f;
        }
        
        public void Open()
        {
            StopFade();
            SetCanvasAlpha(1f);

            TrackEntry openAnim = null;
            if (_skeletonAnimation != null)
            {
                openAnim = _skeletonAnimation
                    .AnimationState
                    .SetAnimation(0, _openStartAnimationName, false);

                if (openAnim != null)
                {
                    openAnim.MixDuration = 0f;
                    openAnim.TimeScale = _openTimeScale;
                }
                
                TrackEntry idleAnim = _skeletonAnimation
                    .AnimationState
                    .AddAnimation(0, _openIndleAnimationName, true, 0);

                if (idleAnim != null)
                {
                    idleAnim.MixDuration = 0f;
                    idleAnim.TimeScale = _idleTimeScale;
                }
            }
            
            AudioWebBridge.Instance.PlaySound(_cashoutSound);

            StartValueAnimationAfterOpen(openAnim);

            /*
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(true);

            var from = wasActive ? transform.localScale : Vector3.zero;
            transform.localScale = from;
            float fromAlpha = wasActive ? GetCanvasAlpha() : 0f;
            SetCanvasAlpha(fromAlpha);
            StartScaleAnimation(from, Vector3.one, fromAlpha, 1f, _openDuration, EasingType.OutBack, false);

            */
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                transform.localScale = Vector3.zero;
                SetCanvasAlpha(0f);
                return;
            }
            
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            StartFade(0f, _closeDuration);
            StopValueAnimation();
            StopValueStartRoutine();
            if (_openTrackEntry != null)
            {
                _openTrackEntry.Complete -= OnOpenAnimationComplete;
                _openTrackEntry.End -= OnOpenAnimationEnd;
                _openTrackEntry = null;
            }
            /*

            
            StartScaleAnimation(transform.localScale, Vector3.zero, GetCanvasAlpha(), 0f, _closeDuration, EasingType.InOutBack, true);
            */
        }

        /*
        private void StartScaleAnimation(Vector3 from, Vector3 to, float fromAlpha, float toAlpha, float duration, EasingType easingType, bool deactivateOnComplete)
        {
            if (_scaleRoutine != null)
                StopCoroutine(_scaleRoutine);

            _scaleRoutine = StartCoroutine(AnimateScale(from, to, fromAlpha, toAlpha, duration, easingType, deactivateOnComplete));
        }

        private IEnumerator AnimateScale(Vector3 from, Vector3 to, float fromAlpha, float toAlpha, float duration, EasingType easingType, bool deactivateOnComplete)
        {
            if (duration <= 0f)
            {
                transform.localScale = to;
                SetCanvasAlpha(toAlpha);
                if (deactivateOnComplete)
                    gameObject.SetActive(false);
                _scaleRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateEase(t, easingType);
                transform.localScale = Vector3.LerpUnclamped(from, to, eased);
                SetCanvasAlpha(Mathf.LerpUnclamped(fromAlpha, toAlpha, eased));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localScale = to;
            SetCanvasAlpha(toAlpha);
            if (deactivateOnComplete)
                gameObject.SetActive(false);
            _scaleRoutine = null;
        }

        private float EvaluateEase(float t, EasingType easingType)
        {
            switch (easingType)
            {
                case EasingType.OutBack:
                    return EaseOutBack(t);
                case EasingType.InOutBack:
                    return EaseInOutBack(t);
                default:
                    return t;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        private static float EaseInOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;

            if (t < 0.5f)
            {
                float p = 2f * t;
                return (p * p * ((c2 + 1f) * p - c2)) * 0.5f;
            }

            float q = 2f * t - 2f;
            return (q * q * ((c2 + 1f) * q + c2) + 2f) * 0.5f;
        }

        private enum EasingType
        {
            OutBack,
            InOutBack
        }

        private float GetCanvasAlpha()
        {
            return _canvasGroup.alpha;
        }
        */
        private void StartValueAnimationAfterOpen(TrackEntry openAnim)
        {
            StopValueAnimation();
            StopValueStartRoutine();

            if (!_canAnimateValue)
                return;

            SetValueText(0f);

            if (openAnim == null || openAnim.Animation == null || openAnim.Animation.Duration <= 0f)
            {
                StartValueAnimationIfNotStarted();
                return;
            }

            if (_openTrackEntry != null)
            {
                _openTrackEntry.Complete -= OnOpenAnimationComplete;
                _openTrackEntry.End -= OnOpenAnimationEnd;
            }

            _openTrackEntry = openAnim;
            _openTrackEntry.Complete += OnOpenAnimationComplete;
            _openTrackEntry.End += OnOpenAnimationEnd;

            float timeScale = Mathf.Abs(openAnim.TimeScale) > 0.0001f ? Mathf.Abs(openAnim.TimeScale) : 1f;
            float openDurationRealtime = openAnim.Animation.Duration / timeScale;
            float delay = openDurationRealtime + _valueAnimStartOffset;
            if (delay <= 0f)
                StartValueAnimationIfNotStarted();
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
            StartValueAnimationIfNotStarted();
        }

        private IEnumerator DelayStartValue(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _valueStartRoutine = null;
            StartValueAnimationIfNotStarted();
        }

        private void StartValueAnimationIfNotStarted()
        {
            if (_valueRoutine != null)
                return;

            _valueRoutine = StartCoroutine(AnimateValue());
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

        private void StartFade(float toAlpha, float duration)
        {
            StopFade();

            float fromAlpha = _canvasGroup.alpha;
            if (duration <= 0f)
            {
                SetCanvasAlpha(toAlpha);
                return;
            }

            _fadeRoutine = StartCoroutine(FadeRoutine(fromAlpha, toAlpha, duration));
        }

        private void StopFade()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _canvasGroup.alpha = toAlpha;
            _fadeRoutine = null;
        }

        private IEnumerator AnimateValue()
        {
            if (_valueAnimDuration <= 0f)
            {
                SetValueText(_targetValue);
                _valueRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _valueAnimDuration)
            {
                float t = Mathf.Clamp01(elapsed / _valueAnimDuration);
                float value = Mathf.LerpUnclamped(0f, _targetValue, t);
                SetValueText(value);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetValueText(_targetValue);
            _valueRoutine = null;
        }

        private void SetValueText(float value)
        {
            _cashoutValue.text = AmountTextUtility.FormatAmount(value, _valuePrefix, _valueSuffix, _valueDecimals);
        }
    }

    public static class AmountTextUtility
    {
        private static readonly Regex NumberRegex = new Regex(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        public static string FormatAmount(float value, string prefix, string suffix, int decimals)
        {
            string formattedValue = decimals > 0
                ? value.ToString("F" + decimals, CultureInfo.InvariantCulture)
                : Mathf.Round(value).ToString("F0", CultureInfo.InvariantCulture);

            string trimmedPrefix = prefix?.Trim() ?? string.Empty;
            string trimmedSuffix = suffix?.Trim() ?? string.Empty;
            string leading = trimmedPrefix.Length > 0 ? trimmedPrefix + " " : string.Empty;
            string trailing = trimmedSuffix.Length > 0 ? " " + trimmedSuffix : string.Empty;
            return $"{leading}{formattedValue}{trailing}";
        }

        public static bool TryParseAmount(string text, out float value, out string prefix, out string suffix, out int decimals)
        {
            value = 0f;
            prefix = "";
            suffix = "";
            decimals = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match match = NumberRegex.Match(text);
            if (!match.Success)
                return false;

            string numberText = match.Value;
            int separatorIndex = numberText.IndexOfAny(new[] { '.', ',' });
            if (separatorIndex >= 0)
                decimals = numberText.Length - separatorIndex - 1;

            string normalized = numberText.Replace(',', '.');
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            if (value < 0f)
                value = 0f;

            prefix = text.Substring(0, match.Index);
            suffix = text.Substring(match.Index + match.Length);
            return true;
        }

        public static int GetDecimalPlaces(float amount)
        {
            string text = amount.ToString(CultureInfo.InvariantCulture);
            int separatorIndex = text.IndexOf('.');
            if (separatorIndex < 0)
                return 0;

            return Mathf.Clamp(text.Length - separatorIndex - 1, 0, 6);
        }
    }
}
