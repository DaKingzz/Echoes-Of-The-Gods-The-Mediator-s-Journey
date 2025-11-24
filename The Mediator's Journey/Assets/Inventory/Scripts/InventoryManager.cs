using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryManager : MonoBehaviour
{
    public GameObject InventoryMenu;
    public bool inventoryActivated;
    public ItemSlot[] itemSlot;

    public static InventoryManager Instance;

    private HashSet<string> usedKeys = new HashSet<string>();
    public string currentSelectedKey; 



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
}
