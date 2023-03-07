using System;
using Google.Protobuf.Collections;
using Online;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Server = protoBuff.Server;

public class ServerPicker : MonoBehaviour
{
    public string gameSceneName;

    [Header("Work values")] public GameObject elementPrefab;
    public Transform elementContainer;

    public Button joinButton;

    public GameObject serverChooser;
    public TMP_InputField serverAddr;
    public TMP_InputField sessionName;

    private RepeatedField<Server> servers;

    void Start()
    {
        serverAddr.SetTextWithoutNotify(Address.GetAddress());
        serverChooser.SetActive(false);
        if (gameSceneName == "")
        {
            throw new Exception("scene is required");
        }

        UpdateServer();
    }

    public void ServerValid()
    {
        var input = sessionName.text;

        foreach (var server in servers)
        {
            if (input == server.Id && server.Max == server.Online)
            {
                joinButton.interactable = false;
                return;
            }
        }

        joinButton.interactable = true;
    }

    public static bool gameLoaded;

    public void ConnectToSession()
    {
        var sessionID = sessionName.text;
        if (sessionID.Length < 3 || sessionID.Length > 25)
        {
            Debug.Log("bad session ID"); //todo change colour or something
            return;
        }
        joinButton.interactable = false;

        gameLoaded = false;

        var networkManager = FindObjectOfType<NetworkManager>();
        networkManager.OnDisconnect(Disconnected); // todo fix
        networkManager.Connect(sessionID)
            .Then(() =>
            {
                SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single); //todo loading bar
                
                SceneManager.sceneLoaded += (scene, _) =>
                {
                    if (gameLoaded || scene.name != gameSceneName) return;
                    gameLoaded = true;
                    
                    FindObjectOfType<StartGame>().StartMatch();
                    FindObjectOfType<PausedMenu>().SwitchMenuState();
                };
            })
            .Catch(e =>
            {
                Debug.LogError(e);
                Disconnected(); // return to selector scene if load failed
            })
            .Finally(() => joinButton.interactable = true);
    }   

    private void Disconnected()
    {
        Debug.LogWarning("disconnected");
        SceneManager.LoadScene("ServerSelector", LoadSceneMode.Single);
    }
    
    public void UpdateServer()
    {
        serverChooser.SetActive(false);
        Address.ChangeAddress(serverAddr.text);
        Connection.IsGameServer().Then(b =>
        {
            if (!b)
            {
                Debug.Log($"{Address.GetAddress()} is not a valid address");
                return;
            }

            serverChooser.SetActive(true);
            GetServers();
        }).Catch(Debug.Log);
    }

    private void GetServers()
    {
        foreach (Transform child in elementContainer.transform)
            Destroy(child.gameObject);

        Connection.List().Then(s =>
        {
            servers = s;
            if (servers.Count == 0)
            {
                UpdateSelection("game " + Random.Range(0, 2057));
                return;
            }

            for (var i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                if (i == 0)
                {
                    UpdateSelection(server.Id);
                }

                var o = Instantiate(elementPrefab, elementContainer);
                var script = o.GetComponent<multiScreenItem>();
                script.sessionName = server.Id;
                script.maxPlayers = (int)server.Max;
                script.playerAmount = (int)server.Online;
                script.selectFunction = UpdateSelection;
            }
        }).Catch(Debug.Log);
    }

    private void UpdateSelection(string s)
    {
        sessionName.SetTextWithoutNotify(s);
        Debug.Log("selected session: " + s);
    }
}