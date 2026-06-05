using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Persistent singleton that handles scene transitions with a fade
// Survives scene loads via DontDestroyOnLoad
// Add this to a GameObject in HomeScene with a full-screen black Image child
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public Image fadeOverlay;       // full screen black Image — assign in Inspector
    public float fadeDuration = 0.4f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Fade in from black on first load
        StartCoroutine(FadeIn());
    }

    // ── Public API ────────────────────────────────────────────

    public void LoadScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    // ── Fade coroutines ───────────────────────────────────────

    IEnumerator FadeAndLoad(string sceneName)
    {
        yield return StartCoroutine(FadeOut());
        SceneManager.LoadScene(sceneName);
        yield return StartCoroutine(FadeIn());
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        SetAlpha(1f);
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
    }

    void SetAlpha(float alpha)
    {
        if (fadeOverlay != null)
            fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
    }
}