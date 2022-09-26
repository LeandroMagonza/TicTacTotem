using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController instance;
    public TMP_Text overheatedMessage;
    public Slider weaponTempSlider;
    public Slider healthSlider;
    public GameObject deathScreen;
    public TMP_Text deathText;

    public TMP_Text deathsText;
    public TMP_Text killsText;
    
    public GameObject leaderboard;
    public LeaderboardPlayer leaderboardPlayerDisplay;
    
    public GameObject endScreen;
    public TMP_Text timerText;
    
    void Awake(){
        instance = this;
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
