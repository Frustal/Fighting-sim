using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    public GameObject NPCcontroller;
    public Toggle toggle;
    public TMP_Text fpsText;
    public TMP_Text cpuText;

    public bool isOptimized;


    private float fps;
    private float cpuTime;
    private Recorder mainThreadRecorder;

    private void Start()
    {
        mainThreadRecorder = Recorder.Get("Main Thread");

        StartCoroutine(PrintStats());
    }

    private void Update()
    {
        isOptimized = toggle.isOn;
    }

    public void StartGame()
    {
        if (isOptimized) NPCcontroller.GetComponent<NPCManager>().enabled = true;
        else NPCcontroller.GetComponent<SimpleNPCManager>().enabled = true;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(0);
    }

    private IEnumerator PrintStats()
    {
        fps = 1f / Time.unscaledDeltaTime;

        if (mainThreadRecorder != null && mainThreadRecorder.isValid)
        {
            cpuTime = mainThreadRecorder.elapsedNanoseconds / 1_000_000f;
        }

        fpsText.text = $"FPS: {fps}";
        cpuText.text = $"CPU main: {cpuTime} ms";

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(PrintStats());
    }
}
