using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public class Door : MonoBehaviour
{
    [HideInInspector] public Animator anim;
    [Header("场景")]
    public string nextSceneName="2";
    [Header("状态")]
    public bool unlocked;
    private PlayerInputControl inputControl;
    private bool entering;
    private bool playerInside;

    private void Awake()
    {
        anim=GetComponent<Animator>();

        inputControl=new PlayerInputControl();
        inputControl.Gameplay.Interact.started+=OnInteract;
    }

    private void OnEnable()
    {
        if(inputControl!=null)
        {
            inputControl.Gameplay.Enable();
        }
    }

    private void OnDisable()
    {
        if(inputControl!=null)
        {
            inputControl.Gameplay.Disable();
        }
    }

    private void OnDestroy()
    {
        if(inputControl==null)
        {
            return;
        }

        inputControl.Gameplay.Interact.started-=OnInteract;
        inputControl.Dispose();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
        {
            return;
        }
        playerInside=true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
        {
            return;
        }
        playerInside=false;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if(!playerInside)
        {
            return;
        }
        if(!unlocked)
        {
            return;
        }

        if(entering)
        {
            return;
        }
        entering=true;
        anim.SetTrigger("open");
    }
    private void OnDoorClosed()
    {
        SceneManager.LoadScene(nextSceneName);
    }
    public void Unlock()
    {
        unlocked=true;
    }
}