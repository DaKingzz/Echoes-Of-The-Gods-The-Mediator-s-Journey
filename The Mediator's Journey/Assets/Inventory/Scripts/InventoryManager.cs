using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public GameObject InventoryMenu;
    private bool inventoryActivated;

    void Start()
    {
        
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

    public void AddItem(string itemName, Sprite itemSprite)
    {
        Debug.Log("itemName = " + itemName + " sprite = " + itemSprite); 
    }
}
