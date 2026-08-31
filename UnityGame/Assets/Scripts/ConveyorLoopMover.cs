using UnityEngine;

// Двигает посылку по замкнутому кругу вейпоинтов — как настоящая карусель конвейера.
// В отличие от старого ConveyorMover (который просто ехал вперёд и останавливался),
// эта посылка НИКОГДА не исчезает и не замирает: если игрок не успел её забрать,
// она едет к следующей точке маршрута и так по кругу бесконечно.
// Пока посылку держат в руках (PackagePickup.IsHeld) — движение по конвейеру приостановлено.
public class ConveyorLoopMover : MonoBehaviour
{
    public Transform waypointsRoot;
    public float speed = 0.6f;

    private int targetIndex;
    private bool hasTarget;
    private bool wasHeld;
    private PackagePickup pickup;

    private void Start()
    {
        pickup = GetComponent<PackagePickup>();
    }

    private void Update()
    {
        if (waypointsRoot == null || waypointsRoot.childCount == 0)
        {
            return;
        }

        bool isHeld = pickup != null && pickup.IsHeld;
        if (isHeld)
        {
            wasHeld = true;
            return;
        }

        // Только что отпустили посылку (или она никогда не была в руках) — цепляемся
        // за ближайшую следующую точку маршрута вместо той, что была актуальна до захвата.
        if (!hasTarget || wasHeld)
        {
            wasHeld = false;
            hasTarget = true;
            targetIndex = NextIndexAfterClosest();
        }

        Transform target = waypointsRoot.GetChild(targetIndex);
        Vector3 toTarget = target.position - transform.position;
        float step = speed * Time.deltaTime;

        if (toTarget.magnitude <= step)
        {
            transform.position = target.position;
            targetIndex = (targetIndex + 1) % waypointsRoot.childCount;
        }
        else
        {
            transform.position += toTarget.normalized * step;
        }
    }

    private int NextIndexAfterClosest()
    {
        int closest = 0;
        float closestDist = float.MaxValue;
        for (int i = 0; i < waypointsRoot.childCount; i++)
        {
            float dist = Vector3.Distance(transform.position, waypointsRoot.GetChild(i).position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = i;
            }
        }

        return (closest + 1) % waypointsRoot.childCount;
    }
}
