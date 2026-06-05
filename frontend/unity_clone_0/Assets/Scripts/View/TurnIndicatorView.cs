using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SnakeLudo.View
{
    // Manages the persistent turn indicator panel at the top of the screen
    // Shows both player slots — active player highlighted, inactive dimmed
    public class TurnIndicatorView : MonoBehaviour
    {
        [Header("Player 0 Slot")]
        public Image player0ColorDot;   // small colored circle
        public TextMeshProUGUI player0Label;    // "You" or "P1"
        public Image player0Background; // slot background panel

        [Header("Player 1 Slot")]
        public Image player1ColorDot;
        public TextMeshProUGUI player1Label;
        public Image player1Background;

        [Header("Active / Inactive appearance")]
        public Color activeBackgroundColor = new Color(1f, 1f, 1f, 0.25f);
        public Color inactiveBackgroundColor = new Color(0f, 0f, 0f, 0.15f);
        public Color activeLabelColor = Color.white;
        public Color inactiveLabelColor = new Color(1f, 1f, 1f, 0.45f);

        // ── Public API ────────────────────────────────────────

        // Call once on init to set player colors and labels
        public void SetupPlayers(Color p0Color, Color p1Color, int myPlayerIndex)
        {
            if (player0ColorDot != null) player0ColorDot.color = p0Color;
            if (player1ColorDot != null) player1ColorDot.color = p1Color;

            // Label local player as "You", opponent as "P1"/"P2"
            if (player0Label != null)
                player0Label.text = myPlayerIndex == 0 ? "You" : "P1";
            if (player1Label != null)
                player1Label.text = myPlayerIndex == 1 ? "You" : "P2";
        }

        // Called whenever the active player changes
        public void SetActiveTurn(int activePlayerIndex)
        {
            bool p0Active = activePlayerIndex == 0;

            SetSlotAppearance(player0Background, player0Label, p0Active);
            SetSlotAppearance(player1Background, player1Label, !p0Active);
        }

        // ── Helpers ───────────────────────────────────────────

        void SetSlotAppearance(Image background, TextMeshProUGUI label, bool isActive)
        {
            if (background != null)
                background.color = isActive ? activeBackgroundColor : inactiveBackgroundColor;
            if (label != null)
                label.color = isActive ? activeLabelColor : inactiveLabelColor;
        }
    }
}