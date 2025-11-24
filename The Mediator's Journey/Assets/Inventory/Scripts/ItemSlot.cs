using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 
using UnityEngine.UI; 

public class ItemSlot : MonoBehaviour
{
    //Item Data 
    public string itemName;
    public Sprite itemSprite;
    public bool isFull;

    //Item Slot
    [SerializeField] private Image itemImage; 

    public void AddItem (string itemName, Sprite itemSprite)
    {
        this.itemName = itemName; 
        this.itemSprite = itemSprite;
        isFull = true;

        itemImage.sprite = itemSprite; 


    }
}
