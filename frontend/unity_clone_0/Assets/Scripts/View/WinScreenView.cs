using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace SnakeLudo.View
{
    // Full screen win/lose overlay
    // Activated by UIManager when playerWon event arrives
    // Fades in over the game board — board stays visible underneath
    public class WinScreenView : MonoBehaviour
    {
        [Header("Text")]
        public TextMeshProUGUI resultText;       // "You Win! 🏆" or "You Lose"
        public TextMeshProUGUI subtitleText;     // "Player 1 wins the game"

        [Header("Buttons")]
        public Button playAgainButton;           // placeholder — no action yet
        public Button homeButton;                // placeholder — no action yet

        [Header("Background")]
        public Image backgroundOverlay;         // semi-transparent dark panel

        [Header("Animation")]
        public float fadeInDuration = 0.6f;

        [Header("Colors")]
        public Color winColor = new Color(1.0f, 0.85f, 0.1f);  // gold
        public Color loseColor = new Color(0.7f, 0.7f, 0.7f);   // muted grey

        // ── Public API ────────────────────────────────────────

        // Called by UIManager with the result for the local player
        public void Show(bool didIWin, int winnerIndex)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeIn(didIWin, winnerIndex));
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // ── Animation ─────────────────────────────────────────

        IEnumerator FadeIn(bool didIWin, int winnerIndex)
        {
            // Set text content before fade starts
            if (resultText != null)
            {
                resultText.text = didIWin ? "You Win! 🏆" : "You Lose";
                resultText.color = didIWin ? winColor : loseColor;
            }

            if (subtitleText != null)
                subtitleText.text = $"Player {winnerIndex + 1} wins the game";

            // Start fully transparent
            SetAlpha(0f);

            // Fade in smoothly
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                // Ease in — slow start, faster finish
                float eased = alpha * alpha;
                SetAlpha(eased);
                yield return null;
            }

            SetAlpha(1f);
        }

        // Sets alpha on all visual elements simultaneously
        void SetAlpha(float alpha)
        {
            if (backgroundOverlay != null)
            {
                Color c = backgroundOverlay.color;
                // Background max alpha is 0.85 — board still visible underneath
                backgroundOverlay.color = new Color(c.r, c.g, c.b, alpha * 0.85f);
            }

            if (resultText != null)
            {
                Color c = resultText.color;
                resultText.color = new Color(c.r, c.g, c.b, alpha);
            }

            if (subtitleText != null)
            {
                Color c = subtitleText.color;
                subtitleText.color = new Color(c.r, c.g, c.b, alpha);
            }

            if (playAgainButton != null)
            {
                var img = playAgainButton.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, alpha);
                }
                var label = playAgainButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    Color c = label.color;
                    label.color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            if (homeButton != null)
            {
                var img = homeButton.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, alpha);
                }
                var label = homeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    Color c = label.color;
                    label.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }
    }
}