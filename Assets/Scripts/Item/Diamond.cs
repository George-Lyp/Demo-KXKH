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
        // 只认玩家，且只收一次
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
        anim.SetTrigger("hit");
    }

    // 由 Hit 动画最后一帧的 Animation Event 调用
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}