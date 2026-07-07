using System;
using System.Collections;
using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// A single notification entry managed by NotificationManager's object pool.
    ///
    /// Call Show() to display; the item auto-returns to the pool after duration.
    /// Call ForceComplete() to immediately dismiss.
    /// </summary>
    public class NotificationItem : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Elements")]
        [SerializeField] private TextMeshProUGUI messageLabel;
        [SerializeField] private Image           iconImage;
        [SerializeField] private Image           borderImage;
        [SerializeField] private CanvasGroup     canvasGroup;

        [Header("Type Colors")]
        [SerializeField] private Color infoColor    = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color warningColor = new Color(1f,   0.7f, 0.1f);
        [SerializeField] private Color errorColor   = new Color(0.9f, 0.2f, 0.2f);

        [Header("Sprites (optional)")]
        [SerializeField] private Sprite infoIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite errorIcon;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration  = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.4f;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private Action    _onComplete;
        private Coroutine _routine;

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Displays the notification for the specified duration, then calls onComplete.</summary>
        public void Show(string message, NotificationType type, float duration, Action onComplete)
        {
            if (_routine != null) StopCoroutine(_routine);

            _onComplete = onComplete;

            if (messageLabel != null) messageLabel.text = message;
            ApplyType(type);

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(true);

            _routine = StartCoroutine(NotificationRoutine(duration));
        }

        /// <summary>Immediately fades out and returns to pool.</summary>
        public void ForceComplete()
        {
            if (_routine != null) StopCoroutine(_routine);
            _onComplete?.Invoke();
            _onComplete = null;
        }

        // ─────────────────────────────────────────────
        //  Coroutine
        // ─────────────────────────────────────────────

        private IEnumerator NotificationRoutine(float duration)
        {
            // Fade in
            yield return FadeTo(1f, fadeInDuration);

            // Hold
            yield return new WaitForSecondsRealtime(duration);

            // Fade out
            yield return FadeTo(0f, fadeOutDuration);

            _onComplete?.Invoke();
            _onComplete = null;
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            if (canvasGroup == null) yield break;
            float start   = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed           += Time.unscaledDeltaTime;
                canvasGroup.alpha  = Mathf.Lerp(start, target, elapsed / seconds);
                yield return null;
            }
            canvasGroup.alpha = target;
        }

        // ─────────────────────────────────────────────
        //  Type Styling
        // ─────────────────────────────────────────────

        private void ApplyType(NotificationType type)
        {
            Color col = type switch
            {
                NotificationType.Success => successColor,
                NotificationType.Warning => warningColor,
                NotificationType.Error   => errorColor,
                _                        => infoColor
            };

            if (borderImage != null) borderImage.color = col;

            if (iconImage != null)
            {
                Sprite icon = type switch
                {
                    NotificationType.Success => successIcon,
                    NotificationType.Warning => warningIcon,
                    NotificationType.Error   => errorIcon,
                    _                        => infoIcon
                };
                iconImage.sprite  = icon;
                iconImage.enabled = icon != null;
            }
        }
    }
}
