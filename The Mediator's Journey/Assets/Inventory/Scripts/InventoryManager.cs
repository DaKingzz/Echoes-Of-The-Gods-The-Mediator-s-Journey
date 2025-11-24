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
            InventoryMenu.SetActive(false);
            inventoryActivated = false;
        }

        else if (Input.GetKeyDown(KeyCode.I) && !inventoryActivated)
        {
            InventoryMenu.SetActive(true);
            inventoryActivated = true;
        }
    }
}
