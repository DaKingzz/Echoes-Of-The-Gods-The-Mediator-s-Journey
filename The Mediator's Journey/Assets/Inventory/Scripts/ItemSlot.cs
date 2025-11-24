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
        inventoryManager = GameObject.Find("InventoryCanvas").GetComponent<InventoryManager>();
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
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClick();
        }
    }

    public void OnLeftClick()
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

    public void OnRightClick()
    {

    }
}
