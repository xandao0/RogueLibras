using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    [SerializeField] private int fps = 60;
    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
    }
}
