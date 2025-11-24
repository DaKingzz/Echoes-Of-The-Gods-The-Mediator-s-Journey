using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryManager : MonoBehaviour
{
    public GameObject InventoryMenu;
    private bool inventoryActivated;
    public ItemSlot[] itemSlot;

    public static InventoryManager Instance;
    public GameObject InventoryCanvas;

    private void Awake()
    {
        // Singleton pattern: ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject); // Destroy duplicates if another scene has one
        }
    }

    public void Start()
    {
        InventoryMenu.SetActive(false);
        inventoryActivated = false;
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

    public void UseItem(string itemName)
    {
        if (itemName == "Time Key")
            Debug.Log("Enter Time Boss Zone");
        else if (itemName == "Gravity Key")
            Debug.Log("Enter Gravity Boss Zone");
        else if(itemName == "Light Key")
            Debug.Log("Enter Light Boss Zone");
        else if(itemName == "Evil Key")
            Debug.Log("Enter Evil Boss Zone");
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
    }
}
