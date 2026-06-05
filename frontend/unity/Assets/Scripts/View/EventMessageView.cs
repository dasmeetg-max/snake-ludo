using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace SnakeLudo.View
{
    public class EventMessageView : MonoBehaviour
    {
        public TextMeshProUGUI messageText;
        public float fadeInDuration = 0.3f;
        public float holdDuration = 1.5f;
        public float fadeOutDuration = 0.3f;

        public Color myMessageColor = new Color(1.0f, 0.95f, 0.6f);
        public Color opponentMessageColor = new Color(0.8f, 0.8f, 0.8f);
        public Color eventMessageColor = new Color(1.0f, 0.6f, 0.2f);

        private Queue<(string text, Color color)> messageQueue = new Queue<(string, Color)>();
        private bool isDisplaying = false;

        public void ShowMessage(string text, Color color)
        {
            messageQueue.Enqueue((text, color));
            if (!isDisplaying)
            {
                gameObject.SetActive(true);
                StartCoroutine(ProcessQueue());
            }
        }

        public void ShowMyMessage(string text) => ShowMessage(text, myMessageColor);
        public void ShowOpponentMessage(string text) => ShowMessage(text, opponentMessageColor);
        public void ShowEventMessage(string text) => ShowMessage(text, eventMessageColor);

        IEnumerator ProcessQueue()
        {
            isDisplaying = true;

            while (messageQueue.Count > 0)
            {
                var (text, color) = messageQueue.Dequeue();
                yield return StartCoroutine(DisplayMessage(text, color));
            }

            // ONLY deactivate after the queue is totally empty
            isDisplaying = false;
            gameObject.SetActive(false);
        }

        IEnumerator DisplayMessage(string text, Color color)
        {
            messageText.text = text;
            messageText.color = new Color(color.r, color.g, color.b, 0f);

            // Fade in
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                messageText.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            // Hold
            yield return new WaitForSeconds(holdDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                messageText.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            // Just ensure it's fully transparent at the end, but DO NOT deactivate here
            messageText.color = new Color(color.r, color.g, color.b, 0f);
        }
    }
}