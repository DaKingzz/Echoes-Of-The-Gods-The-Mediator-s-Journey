using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private RectTransform healthBarFill;
    [SerializeField] private float healthWidth, healthHeight;
    
    void Update()
    {
        float newWidth = (player.CurrentHealth/ player.MaximumHealth) * healthWidth;

        healthBarFill.sizeDelta = new Vector2(newWidth, healthHeight);
    }
    
    
}
