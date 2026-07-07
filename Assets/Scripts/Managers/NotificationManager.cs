using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CityScape.Managers
{
    // ─────────────────────────────────────────────
    //  Notification Type
    // ─────────────────────────────────────────────

    /// <summary>Visual style of a notification.</summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    // ─────────────────────────────────────────────
    //  Notification Manager
    // ─────────────────────────────────────────────

    /// <summary>
    /// Object-pooled notification system.
    /// Call ShowNotification() from anywhere — no UI coupling required.
    ///
    /// Attach this to a persistent GameObject. The prefab must have a
    /// NotificationItem component.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static NotificationManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Prefab & Parent")]
        [Tooltip("The NotificationItem prefab (must have NotificationItem component).")]
        [SerializeField] private UI.NotificationItem notificationItemPrefab;

        [Tooltip("Parent transform inside the Canvas where items are instantiated.")]
        [SerializeField] private Transform notificationContainer;

        [Header("Pool Settings")]
        [SerializeField] private int poolDefaultSize = 5;
        [SerializeField] private int poolMaxSize     = 20;

        [Header("Display")]
        [SerializeField] private float defaultDuration = 3f;
        [SerializeField] private int   maxVisible      = 5;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private ObjectPool<UI.NotificationItem> _pool;
        private readonly List<UI.NotificationItem> _visible = new List<UI.NotificationItem>();

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _pool = new ObjectPool<UI.NotificationItem>(
                createFunc:      CreateItem,
                actionOnGet:     item => item.gameObject.SetActive(true),
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item => 
                {
                    if (item != null && item.gameObject != null) 
                    {
                        Destroy(item.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: poolDefaultSize,
                maxSize:         poolMaxSize);
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Displays a notification with the given message and type.
        /// </summary>
        public void ShowNotification(string message, NotificationType type = NotificationType.Info,
                                     float duration = -1f)
        {
            if (notificationItemPrefab == null || notificationContainer == null)
            {
                Debug.LogWarning($"[NotificationManager] {message}");
                return;
            }

            // Evict oldest if we are at the visible limit
            while (_visible.Count >= maxVisible)
            {
                var oldest = _visible[0];
                _visible.RemoveAt(0);
                oldest.ForceComplete();
            }

            float dur  = duration <= 0f ? defaultDuration : duration;
            var   item = _pool.Get();
            item.transform.SetParent(notificationContainer, false);
            item.transform.SetAsLastSibling();
            item.Show(message, type, dur, () =>
            {
                _visible.Remove(item); // O(n) but n ≤ 5 — acceptable
                _pool.Release(item);
            });

            _visible.Add(item);
        }

        // ─────────────────────────────────────────────
        //  Pool Factory
        // ─────────────────────────────────────────────

        private UI.NotificationItem CreateItem()
        {
            var go = Instantiate(notificationItemPrefab, notificationContainer);
            go.gameObject.SetActive(false);
            return go;
        }
    }
}
