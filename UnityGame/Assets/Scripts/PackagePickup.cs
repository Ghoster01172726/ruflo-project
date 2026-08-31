using UnityEngine;

// Этот скрипт вешается на саму посылку (на префаб куба, оставшийся только как
// носитель Collider/Rigidbody — собственный меш куба отключается ниже).
// Он отвечает за: 1) хранение категории посылки, 2) подстановку настоящей 3D-модели
// (коробка или конверт) по категории, 3) логику "взять/нести/положить" от первого
// лица (см. PackageGrabber.cs).
public class PackagePickup : MonoBehaviour
{
    // Модели посылок (Poly Pizza, CC0): "Cardboard Box Closed" by Kenney — для
    // Normal/Heavy, "Envelope" by reyshapes — для Fragile ("письмо"). Назначаются
    // один раз через Warehouse Tools/Setup Package Visuals (WarehouseBuilder.Packages.cs).
    [SerializeField] private GameObject boxModelPrefab;
    [SerializeField] private GameObject envelopeModelPrefab;

    // Приватное поле для хранения категории. Снаружи его напрямую не меняют —
    // для этого есть метод SetCategory() и свойство Category ниже.
    private PackageCategory category;

    // Флаг "сейчас несут эту посылку или нет". bool в C# — то же самое, что bool в Python (True/False),
    // просто пишется с маленькой буквы: true/false.
    private bool isHeld;

    // Точка у руки игрока, за которой посылка "следует", пока её несут.
    // Назначается снаружи через Grab() — сама посылка ничего не знает про камеру/игрока.
    private Transform holdAnchor;

    // Твёрдый коллайдер посылки. Пока её несут, каждый кадр телепортируем transform
    // прямо перед камерой (handAnchor) — если в этот момент коллайдер перекрывает
    // капсулу CharacterController игрока, встроенное выталкивание из перекрытия
    // резко расталкивает игрока ("улетает вместе с посылкой"). Поэтому на время
    // переноски коллайдер отключается и включается обратно при Release().
    private Collider packageCollider;

    // Текущий заспавненный инстанс модели (коробки/конверта) — пересоздаётся при смене
    // категории, чтобы не плодить старые меши друг под другом.
    private GameObject visualInstance;

    // Свойство (property) — как @property в Python: снаружи выглядит как обычное поле
    // (someScript.Category), но на самом деле это метод "только для чтения".
    // "=> category;" — сокращённая запись "get { return category; }".
    public PackageCategory Category => category;

    // Нужно ConveyorLoopMover-у: пока посылку держат в руках, конвейер её не толкает.
    public bool IsHeld => isHeld;

    // Размер твёрдого коллайдера по категории (форма всегда box — используется только
    // для физики/захвата, не для визуала) и целевой видимый размер модели (по наибольшему
    // габариту) — так игрок отличает посылки не только по модели, но и по размеру.
    private static readonly Vector3 FragileColliderSize = new Vector3(0.28f, 0.02f, 0.22f);
    private static readonly Vector3 NormalColliderSize = new Vector3(0.3f, 0.3f, 0.3f);
    private static readonly Vector3 HeavyColliderSize = new Vector3(0.45f, 0.45f, 0.45f);
    private const float FragileVisualSize = 0.28f;
    private const float NormalVisualSize = 0.3f;
    private const float HeavyVisualSize = 0.45f;

    // Start() Unity вызывает один раз, когда объект впервые появляется на сцене —
    // аналог __init__ в Python, только вызывается не при создании инстанса класса,
    // а когда сам игровой объект "просыпается" в игровом мире.
    private void Start()
    {
        packageCollider = GetComponent<Collider>();
        ApplyCategoryVisual();
    }

    // Публичный метод — вызывается извне, например из PackageSpawner сразу после Instantiate.
    // Так мы задаём категорию только что созданной посылке.
    public void SetCategory(PackageCategory newCategory)
    {
        category = newCategory;
        ApplyCategoryVisual();
    }

    // Подставляем настоящую модель (коробка/конверт) и коллайдер нужного размера —
    // так игрок отличает посылки друг от друга по виду, без чтения текста.
    private void ApplyCategoryVisual()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
            visualInstance = null;
        }

        GameObject modelPrefab;
        Vector3 colliderSize;
        float visualSize;

        // switch по enum — аналог Python if/elif цепочки или match-case (3.10+),
        // только компилятор проверяет, что мы не забыли ни один вариант enum.
        switch (category)
        {
            case PackageCategory.Fragile:
                modelPrefab = envelopeModelPrefab;
                colliderSize = FragileColliderSize;
                visualSize = FragileVisualSize;
                break;
            case PackageCategory.Heavy:
                modelPrefab = boxModelPrefab;
                colliderSize = HeavyColliderSize;
                visualSize = HeavyVisualSize;
                break;
            case PackageCategory.Normal:
            default:
                modelPrefab = boxModelPrefab;
                colliderSize = NormalColliderSize;
                visualSize = NormalVisualSize;
                break;
        }

        if (packageCollider is BoxCollider box)
        {
            box.size = colliderSize;
        }

        if (modelPrefab == null)
        {
            return;
        }

        visualInstance = Instantiate(modelPrefab, transform);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;
        FitVisualScale(visualInstance, visualSize);
    }

    // Модели приходят в произвольном родном масштабе (см. WarehouseBuilder.Packages.cs) —
    // растягиваем/сжимаем равномерно так, чтобы наибольший габарит стал равен targetSize.
    private static void FitVisualScale(GameObject go, float targetSize)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension <= 0.0001f)
        {
            return;
        }

        float scaleFactor = targetSize / maxDimension;
        go.transform.localScale *= scaleFactor;
    }

    // Вызывается снаружи из PackageGrabber, когда игрок посмотрел на посылку и нажал
    // клавишу взаимодействия. anchor — точка у камеры/руки, за которой посылка едет.
    public void Grab(Transform anchor)
    {
        isHeld = true;
        holdAnchor = anchor;

        // Отключаем твёрдый коллайдер на время переноски — иначе телепортация
        // посылки к руке каждый кадр может протолкнуть её сквозь CharacterController
        // игрока и вызвать резкое выталкивание ("улетает вместе с посылкой").
        if (packageCollider != null)
        {
            packageCollider.enabled = false;
        }
    }

    // Вызывается снаружи из PackageGrabber при повторном нажатии — "бросаем" посылку.
    // Она остаётся лежать там, где отпущена (или её снова подхватит ConveyorLoopMover,
    // если она осталась в зоне конвейера).
    public void Release()
    {
        isHeld = false;
        holdAnchor = null;

        // Возвращаем коллайдер — без него ShelfSlot не увидит посылку в своей
        // триггер-зоне, а конвейер не сможет снова её подхватить.
        if (packageCollider != null)
        {
            packageCollider.enabled = true;
        }
    }

    // Update вызывается каждый кадр, пока объект существует на сцене — как и в PackageSpawner.
    private void Update()
    {
        // Пока посылку не подняли — ничего не делаем и сразу выходим из метода.
        // "return;" без значения — то же самое, что return в Python-функции без выражения.
        if (!isHeld || holdAnchor == null)
        {
            return;
        }

        // Посылка просто "прилипает" к точке руки — следует за её позицией и поворотом.
        transform.position = holdAnchor.position;
        transform.rotation = holdAnchor.rotation;
    }
}
