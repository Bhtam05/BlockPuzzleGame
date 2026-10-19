using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle
{
    /// <summary>
    /// Nút bật/tắt một cài đặt (âm thanh, nhạc, rung...). Gắn vào Button, nối onClick tới Toggle().
    /// Bật: dùng sprite xanh, tắt: dùng sprite gỗ. Nhãn tự hiện "Tên: BẬT/TẮT" mỗi lần panel mở.
    /// </summary>
    public class SettingToggle : MonoBehaviour
    {
        [SerializeField] private SettingKind kind;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private string title = "Âm thanh";
        [SerializeField] private string onText = "BẬT";
        [SerializeField] private string offText = "TẮT";

        private void OnEnable() => Refresh();

        /// <summary>Gắn vào onClick của Button.</summary>
        public void Toggle()
        {
            bool now = !GameSettings.Get(kind);
            GameSettings.Set(kind, now);

            AudioManager.Play(Sfx.Click); // tắt âm thanh thì tự im lặng
            if (kind == SettingKind.Haptic && now) Haptics.Tap();

            Refresh();
        }

        private void Refresh()
        {
            bool on = GameSettings.Get(kind);
            if (label != null) label.text = title + ": " + (on ? onText : offText);
            if (background != null)
            {
                Sprite s = on ? onSprite : offSprite;
                if (s != null) background.sprite = s;
            }
        }
    }
}
