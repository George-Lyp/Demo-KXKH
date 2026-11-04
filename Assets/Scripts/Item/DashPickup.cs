using UnityEngine;

public class DashPickup : MonoBehaviour
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

        PlayerController player = other.GetComponent<PlayerController>();
        if(player != null)
        {
            player.UnlockDash();
        }
        anim.SetTrigger("hit");
    }
    public void DestroySelf()
    {
        Destroy(this.gameObject);
    }
}