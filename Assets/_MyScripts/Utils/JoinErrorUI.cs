// JoinErrorUI.cs
using UnityEngine;
using TMPro;
using System.Collections;

public class JoinErrorUI : MonoBehaviour 
{
  [SerializeField] TMP_Text label;
  [SerializeField] float autoHideSeconds = 2f;
  
  public void ShowInvalid()
    {
        if (label) label.text = "Invalid code";
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HideLater());
    }
  IEnumerator HideLater()
    {
        yield return new WaitForSeconds(autoHideSeconds);
        gameObject.SetActive(false);
    }
}
