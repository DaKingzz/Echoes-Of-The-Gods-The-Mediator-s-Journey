using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] public string itemName;

    [SerializeField] public Sprite sprite;

    [TextArea] [SerializeField] public string itemDescription;

    private InventoryManager inventoryManager;

    // Start is called before the first frame update
    void Start()
    {
        inventoryManager = InventoryManager.Instance;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            inventoryManager.AddItem(itemName, sprite, itemDescription); 
            Destroy(gameObject);
        }
    }

}
