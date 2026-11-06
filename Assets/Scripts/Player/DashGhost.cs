using UnityEngine;
public class DashGhost : MonoBehaviour
{
    public float life=0.18f;

    private SpriteRenderer sr;
    private float lifeCounter;
    private float startAlpha;

    private void Awake()
    {
        sr=GetComponent<SpriteRenderer>();
        startAlpha=sr.color.a;
    }

    private void Start()
    {
        lifeCounter=life;
    }

    private void Update()
    {
        lifeCounter-=Time.deltaTime;
        if(lifeCounter<=0)
        {
            Destroy(gameObject);
            return;
        }

        Color c=sr.color;
        c.a=startAlpha*(lifeCounter/life);
        sr.color=c;
    }
}