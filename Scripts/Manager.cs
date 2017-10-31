
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

public class Manager : Singleton<Manager>
{
    /// <summary>
    /// All the operations supported by the application.
    /// </summary>
    public enum OperationModes { Rotate, Up, Side, Assemble, Scale, Reset, Break };

    /// <summary>
    /// The current operation that is active..
    /// </summary>
    public static OperationModes opMode = OperationModes.Assemble;

    private GameObject splash;
    private List<int> randomList;
    private List<int> objList = new List<int>();
    private List<GameObject> messages = new List<GameObject>();
    private List<GameObject> removeList = new List<GameObject>();

    private int splashInterval = 4;
    private int positionInterval = 1;
    private int numberOfPrimitives = 14;
    private int messageInterval = 2;
    protected static int numberOfPrimitivesToRemove = 10;

    private string primitivePre = "/Primitives/h";
    private string alertsPath = "Alerts";
    private string splashName = "/mrimage";

    /// <summary>
    /// Position of the primitives.
    /// </summary>
    private Vector3[] positionArray = { new Vector3(-2.4f, 1.2f, 1.2f), new Vector3(-1.4f, 1.2f, 1.2f), new Vector3(-2.4f, 0.7f, 1.2f), new Vector3(-1.4f, 0.7f, 1.2f), new Vector3(-2.4f, 1f, 1f), new Vector3(-1.4f, 1f, 1f) };

    // Use this for initialization
    void Start()
    {
        removeSplash();
        randomizeObjects();
        Invoke("removeGameObjects", splashInterval);
    }

    // Update is called once per frame
    void Update()
    {
        // todo: collect usage meta-data.
    }

    /// <summary>
    /// 
    /// </summary>
    public static void resetScene()
    {
        Manager.numberOfPrimitivesToRemove = 8;
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Basic");
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Basic");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventData"></param>
    public void OnSpeechKeywordRecognized(SpeechKeywordRecognizedEventData eventData)
    {
        alertUserOnModeChange(eventData);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventData"></param>
    private void alertUserOnModeChange(SpeechKeywordRecognizedEventData eventData)
    {
        showMessage(eventData.RecognizedText);
        //TODO: Determine other ways to alert user; multi-modal?
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    private void showMessage(string message)
    {
        destroyMessages();
        GameObject temp = GameObject.Find(alertsPath);
        if (temp != null && temp.transform != null)
        {
            GameObject alert = (temp.transform.Find(message))? temp.transform.Find(message).gameObject : null;
            if (alert != null)
            {
                alert.SetActive(true);
                // bring the message right infront of the camera
                temp.transform.position = Camera.main.transform.position + Camera.main.transform.forward + new Vector3(0, -0.02f, 0);
                messages.Add(alert);
                Invoke("destroyMessages", messageInterval);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void destroyMessages()
    {
        foreach (GameObject obj in messages)
        {
            obj.SetActive(false);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void removeSplash()
    {
        splash = GameObject.Find(splashName);
        removeList.Add(splash);
    }

    /// <summary>
    /// 
    /// </summary>
    private void removeGameObjects()
    {
        foreach (GameObject obj in removeList)
        {
            obj.GetComponentInChildren<Renderer>().enabled = false;
        }
        Invoke("positionObjects", positionInterval);
    }

    /// <summary>
    /// 
    /// </summary>
    private void positionObjects()
    {
        int k = 0;
        foreach (int i in objList)
        {
            GameObject tempObj = GameObject.Find(primitivePre + i.ToString());
            if (tempObj != null)
            {
                tempObj.transform.localPosition = positionArray[k];
                ++k;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void randomizeObjects()
    {
        randomList = getRandomIndexes();
        for (int i = 1; i <= numberOfPrimitives; i++)
        {
            if (randomList.Contains(i))
            {
                GameObject tempObj = GameObject.Find(primitivePre + i.ToString());
                if (tempObj != null)
                {
                    removeList.Add(tempObj);
                    removeList.AddRange(getAllChildObjectsRecursively(tempObj));
                }
            }
            else
            {
                objList.Add(i);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    private List<GameObject> getAllChildObjectsRecursively (GameObject obj)
    {
        List<GameObject> returnList = new List<GameObject>();
        if (obj != null)
        {
            foreach (Transform child in obj.transform)
            {
                returnList.Add(child.gameObject);
                returnList.AddRange(getAllChildObjectsRecursively(child.gameObject));
            }
        }
        return returnList;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private List<int> getRandomIndexes()
    {
        List<int> tempList = new List<int>();
        System.Random randomizer = new System.Random();
        // keep generating random numbers until you have the require numberd (unique)
        while (tempList.Count < numberOfPrimitivesToRemove)
        {
            int randomNumber = randomizer.Next(1, numberOfPrimitives + 1);
            if (!tempList.Contains(randomNumber))
            {
                tempList.Add(randomNumber);
            }
        }
        return tempList;
    }
}
