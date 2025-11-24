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

    //Item Slot
    [SerializeField] private Image itemImage; 
    
    public GameObject selectedShader;
    public bool thisItemSelected;

    private InventoryManager inventoryManager;

    public void Start()
    {
        inventoryManager = GameObject.Find("InventoryCanvas").GetComponent<InventoryManager>();
    }

    public void AddItem (string itemName, Sprite itemSprite)
    {
        this.itemName = itemName; 
        this.itemSprite = itemSprite;
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
    }

    public void OnRightClick()
    {

    }
}
