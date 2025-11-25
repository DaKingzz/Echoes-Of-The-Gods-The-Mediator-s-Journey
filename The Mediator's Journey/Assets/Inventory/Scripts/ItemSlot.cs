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
            DeselectItem(); 
            return;
        }

        inventoryManager.DeselectAllSlots();
        selectedShader.SetActive(true);
        thisItemSelected = true;

        inventoryManager.currentSelectedKey = this;

        ItemDescNameTxt.text = itemName;
        ItemDescTxt.text = itemDescription;
        itemDescriptionImage.sprite = itemSprite;
        if (itemDescriptionImage.sprite == null)
            itemDescriptionImage.sprite = emptySprite;
     
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

    private void DeselectItem()
    {
        selectedShader.SetActive(false);
        thisItemSelected = false;

        // Clear description UI
        ItemDescNameTxt.text = "";
        ItemDescTxt.text = "";
        itemDescriptionImage.sprite = emptySprite;

        // Tell inventory manager nothing is selected
        if (inventoryManager.currentSelectedKey == this)
            inventoryManager.currentSelectedKey = null;
    }


}
