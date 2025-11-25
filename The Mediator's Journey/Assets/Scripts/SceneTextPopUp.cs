using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SceneTextPopUp : MonoBehaviour
{
    public TextMeshProUGUI textElement;
    public float displayTime = 3f;

    // Start is called before the first frame update
    void Start()
    {
        if (textElement != null)
        {
            textElement.gameObject.SetActive(true);
            StartCoroutine(HideTextAfterDelay());
        }
    }

    IEnumerator HideTextAfterDelay()
    {
        yield return new WaitForSeconds(displayTime);
        textElement.gameObject.SetActive(false);  
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
