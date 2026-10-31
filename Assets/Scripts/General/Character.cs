using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("基本属性")]
    public float maxHealth;
    public float currentHealth;

    [Header("受伤无敌")]
    public float invulnerableDuration;
    
    public float invulnerableCount; 
    public bool invulnerable;

    private void Update()
    {
        if(invulnerable)
        {
            invulnerableCount-=Time.deltaTime;
            if(invulnerableCount<=0)
            {
                invulnerable=false;
            }
        }
    }
    private void Start()
    {
        currentHealth=maxHealth;
    }
    public void TakeDamage(Attack attacker)
    {
        if(invulnerable) return;
        if(currentHealth-attacker.damage>0)
        {
            currentHealth-=attacker.damage;
            TriggerInvulnerable();
        }
        else
        {
            currentHealth=0;
        }
    }
    private void TriggerInvulnerable()
    {
        if(!invulnerable)
        {
            invulnerable=true;
            invulnerableCount=invulnerableDuration;
        }
    }
}

