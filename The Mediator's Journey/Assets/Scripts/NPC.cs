using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
{

    public GameObject dialoguePanel;
    public bool playerIsClose;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && playerIsClose)
        {
            if (dialoguePanel.activeInHierarchy)
            {
                dialoguePanel.SetActive(false);
            }
            else
            {
                
                dialoguePanel.SetActive(true);
               
            }
        }
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = true;
        }
    }

    public void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = false;
        }
    }

}
