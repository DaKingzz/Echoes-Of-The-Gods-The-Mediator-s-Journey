using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
{
    public GameObject dialoguePanel;
    public GameObject prompt;
    public bool playerIsClose;
    private PlayerController playerMovement;
    private Dialogue dialogue;
    private SwordWeapon swordWeapon;
    // Start is called before the first frame update
    void Start()
    {
        
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerMovement = player.GetComponent<PlayerController>();
                swordWeapon = player.GetComponent<SwordWeapon>();
            }
        
    }

    // Update is called once per frame
    void Update()
    {
        if (playerMovement == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerMovement = player.GetComponent<PlayerController>();
                swordWeapon = player.GetComponent<SwordWeapon>();
            }
        }

        if (playerIsClose)
        {
            if (!prompt.activeInHierarchy)
            {
                prompt.SetActive(true);
            }

        }
        else
        {
            if (prompt.activeInHierarchy)
            {
                prompt.SetActive(false);
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && playerIsClose)
        {
            if (!dialoguePanel.activeInHierarchy)
            {
                dialoguePanel.SetActive(true);
                if (playerMovement != null)
                {
                    playerMovement.enabled = false;

                }
                if (swordWeapon != null)
                {
                    swordWeapon.enabled = false;

                }


                dialogue = dialoguePanel.GetComponent<Dialogue>();
                if (dialogue != null)
                {
                    dialogue.startDialogue();
                }

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
