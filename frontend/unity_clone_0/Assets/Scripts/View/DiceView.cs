using UnityEngine;
using System.Collections;
using TMPro;

namespace SnakeLudo.View
{
    // Attached to the Dice prefab root GameObject
    // BoardManager calls PlayRoll(result) when server responds with a dice value
    public class DiceView : MonoBehaviour
    {
        // ── Inspector refs ────────────────────────────────────────
        // The 6 face label TextMeshPro components
        // Assign in Inspector in order: face1, face2, face3, face4, face5, face6
        public TextMeshPro face1;
        public TextMeshPro face2;
        public TextMeshPro face3;
        public TextMeshPro face4;
        public TextMeshPro face5;
        public TextMeshPro face6;

        // ── Animation settings ────────────────────────────────────
        public float spinDuration = 1.2f;  // how long the tumbling lasts
        public float spinSpeed = 720f;  // degrees per second during tumble
        public float landDuration = 0.4f;  // how long the landing snap takes

        // ── Face rotations ────────────────────────────────────────
        // Each entry is the LOCAL euler rotation that places that face number on top
        // Calibrated for the prefab layout defined in the setup guide
        // Face 1 up = no rotation (face1 quad faces up by default)
        private static readonly Vector3[] faceUpRotations = new Vector3[]
        {
            Vector3.zero,               // 1 up
            new Vector3(-90f,  0f, 0f), // 2 up
            new Vector3(  0f,  0f, 90f),// 3 up
            new Vector3(  0f,  0f,-90f),// 4 up
            new Vector3( 90f,  0f, 0f), // 5 up
            new Vector3(180f,  0f, 0f)  // 6 up
        };

        // ─────────────────────────────────────────────────────────
        // PUBLIC API — called by BoardManager
        // ─────────────────────────────────────────────────────────

        // Show dice and play full roll animation ending on the given result (1-6)
        public void PlayRoll(int result)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(RollAnimation(result));
        }

        // Hide dice — called after token selection to clean up
        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────
        // ANIMATION
        // ─────────────────────────────────────────────────────────

        IEnumerator RollAnimation(int result)
        {
            // ── Phase 1: Random tumble ────────────────────────────
            // Spin randomly on all axes for spinDuration seconds
            float elapsed = 0f;
            Vector3 spinAxis = new Vector3(
                Random.Range(0.5f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(0.2f, 0.5f)
            ).normalized;

            // Start from a random rotation so each roll looks different
            transform.localRotation = Random.rotation;

            while (elapsed < spinDuration)
            {
                // Gradually slow the spin as we approach the end of tumble phase
                float slowdown = Mathf.Lerp(1f, 0.1f, elapsed / spinDuration);
                transform.Rotate(spinAxis * spinSpeed * slowdown * Time.deltaTime, Space.World);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── Phase 2: Land on correct face ─────────────────────
            // Smoothly rotate to the pre-defined rotation for this result
            Quaternion startRot = transform.localRotation;
            Quaternion targetRot = Quaternion.Euler(faceUpRotations[result - 1]);

            elapsed = 0f;
            while (elapsed < landDuration)
            {
                float t = elapsed / landDuration;

                // Ease out — fast start, slow finish for a satisfying land
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                transform.localRotation = Quaternion.Slerp(startRot, targetRot, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Snap to exact final rotation
            transform.localRotation = targetRot;

            Debug.Log($"🎲 Dice landed on {result}");
        }
    }
}