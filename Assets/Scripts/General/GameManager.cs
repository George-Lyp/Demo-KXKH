using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public int totalDiamonds = 4;
    public Image diamondCountImage;
    public Sprite[] digitSprites;
    public Door door;
    public GameObject winPanel;
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
    public void Win()
    {
        StartCoroutine(ShowWinPanel());
    }
    private IEnumerator ShowWinPanel()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        if(winPanel != null)
        {
            winPanel.SetActive(true);
        }
        Time.timeScale = 0f;
    }
    public void Restart()
    {
        Time.timeScale = 1f;
        PlayerController.dashUnlocked = false;
        SceneManager.LoadScene("1");
    }
}