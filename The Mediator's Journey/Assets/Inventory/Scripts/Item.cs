using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] public string itemName;
    [SerializeField] public Sprite sprite;
    [TextArea] [SerializeField] public string itemDescription;

    private InventoryManager inventoryManager;

    void Start()
    {
        inventoryManager = InventoryManager.Instance;

        // Prevent key from respawning if already picked up
        if (inventoryManager.HasPickedUp(itemName))
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            inventoryManager.AddItem(itemName, sprite, itemDescription);
            inventoryManager.MarkItemPickedUp(itemName);
            Destroy(gameObject);
        }
    }

}
