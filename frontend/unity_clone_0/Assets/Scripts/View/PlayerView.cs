using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace SnakeLudo.View
{
    // IPointerClickHandler lets Unity's EventSystem detect taps and mouse clicks
    // Works for both touch screens and mouse without any raycasting code
    public class PlayerView : MonoBehaviour, IPointerClickHandler
    {
        public float moveSpeed = 5f;
        public float heightOffset = 0.3f;

        // Set by BoardManager after spawning so this view knows its identity
        public int playerIndex = -1;
        public int tokenId = -1;

        private Renderer rend;
        private Color baseColor;
        private Vector3 originalScale;

        // Callback to BoardManager when this token is tapped
        // BoardManager sets this after spawning
        public System.Action<int, int> OnTapped; // (playerIndex, tokenId)

        void Awake()
        {
            rend = GetComponent<Renderer>();
            originalScale = transform.localScale;
        }

        // ── IPointerClickHandler ──────────────────────────────
        // Called by Unity's EventSystem on tap or mouse click
        // Requires: Collider on this GameObject + EventSystem in scene
        public void OnPointerClick(PointerEventData eventData)
        {
            OnTapped?.Invoke(playerIndex, tokenId);
        }

        // ── Position ──────────────────────────────────────────

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        // ── Color / Selection ─────────────────────────────────

        public void SetColor(Color color)
        {
            baseColor = color;
            if (rend == null) return;

            // Instance the material so each token gets its own color
            // Without this all tokens share one material and color each other
            rend.material = new Material(rend.sharedMaterial);

            // URP uses _BaseColor, not _Color
            // Setting both covers Built-in and URP pipelines
            rend.material.color = color;
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", color);
        }

        public void SetSelected(bool isSelected)
        {
            if (rend == null) return;

            Color displayColor = isSelected
                ? Color.Lerp(baseColor, Color.white, 0.5f)
                : baseColor;

            rend.material.color = displayColor;
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", displayColor);

            transform.localScale = isSelected ? originalScale * 1.3f : originalScale;
        }

        // ── Movement ──────────────────────────────────────────

        public IEnumerator MoveToTile(TileView tile)
        {
            float tileHeight = tile.transform.localScale.y;
            Vector3 targetPos = tile.transform.position + Vector3.up * (tileHeight / 2f + heightOffset);

            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }

            transform.position = targetPos;
        }

        public IEnumerator MoveAlongPath(List<TileView> path)
        {
            foreach (var tile in path)
                yield return MoveToTile(tile);
        }

        public IEnumerator ArcTo(TileView targetTile, float arcHeight = 3f)
        {
            float tileHeight = targetTile.transform.localScale.y;
            Vector3 targetPos = targetTile.transform.position + Vector3.up * (tileHeight / 2f + heightOffset);
            Vector3 startPos = transform.position;
            Vector3 peak = (startPos + targetPos) / 2f + Vector3.up * arcHeight;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * moveSpeed * 0.5f;
                t = Mathf.Clamp01(t);

                Vector3 a = Vector3.Lerp(startPos, peak, t);
                Vector3 b = Vector3.Lerp(peak, targetPos, t);
                transform.position = Vector3.Lerp(a, b, t);

                yield return null;
            }

            transform.position = targetPos;
        }

        // Arc from anywhere to a world position (used for start zone → board)
        public IEnumerator ArcToPosition(Vector3 targetPos, float arcHeight = 2f)
        {
            Vector3 startPos = transform.position;
            Vector3 peak = (startPos + targetPos) / 2f + Vector3.up * arcHeight;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * moveSpeed * 0.5f;
                t = Mathf.Clamp01(t);

                Vector3 a = Vector3.Lerp(startPos, peak, t);
                Vector3 b = Vector3.Lerp(peak, targetPos, t);
                transform.position = Vector3.Lerp(a, b, t);

                yield return null;
            }

            transform.position = targetPos;
        }
    }
}