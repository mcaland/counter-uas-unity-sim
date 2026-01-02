using System.Collections.Generic;
using System.Linq;
using ClothDynamics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance; // singleton

    private RenderTexture _cachedRenderTex;

    // menu options and important sim gameobjects
    public GameObject dronePreviewImage;
    public GameObject mainCamera;
    private Vector3 baseCameraPosition;
    private Quaternion baseCameraRotation;
    public GameObject dronePreviewCamera;
    public GameObject keybindText;
    public GameObject isPausedText;
    public GameObject menu;
    public GameObject instructionMenu;
    public GameObject sendFleetButton;

    // values for random timing of new drones after spawning one
    const float TIMER_MIN = 2f;
    const float TIMER_MAX = 5f;

    public GameObject dronePrefab;
    public GameObject droneFleetPrefab;

    // drones not being tracked by a fleet
    private List<GameObject> untrackedEnemyDrones = new List<GameObject>();

    // drones with fleets targeting them
    private Dictionary<GameObject, GameObject> enemyDroneToFleet = new Dictionary<GameObject, GameObject>();

    private float timerVariable = 0f; // keeps track of delay between drone objects

    public bool runningSim = false; // are we running the sim?

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;

        // save camera defaults to return to when we exit pause mode
        baseCameraPosition = mainCamera.transform.position;
        baseCameraRotation = mainCamera.transform.rotation;

        dronePreviewImage.SetActive(false);
        dronePreviewCamera.SetActive(false);
        
        // set up basic rendertexture to display a snapshot of the aerial target from afar
        _cachedRenderTex = new RenderTexture((int)dronePreviewImage.GetComponent<RectTransform>().rect.size.x, (int)dronePreviewImage.GetComponent<RectTransform>().rect.size.y, 1);
        dronePreviewCamera.GetComponent<Camera>().targetTexture = _cachedRenderTex;

        StopSim();
    }

    // Update is called once per frame
    void Update()
    {
        if (runningSim)
        {
            // display information about the sim
            if (instructionMenu.activeInHierarchy)
            {
                if (Input.GetMouseButtonDown(0)) // advance text on click
                {
                    if (instructionMenu.transform.GetChild(1).gameObject.activeInHierarchy)
                    {
                        instructionMenu.transform.GetChild(1).gameObject.SetActive(false);
                        instructionMenu.transform.GetChild(2).gameObject.SetActive(true);
                    }
                    else if (instructionMenu.transform.GetChild(2).gameObject.activeInHierarchy)
                    {
                        instructionMenu.SetActive(false);
                        instructionMenu.transform.GetChild(2).gameObject.SetActive(false);
                        sendFleetButton.SetActive(true);
                    }
                }
            }
            else
            {
                // drone spawning override key. only count down the timer if we have NO untracked drones (drones not yet being retrieved by a fleet)
                if ((timerVariable <= 0f || Input.GetKeyDown(KeyCode.Z)) && untrackedEnemyDrones.Count == 0)
                {
                    SpawnAerialTarget();

                    timerVariable = Random.Range(TIMER_MIN, TIMER_MAX);
                }
                else if (untrackedEnemyDrones.Count == 0)
                {
                    timerVariable -= Time.deltaTime;
                }

                // send fleet keybind
                if (Input.GetKeyDown(KeyCode.X) && untrackedEnemyDrones.Count > 0)
                {
                    dronePreviewImage.SetActive(false);
                    CreateFleet();
                }

                List<GameObject> dronesToRemove = new List<GameObject>();

                // check if each fleet has reached their drone, and progress their behavior if so
                foreach (GameObject drone in enemyDroneToFleet.Keys)
                {
                    DroneFleet fleet = enemyDroneToFleet[drone].GetComponent<DroneFleet>();
                    if (fleet.reached)
                    {
                        if (!fleet.reachedInitialPosition) // we've reached the target drone
                        {
                            fleet.reachedInitialPosition = true;
                            fleet.reached = false;
                            fleet.SetNavigation(drone.transform.position + new Vector3(0f, 3f, 0f));
                        }
                        else if (!fleet.attachedTargetDrone) // we've reached the inital position and moved upwards to make contact with the target
                        {
                            // attach drone to cloth
                            fleet.net.GetComponent<ClothObjectGPU>()._attachedObjects.Append(drone.transform);

                            fleet.attachedTargetDrone = true;
                            fleet.reached = false;

                            // initialize a random position to move to after retrieving the drone
                            bool moveToXExtent = Random.Range(0f, 1f) > 0.5f;
                            bool positiveExtent = Random.Range(0f, 1f) > 0.5f;

                            float primaryExtent = Random.Range(300f, 400f);
                            if (!positiveExtent)
                            {
                                primaryExtent = -primaryExtent;
                            }

                            float secondaryExtent = Random.Range(-200f, 200f);

                            if (moveToXExtent)
                            {
                                fleet.SetNavigation(new Vector3(primaryExtent, 100, secondaryExtent));
                            }
                            else
                            {
                                fleet.SetNavigation(new Vector3(secondaryExtent, 100, primaryExtent));
                            }
                        }
                        else // we've reached the "return point" and are finished
                        {
                            dronesToRemove.Add(drone);
                        }
                    }
                    else if (!fleet.reachedInitialPosition) // only track this if we havent even gotten to the drone yet
                    {
                        fleet.SetNavigation(drone.transform.position);
                    }
                }

                foreach (GameObject drone in dronesToRemove)
                {
                    // detach all connections and remove each drone and their fleet from the pairing list
                    GameObject fleetObj = enemyDroneToFleet[drone];
                    enemyDroneToFleet.Remove(drone);
                    System.Exception err;
                    fleetObj.GetComponent<DroneFleet>().net.GetComponent<ClothObjectGPU>()._attachedObjects.TryRemoveElementsInRange(0, 1, out err);
                    Destroy(drone);
                    Destroy(fleetObj);
                }

                // pause keybind
                if (Input.GetKeyDown(KeyCode.P))
                {
                    if (Time.timeScale != 0f)
                    {
                        isPausedText.SetActive(true);
                        Time.timeScale = 0f;
                    }
                    else
                    {
                        isPausedText.SetActive(false);
                        Time.timeScale = 1f;
                        // reset camera view to default
                        mainCamera.transform.position = baseCameraPosition;
                        mainCamera.transform.rotation = baseCameraRotation;
                    }
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    StopSim();
                }
            }
        }
    }

    void SpawnAerialTarget()
    {
        SpawnEnemyDrone();
    }

    void SpawnEnemyDrone()
    {
        // spawn randomly in the map and assign a random target position for the drone to reach
        // TODO: paths???
        GameObject drone = Instantiate(dronePrefab, new Vector3(Random.Range(-300, 300), 200f, Random.Range(-300, 300)), Quaternion.identity);
        drone.GetComponent<DroneController>().SetNavigation(new Vector3(Random.Range(-300, 300), Random.Range(50, 100), Random.Range(-300, 300)));
        untrackedEnemyDrones.Add(drone);
        ShowDronePreview(drone);
    }

    public void CreateFleet()
    {
        // don't do anything if there aren't any untracked drones
        if (untrackedEnemyDrones.Count > 0)
        {
            CreateFleet(untrackedEnemyDrones[0]); // default behavior
        }
    }

    void CreateFleet(GameObject target)
    {
        untrackedEnemyDrones.Remove(target);

        // spawn randomly in the map
        GameObject fleet = Instantiate(droneFleetPrefab, new Vector3(Random.Range(-300, 300), 200f, Random.Range(-300, 300)), Quaternion.identity);
        enemyDroneToFleet.Add(target, fleet);
        // make it so target can collide with cloth
        fleet.GetComponent<DroneFleet>().net.GetComponent<ClothObjectGPU>()._meshObjects.Append(target.transform);
    }

    void ShowDronePreview(GameObject drone)
    {
        if (drone == null || !drone.activeInHierarchy)
        {
            return;
        }

        dronePreviewImage.SetActive(true);
        dronePreviewCamera.SetActive(true);

        // position camera afar from drone to get a random viewpoint
        Vector3 distance = new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)); // randomize how far we are from the target
        distance = distance.normalized * Random.Range(30f, 75f);
        dronePreviewCamera.transform.position = drone.transform.position + distance;
        
        dronePreviewCamera.GetComponent<Camera>().transform.LookAt(drone.transform);

        RenderTexture currentActiveTex = RenderTexture.active;
        RenderTexture.active = _cachedRenderTex;

        dronePreviewCamera.GetComponent<Camera>().Render();

        // set the image on the preview to be of the camera rendering the drone from afar
        Texture2D tex = new Texture2D(_cachedRenderTex.width, _cachedRenderTex.height);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        RenderTexture.active = currentActiveTex;

        dronePreviewImage.GetComponent<Image>().sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        dronePreviewCamera.SetActive(false);
    }

    void ClearDrones()
    {
        foreach (GameObject drone in untrackedEnemyDrones)
        {
            Destroy(drone);
        }
        untrackedEnemyDrones.Clear();

        foreach (GameObject droneKey in enemyDroneToFleet.Keys)
        {
            Destroy(enemyDroneToFleet[droneKey]); // delete fleet
            Destroy(droneKey);
        }
        enemyDroneToFleet.Clear();
    }

    void SetGameState(bool runSim)
    {
        if (runningSim == runSim)
        {
            return;
        }

        Time.timeScale = 1f;
        isPausedText.SetActive(false);

        if (runSim)
        {
            // hide main menu, and show instructions
            runningSim = true;

            instructionMenu.SetActive(true);
            instructionMenu.transform.GetChild(1).gameObject.SetActive(true);
            instructionMenu.transform.GetChild(2).gameObject.SetActive(false);
            keybindText.SetActive(true);
            menu.SetActive(false);
        }
        else
        {
            // clear out drones, sim menu elements, and display main menu options
            runningSim = false;

            instructionMenu.SetActive(false);
            sendFleetButton.SetActive(false);
            keybindText.SetActive(false);
            menu.SetActive(true);
            dronePreviewImage.SetActive(false);
            dronePreviewCamera.SetActive(false);

            ClearDrones();
        }
    }

    public void StopSim()
    {
        SetGameState(false);
    }

    public void StartSim()
    {
        SetGameState(true);
    }

    public void CloseGame()
    {
        Application.Quit();
    }
}
