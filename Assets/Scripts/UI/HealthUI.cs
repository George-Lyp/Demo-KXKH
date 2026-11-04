using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class HealthUI : MonoBehaviour
{
    [Header("数据")]
    public Character character;

    [Header("心")]
    public Image[] hearts;
    public Animator[] heartAnimators;

    [Header("空心颜色")]
    public Color emptyColor = new Color(1f, 1f, 1f, 0.25f);

    private float lastHealth;

    private void Start()
    {
        lastHealth = character.currentHealth;

        for(int i = 0; i < hearts.Length; i++)
        {
            hearts[i].color = IsAlive(i) ? Color.white : emptyColor;
        }
    }

    private void Update()
    {
        for(int i = 0; i < hearts.Length; i++)
        {
            bool alive = IsAlive(i);
            bool wasAlive = lastHealth > i * PerHeart();
            if(wasAlive == alive)
            {
                continue;
            }

            if(alive)
            {
                hearts[i].color = Color.white;
            }
            else
            {
                StartCoroutine(Shatter(i));
            }
        }

        lastHealth = character.currentHealth;
    }
    private bool IsAlive(int index)
    {
        return character.currentHealth > index * PerHeart();
    }
    private int PerHeart()
    {
        return Mathf.CeilToInt(character.maxHealth / hearts.Length);
    }
    private IEnumerator Shatter(int index)
    {
        if(heartAnimators[index] != null) heartAnimators[index].SetTrigger("hit");
        yield return new WaitForSeconds(0.2f);
        if(!IsAlive(index))
        {
            hearts[index].color = emptyColor;
        }
    }
}