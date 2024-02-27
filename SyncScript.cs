using UnityEngine;
using Unity.Netcode;

public class SyncScript : NetworkBehaviour
{
    // Network variables to pass transform data between client and server.
    public NetworkVariable<Vector3> hostPosVector = new NetworkVariable<Vector3>();
    public NetworkVariable<Quaternion> hostRotQuat = new NetworkVariable<Quaternion>();
    public NetworkVariable<Vector3> hostScale = new NetworkVariable<Vector3>();

    // Reference to the Spatial Anchor Script attached to the GameManager object.
    public SimpleShare simpleShare;

    void Start()
    {
        // Get a reference to the Spatial Anchor Script attached to the GameManager
        // object in the scene when spawned.
        GameObject gameManager = GameObject.FindGameObjectWithTag("GameController");
        simpleShare = gameManager.GetComponent<SimpleShare>();

        Debug.Log("A SyncScript has been spawned.");
    }

    void Update()
    {
        // Update network variables with current transform if this is the host.
        if (simpleShare.netManager.IsHost)
        {
            if (simpleShare.wasLoaded)
            {
                Vector3 deltaPosition = gameObject.transform.position - simpleShare.hostAxes.originPos;

                float scalarX = deltaPosition.x;
                float scalarY = deltaPosition.y;
                float scalarZ = deltaPosition.z;

                Vector3 deltaX = scalarX * simpleShare.myAxes.zAxis;
                Vector3 deltaY = scalarY * simpleShare.myAxes.yAxis;
                Vector3 deltaZ = scalarZ * (-1.0f * simpleShare.myAxes.xAxis);
                deltaPosition = deltaX + deltaY + deltaZ;

                hostPosVector.Value = deltaPosition + simpleShare.hostAxes.originPos;
                hostRotQuat.Value = gameObject.transform.rotation * simpleShare.hostAxes.originRot;

            }
            else
            {
                hostPosVector.Value = gameObject.transform.position;
                hostRotQuat.Value = gameObject.transform.rotation;
            }

            hostScale.Value = gameObject.transform.localScale;
        }
        // Update object position using host's coordinate system determined in the Spatial Anchor Script.
        if (!simpleShare.netManager.IsHost && simpleShare.netManager.IsClient)
        {
            // Adjust the reported position of the host's letterboard by their initial offset.
            Vector3 deltaPosition = hostPosVector.Value - simpleShare.hostAxes.originPos;
            Quaternion deltaRotation = hostRotQuat.Value * Quaternion.Inverse(simpleShare.hostAxes.originRot);

            // Convert movement from master client into a scalar value wrt each axis.
            float scalarX = deltaPosition.x;
            float scalarY = deltaPosition.y;
            float scalarZ = deltaPosition.z;

            // Multiply the scalar values from the master client with the secondary client's
            // unit vectors for their synchronized coordinate system.
            Vector3 deltaX = scalarX * simpleShare.myAxes.xAxis;
            Vector3 deltaY = scalarY * simpleShare.myAxes.yAxis;
            Vector3 deltaZ = scalarZ * simpleShare.myAxes.zAxis;
            deltaPosition = deltaX + deltaY + deltaZ;

            // Apply the movement to the object in the secondary client's coordinate system.
            gameObject.transform.position = simpleShare.myAxes.originPos + deltaPosition;
            gameObject.transform.rotation = simpleShare.myAxes.originRot * deltaRotation;
            gameObject.transform.localScale = hostScale.Value;
        }
    }
}