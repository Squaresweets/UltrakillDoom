using ManagedDoom;
using ManagedDoom.Unity;
using MyFirstPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xabblll.DoomInInspector;
using EventType = UnityEngine.EventType;

public class Doom : MonoBehaviour
{
    public float tickTime;
    public string wadPath;
    public string pwadPath;
    public string sfPath;
    public Texture2D screen;

    private UnityDoom doom;
    private string cpuLog;
    private bool isLoaded;
    private List<KeyCode> pressedKeys = new List<KeyCode>();
    private Coroutine doomCoroutine;

    private NewMovement nm;
    private GameObject doomUI;
    private AudioSource shopMusic;

    public void SetUp()
    {
        shopMusic = transform.Find("Music").GetComponent<AudioSource>();
        nm = NewMovement.Instance;
        GameObject smile = transform.Find("Canvas/Background/Icon").gameObject;
        smile.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(SmileButtonPressed);
        smile.AddComponent<ShopButton>(); //Gives an error but i don't care
        smile.GetComponent<Image>().raycastTarget = true;
    }
    private void SmileButtonPressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        if (doomUI != null && doomUI.activeInHierarchy) //If we press it again, cancel out
        {
            doomUI.SetActive(false);
            nm.ReactivateMovement();
            return;
        }
        StartDoom(); 
        if (doomUI == null)
        {
            //We create the new panel by copying the tip of the day panel and shifting it aorund
            Transform mainPanel = transform.Find("Canvas/Background/Main Panel");
            GameObject tipOfTheDay = mainPanel.Find("Tip of the Day").gameObject;
            doomUI = GameObject.Instantiate(tipOfTheDay, mainPanel);
            doomUI.transform.SetAsLastSibling();

            doomUI.GetComponent<RectTransform>().offsetMin = Vector3.zero;
            doomUI.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text = "Doom";
            doomUI.transform.Find("Icon").GetComponent<Image>().sprite = new UKAsset<UnityEngine.Sprite>("Assets/Textures/UI/smileOS 2 icon enemy.png").Asset;
            doomUI.GetComponent<Image>().raycastTarget = true; //Block pressing buttons behind
            GameObject screenGO = doomUI.transform.Find("Panel/Text Inset").gameObject;
            GameObject.Destroy(screenGO.transform.GetChild(0).gameObject); //Destroy the text
            GameObject.DestroyImmediate(screenGO.GetComponent<Image>());

            RectTransform rt = screenGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            Vector2 parentSize = rt.parent.GetComponent<RectTransform>().rect.size; //Make it fit the size of the parent (while roating it 90 degrees??)
            rt.sizeDelta = new Vector2(parentSize.y, parentSize.x);
            rt.localRotation = Quaternion.Euler(0f, 0f, -90f);

            screenGO.AddComponent<RawImage>().texture = screen;
        }
        doomUI.SetActive(true);
        doom.SetVolume(15);
        shopMusic.gameObject.SetActive(false);
        nm.DeactivateMovement(); //Im just chucking these in at this point
    }
    private bool interacting; //Only used for input management
    public void Update()
    {
        if (doomUI == null) return;

        if (!doomUI.activeInHierarchy)
        {
            doomUI.SetActive(false); //So we have to press the button to turn it on again next time
            shopMusic.gameObject.SetActive(true);
            doom.SetVolume(0);
        }

        if (!interacting)
        {
            if (FistControl.Instance.shopping && doom != null && doomUI.activeInHierarchy)
            {
                interacting = true;
                nm.DeactivateMovement();
            }
            return;
        }

        if (!FistControl.Instance.shopping || doom == null)
        {
            interacting = false;
            nm.ReactivateMovement();
            ClearKeys();
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(key))
            {
                KeyPressed(key);
            }
            if (Input.GetKeyUp(key))
            {
                KeyReleased(key);
            }
        }
    }
    public void StartDoom()
    {
        if(isLoaded) return;
        
        try
        {
            var args = new CommandLineArgs(new string[]
            {
                "iwad", wadPath, pwadPath
            });
            doom = new UnityDoom(args, sfPath);
            doom.OnLoad();
            screen = doom.GetVideoTexture();

            isLoaded = true;
        }
        catch (System.Exception e)
        {
            throw e;
        }
        doomCoroutine = StartCoroutine(DoomCPU());
    }
    private IEnumerator DoomCPU()
    {
        long frequency = Stopwatch.Frequency;
        var sw = Stopwatch.StartNew();
        long frameTime;

        while (true)
        {
            if (isLoaded)
            {
                doom.UpdateKeys(pressedKeys); 
                if (doom.OnUpdate() == UpdateResult.Completed)
                {
                    StopDoom();
                    yield break;
                }

                doom.OnRender();

                var desiredTickTime = (long)Math.Floor(frequency * tickTime);
                frameTime = sw.ElapsedTicks;
                var gamePerformance = frameTime;
                yield return null;

                // More or less reliable way to wait exact time
                frameTime = sw.ElapsedTicks;
                long threadSleepTime = 0;
                var residentSleeper = new TimeSpan(ticks:1000);
                while (frameTime + 1000 < desiredTickTime)
                {
                    Thread.Sleep(residentSleeper);
                    frameTime = sw.ElapsedTicks;
                    threadSleepTime += 1000;
                }
                frameTime = sw.ElapsedTicks;
                int waiter = 0;
                while (frameTime < desiredTickTime)
                {
                    waiter++;
                    frameTime = sw.ElapsedTicks;
                }

                cpuLog =
                    $"CPU:{(float)sw.ElapsedTicks / frequency * 1000:00.00000} ms |" +
                    $"ThreadSleep:{threadSleepTime:00000000000} ticks |" +
                    $"FineWait:{waiter:000000}" +
                    $"\nDoom Performance:{(float)gamePerformance / frequency * 1000:00.000ms}";
                sw.Restart();
            }
        }
    }
    private void StopDoom()
    {
        isLoaded = false;
        
        if (doomCoroutine!= null)
            StopCoroutine(doomCoroutine);
        
        if (doom != null)
        {
            doom.OnClose();
            doom.Dispose();
        }

        screen = null;
        pressedKeys.Clear();

        interacting = false;

        GC.Collect();
    }

    private void OnDestroy()
    {
        StopDoom();
    }

    public void KeyPressed(KeyCode key)
    {
        if (doom == null) return;
        if(!pressedKeys.Contains(key)) pressedKeys.Add(key);
        doom.KeyDown(key);
    }
    public void KeyReleased(KeyCode key)
    {
        if (doom == null) return;
        if(pressedKeys.Contains(key)) pressedKeys.Remove(key);
        doom.KeyUp(key);
    }
    public void ClearKeys()
    {
        if (doom == null) return;
        foreach (KeyCode k in pressedKeys) doom.KeyUp(k);
        pressedKeys = new List<KeyCode>();
    }
}
