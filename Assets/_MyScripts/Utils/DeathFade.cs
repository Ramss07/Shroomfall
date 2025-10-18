using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DeathFade : MonoBehaviour
{

    [SerializeField] private Button spectateButton;
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 1f;

    void Awake()
    {
        spectateButton.gameObject.SetActive(false);
    }

    IEnumerator FadeTo(float targetAlpha)
    {
        float start = fadeImage.color.a;
        float t = 0f;

        if(spectateButton)
        {
            spectateButton.gameObject.SetActive(false);
        }
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            Color c = fadeImage.color;
            c.a = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }
        if (targetAlpha == 1f && spectateButton)
        {
            spectateButton.gameObject.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    

    public void FadeInBlack()
    {
        StartCoroutine(FadeTo(1f));
    }

    public void FadeOutBlack()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        StartCoroutine(FadeTo(0f));
    }
}
