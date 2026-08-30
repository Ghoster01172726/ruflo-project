using UnityEngine;

// Процедурная "физика" движения рук от первого лица: вешается на пустой родительский
// объект "Hands" (тот, что несёт оба меша рук). Сама модель рук никогда не двигается
// напрямую в SetupHands() — вместо этого каждый кадр мы чуть смещаем/поворачиваем этот
// объект относительно его исходной локальной позиции (basePosition/baseRotation),
// поэтому смещение всегда накладывается поверх изначальной расстановки рук.
public class HandsMotion : MonoBehaviour
{
    [SerializeField] private CharacterController playerController;

    [Header("Покачивание при ходьбе (bob)")]
    [SerializeField] private float bobFrequency = 7f;
    [SerializeField] private float bobHorizontalAmount = 0.012f;
    [SerializeField] private float bobVerticalAmount = 0.018f;

    [Header("Раскачивание от взгляда (sway)")]
    [SerializeField] private float swayAmount = 0.6f;
    [SerializeField] private float swaySmoothing = 8f;

    [Header("Дыхание в покое (idle)")]
    [SerializeField] private float idleFrequency = 1.2f;
    [SerializeField] private float idleVerticalAmount = 0.004f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float bobTimer;
    private Vector3 currentSwayOffset;
    private Quaternion currentSwayRotation = Quaternion.identity;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    private void Update()
    {
        Vector3 bobOffset = ComputeWalkBob();
        Vector3 swayOffset = ComputeLookSway(out Quaternion swayRotation);

        transform.localPosition = basePosition + bobOffset + swayOffset;
        transform.localRotation = baseRotation * swayRotation;
    }

    private Vector3 ComputeWalkBob()
    {
        // Горизонтальная скорость игрока (без вертикальной составляющей от гравитации/прыжка) —
        // амплитуда покачивания растёт вместе со скоростью ходьбы и падает до нуля в покое.
        float horizontalSpeed = playerController != null
            ? new Vector3(playerController.velocity.x, 0f, playerController.velocity.z).magnitude
            : 0f;
        bool isMoving = horizontalSpeed > 0.1f;

        if (isMoving)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float speedFactor = Mathf.Clamp01(horizontalSpeed / 4f);
            float horizontal = Mathf.Cos(bobTimer) * bobHorizontalAmount * speedFactor;
            float vertical = Mathf.Abs(Mathf.Sin(bobTimer)) * bobVerticalAmount * speedFactor;
            return new Vector3(horizontal, vertical, 0f);
        }

        // В покое — не резкий сброс в ноль, а медленное "дыхание" вверх-вниз для ощущения живости.
        float idleVertical = Mathf.Sin(Time.time * idleFrequency) * idleVerticalAmount;
        return new Vector3(0f, idleVertical, 0f);
    }

    private Vector3 ComputeLookSway(out Quaternion swayRotation)
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Руки чуть "отстают" от взгляда в противоположную сторону — тот же приём, что
        // используют для раскачивания оружия в шутерах от первого лица.
        Vector3 targetOffset = new Vector3(-mouseX, -mouseY, 0f) * (swayAmount * 0.01f);
        Quaternion targetRotation = Quaternion.Euler(mouseY * swayAmount, -mouseX * swayAmount, 0f);

        currentSwayOffset = Vector3.Lerp(currentSwayOffset, targetOffset, Time.deltaTime * swaySmoothing);
        currentSwayRotation = Quaternion.Slerp(currentSwayRotation, targetRotation, Time.deltaTime * swaySmoothing);

        swayRotation = currentSwayRotation;
        return currentSwayOffset;
    }
}
