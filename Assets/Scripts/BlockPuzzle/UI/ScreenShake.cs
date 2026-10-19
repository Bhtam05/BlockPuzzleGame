using DG.Tweening;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Rung camera. Gắn vào Main Camera. Chỉ rung thế giới game, HUD (Canvas Overlay) đứng yên
    /// nên chữ vẫn dễ đọc. Người chơi tắt được trong Cài đặt (GameSettings.ShakeOn).
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        [SerializeField] private bool shakeEnabled = true;
        [SerializeField] private int vibrato = 22;

        private Vector3 _home;

        /// <summary>Bật/tắt rung (nối vào nút cài đặt).</summary>
        public bool Enabled
        {
            get => shakeEnabled;
            set => shakeEnabled = value;
        }

        private void Awake() => _home = transform.position;

        /// <summary>Rung với biên độ (đơn vị World) và thời gian cho trước, tắt dần về 0.</summary>
        public void Shake(float strength, float duration)
        {
            if (!shakeEnabled || !GameSettings.ShakeOn || strength <= 0f) return;

            transform.DOKill();
            transform.position = _home; // về chỗ cũ trước khi rung lần mới, tránh lệch dần

            transform.DOShakePosition(duration, new Vector3(strength, strength, 0f), vibrato, 90f, false, true)
                .OnKill(() => transform.position = _home);
        }

        private void OnDisable()
        {
            transform.DOKill();
            transform.position = _home;
        }
    }
}
