using UnityEngine;
using SnakeLudo.View;
using System.Collections;


namespace SnakeLudo.Managers
{
    // Owns all UI feedback — turn indicator and event messages
    // BoardManager calls this with raw game data
    // UIManager decides what text to show and how to style it
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("UI Components")]
        public TurnIndicatorView turnIndicator;
        public EventMessageView eventMessage;
        public WinScreenView winScreen;

        // Player colors — must match BoardManager.playerColors
        private Color[] playerColors = new Color[]
        {
            new Color(0.2f, 0.4f, 1.0f),   // blue  — Player 0
            new Color(1.0f, 0.2f, 0.2f),   // red   — Player 1
            new Color(0.2f, 0.8f, 0.2f),   // green — Player 2
            new Color(1.0f, 0.85f, 0.1f)   // yellow— Player 3
        };

        void Awake()
        {
            Instance = this;
            Debug.Log($"✅ UIManager.Awake — Instance assigned, turnIndicator={turnIndicator != null}, eventMessage={eventMessage != null}");
        }

        // ── Setup ─────────────────────────────────────────────

        // Called by BoardManager after init so UIManager knows who the local player is
        public void Setup(int myPlayerIndex)
        {
            Debug.Log($"✅ UIManager.Setup — myPlayerIndex={myPlayerIndex}, turnIndicator={turnIndicator != null}, eventMessage={eventMessage != null}");
            if (turnIndicator != null)
                turnIndicator.SetupPlayers(
                    playerColors[0],
                    playerColors[1],
                    myPlayerIndex
                );
        }

        // ── Turn indicator ────────────────────────────────────

        public void OnGameStart(int firstPlayerIndex)
        {
            Debug.Log($"✅ UIManager.OnGameStart — firstPlayer={firstPlayerIndex}, turnIndicator={turnIndicator != null}, eventMessage={eventMessage != null}");
            if (turnIndicator != null)
                turnIndicator.SetActiveTurn(firstPlayerIndex);

            bool isMyTurn = firstPlayerIndex == NetworkManager.Instance.myPlayerIndex;
            ShowMessage(
                isMyTurn ? "Game started — Your turn!" : "Game started — Opponent goes first",
                isMyTurn
            );
        }

        public void OnTurnChanged(int activePlayerIndex)
        {
            if (turnIndicator != null)
                turnIndicator.SetActiveTurn(activePlayerIndex);
        }

        // ── Roll result ───────────────────────────────────────

        public void OnRollResult(int playerIndex, int roll, bool autoPass)
        {
            bool isMe = playerIndex == NetworkManager.Instance.myPlayerIndex;

            if (autoPass)
            {
                ShowMessage(
                    isMe ? "No valid moves — turn skipped"
                         : "Opponent has no valid moves — skipped",
                    isMe
                );
                return;
            }

            if (isMe)
                eventMessage?.ShowMyMessage($"You rolled {roll} — select a token");
            else
                eventMessage?.ShowOpponentMessage($"Opponent rolled {roll}");
        }

        // ── Move events ───────────────────────────────────────

        public void OnMoveEvent(int playerIndex, string moveType, int toLevel, bool hasCollision)
        {
            bool isMe = playerIndex == NetworkManager.Instance.myPlayerIndex;

            switch (moveType)
            {
                case "normal":
                    // Only show collision messages for normal moves — movement speaks for itself
                    if (hasCollision)
                    {
                        if (isMe)
                            eventMessage?.ShowEventMessage("You hit opponent's token!");
                        else
                            eventMessage?.ShowEventMessage("Your token was sent back!");
                    }
                    break;

                case "levelUp":
                    if (isMe)
                        eventMessage?.ShowEventMessage($"Level Up! Reached Level {toLevel} 🎉");
                    else
                        eventMessage?.ShowOpponentMessage($"Opponent reached Level {toLevel}");
                    break;

                case "ladderClimb":
                    if (isMe)
                        eventMessage?.ShowEventMessage($"Ladder! Climbed to Level {toLevel} 🪜");
                    else
                        eventMessage?.ShowOpponentMessage("Opponent climbed a ladder");
                    break;

                case "snakeSlide":
                    if (isMe)
                        eventMessage?.ShowEventMessage($"Snake! Dropped to Level {toLevel} 🐍");
                    else
                        eventMessage?.ShowOpponentMessage("Opponent hit a snake");
                    break;

                case "overshoot":
                    if (isMe)
                        eventMessage?.ShowMyMessage("Can't move — would overshoot the goal");
                    else
                        eventMessage?.ShowOpponentMessage("Opponent overshot");
                    break;

                case "win":
                    // Handled separately by OnPlayerWon
                    break;
            }
        }

        public void OnNextTurn(int nextPlayerIndex)
        {
            if (turnIndicator != null)
                turnIndicator.SetActiveTurn(nextPlayerIndex);

            bool isMyTurn = nextPlayerIndex == NetworkManager.Instance.myPlayerIndex;
            if (isMyTurn)
                eventMessage?.ShowMyMessage("Your turn — Roll the dice");
        }

        // ── Win ───────────────────────────────────────────────

        public void OnPlayerWon(int winnerIndex)
        {
            bool isMe = winnerIndex == NetworkManager.Instance.myPlayerIndex;

            // Show event message briefly then let win screen take over
            eventMessage?.ShowEventMessage(isMe ? "🏆 You Win!" : "Opponent wins");

            // Show win screen after a short delay so the final move animation completes
            StartCoroutine(ShowWinScreenDelayed(winnerIndex, isMe));
        }

        IEnumerator ShowWinScreenDelayed(int winnerIndex, bool didIWin)
        {
            yield return new WaitForSeconds(1.5f);
            if (winScreen != null)
                winScreen.Show(didIWin, winnerIndex);
        }

        // ── Helper ────────────────────────────────────────────

        void ShowMessage(string text, bool isMe)
        {
            if (isMe) eventMessage?.ShowMyMessage(text);
            else eventMessage?.ShowOpponentMessage(text);
        }
    }
}