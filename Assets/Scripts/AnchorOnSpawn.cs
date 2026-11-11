using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AnchorOnSpawn : MonoBehaviour
{
    private async void Start()
    {
        var anchorManager = FindFirstObjectByType<ARAnchorManager>();
        if (anchorManager == null)
        {
            Debug.LogError("AR Anchor Manager lipsește din scenă.");
            return;
        }

        var pose = new Pose(transform.position, transform.rotation);
        var result = await anchorManager.TryAddAnchorAsync(pose);

        if (!result.status.IsSuccess())
        {
            Debug.LogError($"Crearea ancorei a eșuat: {result.status}");
            return;
        }

        var anchor = result.value;
        if (this == null)
        {
            Destroy(anchor.gameObject);
            return;
        }

        transform.SetParent(anchor.transform, worldPositionStays: true);
        Debug.Log($"Cub ancorat: {anchor.trackableId}");
    }
}