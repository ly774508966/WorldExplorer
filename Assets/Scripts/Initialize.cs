﻿using UnityEngine;
using Assets.Scripts;
using MapzenGo.Models;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using HoloToolkit.Unity;
using System;
#if (NETFX_CORE)
using Assets.MapzenGo.Models.Enums;
using Assets.Scripts.Utils;
#endif

public class Initialize : MonoBehaviour
{


    // Use this for initialization
    private GameObject _cursorFab;
    private GameObject cursor;
    private GameObject HoloManagers;
    private AppState appState;
    private GameObject Hud;
    private Dictionary<string, string> audioCommands;
    private Font font;
    private AudioClip fingerPressedSound;
    void Awake()
    {
        var threadDispatcher = gameObject.AddComponent<UnityMainThreadDispatcher>();
        _cursorFab = Resources.Load("Prefabs\\Input\\Cursor") as GameObject;
        appState = AppState.Instance;
        appState.LoadConfig();
        Hud = GameObject.Find("HUDCanvas");
        audioCommands = new Dictionary<string, string>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        fingerPressedSound = (AudioClip)Resources.Load("FingerPressed");
    }

    void InitHud()
    {
        HoloManagers = new GameObject("HoloManagers");
        var Handsmanager = HoloManagers.AddComponent<Assets.Scripts.Utils.HandsManager>();
        Handsmanager.FingerPressedSound = fingerPressedSound;
        
        GameObject textO = new GameObject("Commands-Help");
        textO.transform.SetParent(Hud.transform);
        Text info = textO.AddComponent<Text>();


        RectTransform rt = textO.GetComponent(typeof(RectTransform)) as RectTransform;
       
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1f, 1f);
        rt.position = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(0, 0);

        info.font = font;
        info.resizeTextForBestFit = true;
        info.verticalOverflow = VerticalWrapMode.Truncate;

        StringBuilder s = new StringBuilder();
        s.AppendLine("Commands:");
        int h = 1;
        foreach (var item in audioCommands)
        {
            s.AppendLine(item.Key + ": " + item.Value);
            h++;
        }
        rt.sizeDelta = new Vector2(350, (h + 1) * 25);
        info.text = s.ToString();

    }

    void Start()
    {
        appState.Camera = gameObject;
        appState.Speech = new Assets.Scripts.SpeechManager();

        appState.AddTerrain();
        InitSpeech();
        InitViews();
        InitHud();
        InitMqtt();

        cursor = Instantiate(_cursorFab, new Vector3(0, 0, -1), transform.rotation);
        cursor.name = "Cursor";

        appState.Speech.Init();
    }

    void InitSpeech()
    {
        audioCommands.Add("Hide Commands", " Hides the voice commands");
        appState.Speech.Keywords.Add("Hide Commands", () =>
        {
            Hud.SetActive(false);
            // appState.TileManager.UpdateTiles();
        });
        audioCommands.Add("Show Commands", " Displays the voice commands");
        appState.Speech.Keywords.Add("Show Commands", () =>
        {
            Hud.SetActive(true);
            // appState.TileManager.UpdateTiles();
        });
        audioCommands.Add("Zoom Out", " Zoom out");
        appState.Speech.Keywords.Add("Zoom out", () =>
        {
            appState.Center = new Vector3(appState.Center.x, appState.Center.y, appState.Center.z + 1);
            // appState.TileManager.UpdateTiles();
        });
        audioCommands.Add("Center table", " Places the table at your current position");
        appState.Speech.Keywords.Add("Center Table", () =>
        {
            appState.Table.transform.position = new Vector3(gameObject.transform.position.x, 0.7f, gameObject.transform.position.z);
            //Center = new Vector3(Center.x, Center.y, Center.z + 1);
        });
    }

    void InitViews()
    {

        appState.Config.Views.ForEach(v =>
        {
            audioCommands.Add("Switch to " + v.Name, " displays the view");

            appState.Speech.Keywords.Add("Switch to " + v.Name, () =>
            {
                appState.Config.ActiveView = v;
                appState.ResetMap();
            });
        });
    }

//#if (NETFX_CORE)
    protected void InitMqtt()
    {
#if (NETFX_CORE)
        var client = new uPLibrary.Networking.M2Mqtt.MqttClient(appState.Config.MqttServer, int.Parse(appState.Config.MqttPort), false);
#else
        var client = new uPLibrary.Networking.M2Mqtt.MqttClient(appState.Config.MqttServer, int.Parse(appState.Config.MqttPort));
#endif
        try
        {
            Debug.Log("Connecting to MQTT");
            client.Connect(Guid.NewGuid().ToString());
        }
        catch
        {
            Debug.LogError("Error connecting to mqtt");
        }
        if (client.IsConnected)
        {
            // register to message received 
            client.MqttMsgPublishReceived += (sender, e) =>
            {                
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    var msg = Encoding.UTF8.GetString(e.Message);
                    switch (e.Topic)
                    {
                        case "view":
                            SetView(msg);
                            break;
                    }
                    GameObject _3dText = GameObject.Find("tbTemp");
                    _3dText.GetComponent<TextMesh>().text = msg;
                });
            };

            //// subscribe to the topic "/home/temperature" with QoS 2 
            client.Subscribe(new string[] { "view" }, new byte[] { uPLibrary.Networking.M2Mqtt.Messages.MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }
    }

    protected void SetView(string msg)
    {
        var view = new JSONObject(msg);
        var iv = appState.Config.ActiveView;
        iv.Lat = GetFloat(view, "Lat");
        iv.Lon = GetFloat(view, "Lon");
        iv.Zoom = GetInt(view, "Zoom");
        if (appState.TileManager)
        {
            appState.TileManager.Latitude = iv.Lat;
            appState.TileManager.Longitude = iv.Lon;
            appState.TileManager.Zoom = iv.Zoom;
            appState.ResetMap();
        }
    }
    //#endif

    private static int GetInt(JSONObject json, string key, int defaultValue = 0)
    {
        if (json && json.HasField(key) && json[key].IsNumber) return (int)json[key].n;
        return defaultValue;
    }

    private static float GetFloat(JSONObject json, string key, float defaultValue = 0f)
    {
        if (json && json.HasField(key) && json[key].IsNumber) return (float)json[key].n;
        return defaultValue;
    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.LeftArrow)) appState.Center = new Vector3(-1, 0, 0);
        //if (Input.GetKeyDown(KeyCode.RightArrow)) appState.Center = new Vector3(1, 0, 0);
        //if (Input.GetKeyDown(KeyCode.DownArrow)) appState.Center = new Vector3(0, 0, -1);
        //if (Input.GetKeyDown(KeyCode.UpArrow)) appState.Center = new Vector3(0, 0, 1);
        //if (Input.GetKeyDown(KeyCode.I)) appState.Center = new Vector3(0, 1, 0);
        //if (Input.GetKeyDown(KeyCode.O)) appState.Center = new Vector3(0, -1, 0);
        if (Input.GetKeyDown(KeyCode.C))
        {
            appState.ClearCache();
        }
        for (var i = 0; i < Mathf.Min(8, appState.Config.Views.Count); i++)
        {
            if (!Input.GetKeyDown(string.Format("{0}", i + 1))) continue;
            appState.Config.ActiveView = appState.Config.Views[i];
            appState.ResetMap();
            return;
        }
    }
}
