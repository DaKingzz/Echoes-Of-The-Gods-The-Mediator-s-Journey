using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public GameObject InventoryMenu;
    public bool inventoryActivated;
    public ItemSlot[] itemSlot;

    public static InventoryManager Instance;

    private HashSet<string> usedKeys = new HashSet<string>();
    private HashSet<string> pickedUpItems = new HashSet<string>();

    public ItemSlot currentSelectedKey;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sfxUnlockSuccess;
    public AudioClip sfxUnlockFail;
    public AudioClip sfxPickupItem;

    [Header("UI")]
    public Button useItemButton;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        //InventoryMenu.SetActive(false);
        //inventoryActivated = false;
        UpdateUseButtonState();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I) && inventoryActivated)
        {
            Time.timeScale = 1; 
            InventoryMenu.SetActive(false);
            inventoryActivated = false;
        }

        else if (Input.GetKeyDown(KeyCode.I) && !inventoryActivated)
        {
            Time.timeScale = 0; 
            InventoryMenu.SetActive(true);
            inventoryActivated = true;
        }
    }

    public void MarkKeyUsed(string keyName)
    {
        usedKeys.Add(keyName);
    }

    public bool IsKeyAlreadyUsed(string keyName)
    {
        return usedKeys.Contains(keyName);
    }
    public void MarkItemPickedUp(string itemName)
    {
        pickedUpItems.Add(itemName);
    }

    public bool HasPickedUp(string itemName)
    {
        return pickedUpItems.Contains(itemName);
    }


    public void AddItem(string itemName, Sprite itemSprite, string itemDescription)
    {
        for (int i = 0; i < itemSlot.Length; i++)
        {
            if (itemSlot[i].isFull == false)
            {
                itemSlot[i].AddItem(itemName, itemSprite, itemDescription);
                return; 
            }
        }
    }

    public void DeselectAllSlots()
    {
        for (int i = 0; i < itemSlot.Length; i++)
        {
            itemSlot[i].selectedShader.SetActive(false);
            itemSlot[i].thisItemSelected = false;
        }

        currentSelectedKey = null;
        UpdateUseButtonState();
    }

    public void RemoveItem(string keyName)
    {
        foreach (var slot in itemSlot)
        {
            if (slot.itemName == keyName)
            {
                slot.EmptySlot(); 
                slot.isFull = false;
                return;
            }
        }
    }

    public void UseSelectedItem()
    {
        if (currentSelectedKey == null || !currentSelectedKey.isFull)
            return;

        string keyName = currentSelectedKey.itemName;

        // Attempt to use key ONLY if touching a portal
        BossPortal[] bPortals = FindObjectsOfType<BossPortal>();
        foreach (var portal in bPortals)
        {
            if (portal.IsPlayerTouching && portal.requiredKeyName == keyName)
            {
                // --- PLAY SUCCESS SOUND ---
                if(audioSource && sfxUnlockSuccess)
                    audioSource.PlayOneShot(sfxUnlockSuccess);

                // Unlock portal, remove key
                portal.UnlockPortal();
                MarkKeyUsed(keyName);
                RemoveItem(keyName);
                currentSelectedKey = null;
                UpdateUseButtonState();
                DeselectAllSlots(); 

                // Close inventory
                InventoryMenu.SetActive(false);
                inventoryActivated = false;
                Time.timeScale = 1;

                // Load destination scene
                portal.LoadDestinationBossScene();
                return;
            }
        }

        // --- NO PORTAL FOUND OR WRONG KEY ---
        if (audioSource && sfxUnlockFail)
            audioSource.PlayOneShot(sfxUnlockFail);
    }
    public void UpdateUseButtonState()
    {
        if (currentSelectedKey != null && currentSelectedKey.isFull)
            useItemButton.interactable = true;  // button enabled
        else
            useItemButton.interactable = false; // button dimmed/faded
    }

}
