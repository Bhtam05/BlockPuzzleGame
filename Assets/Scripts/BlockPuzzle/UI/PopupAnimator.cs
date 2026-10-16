using DG.Tweening;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Hiệu ứng mở panel: nền mờ dần + cửa sổ bật lên (scale OutBack).
    /// Gắn vào root của panel (cần CanvasGroup). Chạy mỗi lần panel được SetActive(true).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform window;
        [SerializeField] private float fadeTime = 0.2f;
        [SerializeField] private float popTime = 0.3f;

        private CanvasGroup _group;

        private void Awake() => _group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            _group.alpha = 0f;
            _group.DOFade(1f, fadeTime).SetUpdate(true);

            if (window == null) return;
            window.localScale = Vector3.one * 0.8f;
            window.DOScale(1f, popTime).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnDisable()
        {
            _group.DOKill();
            if (window != null) window.DOKill();
        }
    }
}
