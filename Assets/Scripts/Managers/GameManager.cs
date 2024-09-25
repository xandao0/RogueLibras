using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameStates
{
    COMBAT,
    ENDMATCH
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public GameStates currentState;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        
        DontDestroyOnLoad(this.gameObject);
    }

    public static void ChangeState(GameStates newState)
    {
        if(instance.currentState == newState)
            return;

        instance.currentState = newState;

        switch (newState)
        {
            case GameStates.COMBAT:
                break;
            case GameStates.ENDMATCH:
                break;
        }
    }
}
