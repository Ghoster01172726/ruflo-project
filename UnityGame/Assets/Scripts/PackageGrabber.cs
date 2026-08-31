using UnityEngine;

// Забор/бросок посылок от первого лица: игрок смотрит на посылку в пределах grabRange
// и жмёт interactKey — она "прилипает" к точке у руки (handAnchor), пока не бросишь
// повторным нажатием той же клавиши.
public class PackageGrabber : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform handAnchor;
    [SerializeField] private float grabRange = 2.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private PackagePickup heldPackage;

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey))
        {
            return;
        }

        if (heldPackage != null)
        {
            heldPackage.Release();
            heldPackage = null;
            return;
        }

        TryGrab();
    }

    private void TryGrab()
    {
        if (playerCamera == null || handAnchor == null)
        {
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, grabRange))
        {
            return;
        }

        PackagePickup pickup = hit.collider.GetComponentInParent<PackagePickup>();
        if (pickup == null)
        {
            return;
        }

        pickup.Grab(handAnchor);
        heldPackage = pickup;
    }
}
