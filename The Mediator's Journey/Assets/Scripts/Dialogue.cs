using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.IO.Pipes;


public class Dialogue : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    public string[] lines;
    public float textSpeed;
    private PlayerController playerMovement;
    private SwordWeapon swordWeapon;
    public NPC npc;

    private int index;

    // Start is called before the first frame update
    void Start()
    {
        textComponent.text = string.Empty;
        playerMovement = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        startDialogue();
    }

    // Update is called once per frame
    void Update()
    {
        if(Mouse.current.leftButton.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
        {
            if(textComponent.text == lines[index])
            {
                NextLine();
            }
            else
            {
                StopAllCoroutines();
                textComponent.text = lines[index];
            }
        }
    }

    public void startDialogue()
    {
        index = 0;
        textComponent.text = "";
        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    IEnumerator TypeLine()
    {
        textComponent.text = string.Empty;
        foreach (char c in lines[index].ToCharArray())
        {
            textComponent.text += c;
            yield return new WaitForSeconds(textSpeed);
        }
    }

    void NextLine()
    {
        if(index < lines.Length -1)
        {
            index++;
            StopAllCoroutines();
            StartCoroutine(TypeLine());

            if (npc != null && index % 2 == 0)
            {
                npc.PlayDialogueAudio();
            }
        }
        else
        {
            gameObject.SetActive(false);
            if (npc != null)
            {
                npc.StopDialogueAudio();
            }  
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
            }
            if (swordWeapon != null)
            {
                swordWeapon.enabled = true;
            }

        }
    }
}
