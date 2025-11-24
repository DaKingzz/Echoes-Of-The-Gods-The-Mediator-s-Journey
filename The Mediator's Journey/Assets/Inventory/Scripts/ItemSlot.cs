using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IPointerClickHandler
{
    //Item Data 
    public string itemName;
    public Sprite itemSprite;
    public bool isFull;
    public string itemDescription;
    public Sprite emptySprite; 

    //Item Slot
    [SerializeField] private Image itemImage; 
    
    public GameObject selectedShader;
    public bool thisItemSelected;

    private InventoryManager inventoryManager;

    //Item Description Slot 
    public Image itemDescriptionImage;
    public TMP_Text ItemDescNameTxt;
    public TMP_Text ItemDescTxt;

    public void Start()
    {
        inventoryManager = InventoryManager.Instance;
    }

    public void AddItem (string itemName, Sprite itemSprite, string itemDescription)
    {
        this.itemName = itemName; 
        this.itemSprite = itemSprite;
        this.itemDescription = itemDescription;
        isFull = true;

        itemImage.sprite = itemSprite; 
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(eventData.button == PointerEventData.InputButton.Left)
        {
            OnLeftClick(); 
        }
    }

    public void OnLeftClick()
    {
        if (!isFull) return; 
        
        if (thisItemSelected)
        {
            inventoryManager.DeselectAllSlots();

            // Attempt to use key ONLY if touching a portal
            BossPortal[] bPortals = FindObjectsOfType<BossPortal>();
            foreach (var portal in bPortals)
            {
                if (portal.IsPlayerTouching && portal.requiredKeyName == itemName)
                {
                    // --- PLAY SUCCESS SOUND ---
                    if (inventoryManager.audioSource && inventoryManager.sfxUnlockSuccess)
                        inventoryManager.audioSource.PlayOneShot(inventoryManager.sfxUnlockSuccess);

                    // Unlock portal
                    portal.UnlockPortal();
                    inventoryManager.MarkKeyUsed(itemName);
                    inventoryManager.RemoveItem(itemName);
                    inventoryManager.currentSelectedKey = null;

                    inventoryManager.InventoryMenu.SetActive(false);
                    inventoryManager.inventoryActivated = false;
                    Time.timeScale = 1;
                    portal.LoadDestinationBossScene();
                    return; // only use on one portal
                }
            }

            // --- NO PORTAL FOUND OR WRONG KEY ---
            if (inventoryManager.audioSource && inventoryManager.sfxUnlockFail)
                inventoryManager.audioSource.PlayOneShot(inventoryManager.sfxUnlockFail);

        }

        else
        {
            inventoryManager.DeselectAllSlots();
            selectedShader.SetActive(true);
            thisItemSelected = true;

            ItemDescNameTxt.text = itemName;
            ItemDescTxt.text = itemDescription;
            itemDescriptionImage.sprite = itemSprite;
            if (itemDescriptionImage.sprite == null)
                itemDescriptionImage.sprite = emptySprite;
        }
    }

    public void EmptySlot()
    {
        itemImage.sprite = emptySprite;
        ItemDescNameTxt.text = "";
        ItemDescTxt.text = "";
        itemDescriptionImage.sprite = emptySprite;

        isFull = false;
        thisItemSelected = false;
    }



}
