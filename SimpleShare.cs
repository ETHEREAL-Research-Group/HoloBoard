using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using Unity.Netcode;

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;

public class TriangleAxes
{
    public Vector3 xAxis;
    public Vector3 yAxis;
    public Vector3 zAxis;

    public Vector3 originPos;
    public Quaternion originRot;

    public TriangleAxes()
    {
        xAxis = Vector3.zero;
        yAxis = Vector3.zero;
        zAxis = Vector3.zero;

        originPos = Vector3.zero;
        originRot = Quaternion.identity;
    }
}

public class SimpleShare : MonoBehaviour
{
    public NetworkManager netManager;

    public TriangleAxes myAxes;
    public TriangleAxes hostAxes;

    #region Spatial Anchors

    SpatialAnchorManager spatialAnchorManager;
    public List<GameObject> anchorGameObjects;
    public List<string> anchorIds;

    public bool wasLoaded;

    private void CreateAnchorGameObject(AnchorLocatedEventArgs args)
    {
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            CloudSpatialAnchor cloudSpatialAnchor = args.Anchor;

            GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anchorGameObject.transform.localScale = Vector3.one * 0.05f;
            anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Standard");
            anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.blue;
            anchorGameObject.tag = "AnchorObject";

            anchorGameObject.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);

            anchorGameObjects.Add(anchorGameObject);

            Debug.Log("anchorGameObjects.Count = " + anchorGameObjects.Count.ToString());

            anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.green;

            // Create the synchronization triangle for the client.
            if (anchorGameObjects.Count > 2)
            {
                CreateTriangle();
            }
        });
    }

    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.Log("AnchorLocated() was called.");

        if (args.Status == LocateAnchorStatus.Located)
        {
            Debug.Log("Anchor was successfully located.");

            CreateAnchorGameObject(args);
        }
        else if (args.Status == LocateAnchorStatus.AlreadyTracked)
        {
            Debug.Log("Anchor was rediscovered successfully.");

            CreateAnchorGameObject(args);
        }
        else
        {
            Debug.Log("Anchor was not successfully located: " + args.Status);
        }
    }

    private async Task CreateAnchor(Vector3 position, Quaternion rotation)
    {
        Debug.Log("CreateAnchor() was called.");

        // Create a local game object to represent the spatial anchor.
        GameObject anchorGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchorGameObject.transform.localScale = Vector3.one * 0.05f;
        anchorGameObject.GetComponent<MeshRenderer>().material.shader = Shader.Find("Standard");
        anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.blue;
        anchorGameObject.transform.position = position;
        anchorGameObject.transform.rotation = rotation;
        anchorGameObject.tag = "AnchorObject";

        // Attach the spatial anchor to the game object.
        CloudNativeAnchor cloudNativeAnchor = anchorGameObject.AddComponent<CloudNativeAnchor>();
        await cloudNativeAnchor.NativeToCloud();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
        cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(10);

        // Collect spatial data for the anchor.
        while (!spatialAnchorManager.IsReadyForCreate)
        {
            float createProgress = spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"Move your device to capture more environment data: {createProgress:0%}");
        }

        // Create the anchor in the cloud.
        try
        {
            await spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

            bool saveSucceeded = cloudSpatialAnchor != null;
            if (!saveSucceeded)
            {
                Debug.Log("Failed to save spatial anchor to the cloud, but no exception was thrown.");
                return;
            }

            Debug.Log($"Saved anchor to cloud with ID: {cloudSpatialAnchor.Identifier}");

            // Keep track of the anchor's game object.
            anchorGameObjects.Add(anchorGameObject);

            // Keep track of the anchor's ID.
            anchorIds.Add(cloudSpatialAnchor.Identifier);

            // Change anchor game object to green once it's saved successfully.
            anchorGameObject.GetComponent<MeshRenderer>().material.color = Color.green;
        }
        catch (Exception exception)
        {
            Debug.Log("Failed to save anchor to cloud: " + exception.ToString());
            Debug.LogException(exception);
        }

        // Create the synchronization triangles for the host.
        if (anchorGameObjects.Count > 2)
        {
            CreateTriangle();
        }
    }

    public async void LocateAnchors()
    {
        Debug.Log("LocateAnchors() was called.");

        // Ensure the anchor IDs exist.
        if (anchorIds.Count > 2)
        {
            // Start an Azure Spatial Anchors session if it doesn't exist.
            if (!spatialAnchorManager.IsSessionStarted)
            {
                await spatialAnchorManager.StartSessionAsync();
            }

            // Create criteria for anchor location.
            AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
            anchorLocateCriteria.Identifiers = anchorIds.ToArray();

            if (spatialAnchorManager.Session.GetActiveWatchers().Count > 0)
            {
                Debug.Log("Spatial anchor watcher already exists.");
            }

            // Create watch to locate anchors with given criteria.
            spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
        }
    }

    public void DeleteAnchor(GameObject anchorGameObject)
    {
        // Get reference to local spatial anchor.
        CloudNativeAnchor cloudNativeAnchor = anchorGameObject.GetComponent<CloudNativeAnchor>();
        CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;

        // Delete local reference to spatial anchor.
        anchorIds.Remove(cloudSpatialAnchor.Identifier);
        anchorGameObjects.Remove(anchorGameObject);
        Destroy(anchorGameObject);
    }

    #endregion

    #region Unity

    void Start()
    {
        // Get reference to the Unity NetCode network manager.
        netManager = GameObject.FindGameObjectWithTag("NetworkManager").GetComponent<NetworkManager>();

        // Get reference to the spatial anchor manager in the scene.
        spatialAnchorManager = GetComponent<SpatialAnchorManager>();

        // Initialize the lists used to hold spatial anchor data.
        anchorGameObjects = new List<GameObject>(3);
        anchorIds = new List<string>(3);

        // Define the callback function when an anchor is located.
        spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;

        // Initialize the data structures for the synchronization triangle axes.
        myAxes = new TriangleAxes();
        hostAxes = new TriangleAxes();

        wasLoaded = false;
    }

    #endregion

    #region Methods

    private void CreateTriangle()
    {
        Debug.Log("CreateTriangle() was called.");

        // Ensure there are enough spatial anchors saved to proceed.
        if (this.anchorGameObjects.Count != 3)
        {
            Debug.Log("Error: " + anchorGameObjects.Count.ToString() + " spatial anchors were found.");
            return;
        }

        Debug.Log("anchorGameObjects[0] = " + anchorGameObjects[0].transform.position.ToString());
        Debug.Log("anchorGameObjects[1] = " + anchorGameObjects[1].transform.position.ToString());
        Debug.Log("anchorGameObjects[2] = " + anchorGameObjects[2].transform.position.ToString());

        // Get references to the 3 spatial anchors.
        GameObject pointD = anchorGameObjects[0];
        GameObject pointE = anchorGameObjects[1];
        GameObject pointF = anchorGameObjects[2];

        // Find distances between all the spatial anchors.
        float distanceDE = Vector3.Distance(pointD.transform.position, pointE.transform.position);
        float distanceDF = Vector3.Distance(pointD.transform.position, pointF.transform.position);
        float distanceEF = Vector3.Distance(pointE.transform.position, pointF.transform.position);

        Debug.Log("distanceDE = " + distanceDE.ToString());
        Debug.Log("distanceDF = " + distanceDF.ToString());
        Debug.Log("distanceEF = " + distanceEF.ToString());

        // Names for points on reference trangles.
        GameObject pointA = new GameObject();  //      B
        GameObject pointB = new GameObject();  //      | \
        GameObject pointC = new GameObject();  //      A - C

        // Determine which spatial anchor represents which point on the reference triangle
        // using the known side length values.
        if (distanceDE > distanceDF)
        {
            if (distanceDE > distanceEF)
            {
                // Therefore, line DE must represent the hypotenuse.

                if (distanceEF > distanceDF)
                {
                    // line EF must represent the x-axis.
                    // the point on both the hypotenuse and x-axis is pointC.
                    pointC = pointE;

                    // the other point on the x-axis must be pointA.
                    pointA = pointF;

                    // the last remaining point must be pointB.
                    pointB = pointD;
                }
                else // distanceDF > distanceEF
                {
                    // line DF must represent the x-axis.
                    // the point on both the hypotenuse and x-axis is pointC.
                    pointC = pointD;

                    // the other point on the x-axis must be pointA.
                    pointA = pointF;

                    // the last remaining point must be pointB.
                    pointB = pointE;
                }
            }
            else // distanceEF > distanceDE
            {
                // Therefore, line EF must represent the hypotenuse.

                if (distanceDE > distanceDF)
                {
                    // Line DE must represent the x-axis.

                    // The point on both the hypotenuse and the x-axis is pointC.
                    pointC = pointE;

                    // The other point on the x-axis is pointA.
                    pointA = pointD;

                    // The last remaining point must be pointB.
                    pointB = pointF;
                }
                else // distanceDF > distanceDE
                {
                    // Line DF must represent the x-axis.

                    // The point on both the hypotenuse and the x-axis is pointC.
                    pointC = pointF;

                    // The other point on the x-axis is pointA.
                    pointA = pointD;

                    // The last remaining point must be pointB.
                    pointB = pointE;
                }
            }
        }
        else // distanceDF > distanceDE
        {
            if (distanceDF > distanceEF)
            {
                // distanceDF must represent the hypotenuse

                if (distanceEF > distanceDE)
                {
                    // distanceEF must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointF;

                    // the other point on the x-axis must be pointA
                    pointA = pointE;

                    // the last remaining point must be pointB
                    pointB = pointD;
                }
                else // distanceDE > distanceEF
                {
                    // distanceDE must represent the x-axis
                    // the point on both the hypotenuse and x-axis is pointC
                    pointC = pointD;

                    // the other point on the x-axis must be pointA
                    pointA = pointE;

                    // the last remaining point must be pointB
                    pointB = pointF;
                }
            }
            else // distanceEF > distanceDF
            {
                // Therefore, line EF must represent the hypotenuse.

                if (distanceDE > distanceDF)
                {
                    // Line DE must represent the x-axis.

                    // The point on both the hypotenuse and the x-axis is pointC.
                    pointC = pointE;

                    // The other point on the x-axis is pointA.
                    pointA = pointD;

                    // The last remaining point must be pointB.
                    pointB = pointF;
                }
                else // distanceDF > distanceDE
                {
                    // Line DF must represent the x-axis.

                    // The point on both the hypotenuse and the x-axis is pointC.
                    pointC = pointF;

                    // The other point on the x-axis is pointA.
                    pointA = pointD;

                    // The last remaining point must be pointB.
                    pointB = pointE;
                }
            }
        }

        Debug.Log("pointD.transform.position = " + pointD.transform.position.ToString());
        Debug.Log("pointE.transform.position = " + pointE.transform.position.ToString());
        Debug.Log("pointF.transform.position = " + pointF.transform.position.ToString());

        // Determine unit vectors of each shared axis using reference triangle.
        Vector3 unitXAxis = -1.0f * Vector3.Normalize(pointA.transform.position - pointC.transform.position);
        Vector3 unitYAxis = -1.0f * Vector3.Normalize(pointA.transform.position - pointB.transform.position);
        Vector3 unitZAxis = -1.0f * Vector3.Cross(unitYAxis, unitXAxis);

        Debug.Log("pointA.transform.position = " + pointA.transform.position.ToString());
        Debug.Log("pointB.transform.position = " + pointB.transform.position.ToString());
        Debug.Log("pointC.transform.position = " + pointC.transform.position.ToString());

        Debug.Log("unitXAxis = " + unitXAxis.ToString());
        Debug.Log("unitYAxis = " + unitYAxis.ToString());
        Debug.Log("unitZAxis = " + unitZAxis.ToString());

        // Package the axes for future use.
        myAxes.xAxis = new Vector3(unitXAxis.x, unitXAxis.y, unitXAxis.z);
        myAxes.yAxis = new Vector3(unitYAxis.x, unitYAxis.y, unitYAxis.z);
        myAxes.zAxis = new Vector3(unitZAxis.x, unitZAxis.y, unitZAxis.z);

        // Save the transform of pointA as the origin.
        // This point is equivalent to the origin on the master client's coordinate system.
        myAxes.originPos = new Vector3(pointA.transform.position.x, pointA.transform.position.y, pointA.transform.position.z);
        myAxes.originRot = new Quaternion(pointA.transform.rotation.x, pointA.transform.rotation.y, pointA.transform.rotation.z, pointA.transform.rotation.w);

        // If this is the host, send your axes to the clients for synchronization.
        if (netManager.IsHost)
        {
            Debug.Log("Host is setting hostAxes.");

            hostAxes.xAxis = myAxes.xAxis;
            hostAxes.yAxis = myAxes.yAxis;
            hostAxes.zAxis = myAxes.zAxis;
            hostAxes.originPos = myAxes.originPos;
            hostAxes.originRot = myAxes.originRot;
        }
        else
        {
            Debug.Log("This is not the host.");
        }
    }

    public async void CreateButton()
    {
        Debug.Log("CreateButton() was called.");

        if (anchorGameObjects.Count > 0)
        {
            Debug.Log("Anchors already exist!");
            return;
        }

        // Start a Azure Spatial Anchors session if not already started.
        if (!spatialAnchorManager.IsSessionStarted)
        {
            await spatialAnchorManager.StartSessionAsync();
        }

        // Get the direction the user is currently facing.
        Vector3 headDirection = Camera.main.transform.forward;

        // Create the 3 spatial anchors based on the user's current position.
        await CreateAnchor(Vector3.zero + headDirection * 1.0f, Quaternion.identity);
        await CreateAnchor(Vector3.zero + headDirection * 1.0f + new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
        await CreateAnchor(Vector3.zero + headDirection * 1.0f + new Vector3(0.4f, 0.0f, 0.0f), Quaternion.identity);
    }

    public void SendButton()
    {
        Debug.Log("SendButton() was called.");

        // Create the synchronization triangle for the host.
        CreateTriangle();

        // Get a reference to the RPC manager for communication with clients.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnchorsRPCCallsManager rpcManager = player.GetComponent<AnchorsRPCCallsManager>();

        // Tell client to delete any spatial anchors that already exist.
        rpcManager.SendResetClientRpc();

        // Pass the spatial anchor ids to the client.
        foreach (string id in anchorIds)
        {
            rpcManager.SendAnchorIDClientRpc(id);
        }

        rpcManager.SendMasterAxes(myAxes);
    }

    public void DeleteButton()
    {
        Debug.Log("DeleteButton() was called.");

        // Get a reference to the RPC manager for communication with clients.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnchorsRPCCallsManager rpcManager = player.GetComponent<AnchorsRPCCallsManager>();

        rpcManager.SendResetClientRpc();
    }

    public void PrintButton()
    {
        Debug.Log("PrintButton() was called.");

        Debug.Log("My X-Axis: " + myAxes.xAxis.ToString());
        Debug.Log("My Y-Axis: " + myAxes.yAxis.ToString());
        Debug.Log("My Z-Axis: " + myAxes.zAxis.ToString());

        Debug.Log("My OriginPos: " + myAxes.originPos.ToString());
        Debug.Log("My OriginRot: " + myAxes.originRot.ToString());

        Debug.Log("Number of spatial anchors: " + anchorGameObjects.Count.ToString());

        Debug.Log("Host X-Axis: " + hostAxes.xAxis.ToString());
        Debug.Log("Host Y-Axis: " + hostAxes.yAxis.ToString());
        Debug.Log("Host Z-Axis: " + hostAxes.zAxis.ToString());

        Debug.Log("Host OriginPos: " + hostAxes.originPos.ToString());
        Debug.Log("Host OriginRot: " + hostAxes.originRot.ToString());

        if (netManager.IsHost)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            AnchorsRPCCallsManager rpcManager = player.GetComponent<AnchorsRPCCallsManager>();

            rpcManager.SendPrintRequestClientRpc();
        }
    }

    public void SaveButton()
    {
        Debug.Log("SaveButton() was called.");

        AnchorSaveAndLoad.SaveAnchors(anchorIds);
    }

    public void LoadButton()
    {
        Debug.Log("LoadButton() was called.");
        anchorIds = AnchorSaveAndLoad.LoadAnchors();

        wasLoaded = true;

        LocateAnchors();
    }

    #endregion



    [ServerRpc(RequireOwnership = false)] public void CreateButton_ServerRpc() { CreateButton(); }

    [ServerRpc(RequireOwnership = false)] public void SendButton_ServerRpc() { SendButton(); }
}