using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public int totalDiamonds = 4;
    public Image diamondCountImage;
    public Sprite[] digitSprites;
    public Door door;
    private int diamondCount;
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        RefreshDiamondUI();
    }
    public void AddDiamond()
    {
        diamondCount++;
        RefreshDiamondUI();

        if(diamondCount >= totalDiamonds)
        {
            OpenDoor();
        }
    }
    private void RefreshDiamondUI()
    {
        if(diamondCountImage == null || digitSprites == null)
        {
            return;
        }

        if(diamondCount < digitSprites.Length)
        {
            diamondCountImage.sprite = digitSprites[diamondCount];
        }
    }
    private void OpenDoor()
    {
        door.Unlock();       
    }
    public void Respawn()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}