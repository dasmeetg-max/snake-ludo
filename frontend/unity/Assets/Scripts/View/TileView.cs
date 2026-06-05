using UnityEngine;
using System.Collections.Generic;

namespace SnakeLudo.View
{
    public class TileView : MonoBehaviour
    {
        // ── Identity ──────────────────────────────────────────
        public int level;
        public int tileIndex;

        // ── Transition ────────────────────────────────────────
        public int transitionToLevel = -1;
        public int transitionToTile = -1;
        public bool isLadder = false;
        public bool isSnake = false;
        public bool isSafe = false;

        // ── Occupancy ─────────────────────────────────────────
        // Tracks which PlayerViews are currently on this tile
        // Used to calculate dynamic slot positions
        private List<PlayerView> occupants = new List<PlayerView>();

        // Height offset above tile surface — tokens sit on top
        private const float TOKEN_HEIGHT_OFFSET = 0.3f;

        // Half-spread for multi-token layouts
        // Tokens spread 0.4 units apart on a 2x2 tile
        private const float SPREAD = 0.9f;

        private Renderer rend;

        void Awake()
        {
            rend = GetComponent<Renderer>();
        }

        // ── Occupancy API ─────────────────────────────────────

        // Called when a token arrives on this tile
        // Returns the world position the token should move to
        public Vector3 RegisterToken(PlayerView view)
        {
            if (!occupants.Contains(view))
                occupants.Add(view);

            RefreshPositions();

            // Return this token's assigned position
            int slot = occupants.IndexOf(view);
            return GetSlotWorldPosition(slot, occupants.Count);
        }

        // Called when a token leaves this tile
        public void UnregisterToken(PlayerView view)
        {
            occupants.Remove(view);
            RefreshPositions(); // reflow remaining tokens
        }

        // Recalculate and apply positions for all current occupants
        public void RefreshPositions()
        {
            int count = occupants.Count;
            for (int i = 0; i < count; i++)
            {
                if (occupants[i] != null)
                    occupants[i].SetPosition(GetSlotWorldPosition(i, count));
            }
        }

        // ── Slot position calculation ─────────────────────────
        // Dynamic layout based on token count:
        //   1 token  → center
        //   2 tokens → side by side
        //   3 tokens → triangle
        //   4 tokens → 2x2 grid

        public Vector3 GetSlotWorldPosition(int slotIndex, int totalTokens)
        {
            float tileTop = transform.position.y + transform.localScale.y / 2f + TOKEN_HEIGHT_OFFSET;
            Vector3 center = new Vector3(transform.position.x, tileTop, transform.position.z);

            Vector2 offset2D = GetSlotOffset(slotIndex, totalTokens);

            // Offset is in tile-local XZ space — tile faces outward so we rotate it
            // by the tile's Y rotation to align with the tile orientation
            float tileAngle = transform.eulerAngles.y * Mathf.Deg2Rad;
            float rotatedX = offset2D.x * Mathf.Cos(tileAngle) - offset2D.y * Mathf.Sin(tileAngle);
            float rotatedZ = offset2D.x * Mathf.Sin(tileAngle) + offset2D.y * Mathf.Cos(tileAngle);

            return center + new Vector3(rotatedX, 0f, rotatedZ);
        }

        Vector2 GetSlotOffset(int slotIndex, int totalTokens)
        {
            switch (totalTokens)
            {
                case 1:
                    return Vector2.zero; // center

                case 2:
                    // Side by side
                    float[] twoX = { -SPREAD * 0.5f, SPREAD * 0.5f };
                    return new Vector2(twoX[slotIndex], 0f);

                case 3:
                    // Triangle
                    Vector2[] three =
                    {
                        new Vector2(-SPREAD * 0.5f, -SPREAD * 0.3f),
                        new Vector2( SPREAD * 0.5f, -SPREAD * 0.3f),
                        new Vector2(0f,              SPREAD * 0.5f)
                    };
                    return three[slotIndex];

                case 4:
                default:
                    // 2x2 grid
                    Vector2[] four =
                    {
                        new Vector2(-SPREAD * 0.5f, -SPREAD * 0.5f),
                        new Vector2( SPREAD * 0.5f, -SPREAD * 0.5f),
                        new Vector2(-SPREAD * 0.5f,  SPREAD * 0.5f),
                        new Vector2( SPREAD * 0.5f,  SPREAD * 0.5f)
                    };
                    return four[Mathf.Min(slotIndex, 3)];
            }
        }

        // ── Visual helpers ────────────────────────────────────

        public bool HasTransition() { return transitionToLevel >= 0; }

        public void SetAsLadder()
        {
            isLadder = true;
            if (rend != null) rend.material.color = Color.green;
        }

        public void SetAsSnake()
        {
            isSnake = true;
            if (rend != null) rend.material.color = Color.red;
        }

        public void SetAsSafe()
        {
            isSafe = true;
            if (rend != null) rend.material.color = Color.yellow;
        }

        public void SetAsNormal()
        {
            if (rend != null) rend.material.color = Color.white;
        }
    }
}