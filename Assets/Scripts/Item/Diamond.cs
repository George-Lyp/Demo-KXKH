using UnityEngine;

public class Diamond : MonoBehaviour
{
    public Animator anim;

    private bool collected;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(collected)
        {
            return;
        }
        if(!other.CompareTag("Player"))
        {
            return;
        }
        collected = true;
        GameManager.Instance.AddDiamond();
        Character character = other.GetComponent<Character>();
        character.HealToMax();
        anim.SetTrigger("hit");
    }
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}