using UnityEngine;

// Этот скрипт вешается на саму посылку (на префаб куба).
// Он отвечает за: 1) хранение категории посылки, 2) визуальный цвет по категории,
// 3) логику "взять/нести/положить" от первого лица (см. PackageGrabber.cs).
public class PackagePickup : MonoBehaviour
{
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

    // Свойство (property) — как @property в Python: снаружи выглядит как обычное поле
    // (someScript.Category), но на самом деле это метод "только для чтения".
    // "=> category;" — сокращённая запись "get { return category; }".
    public PackageCategory Category => category;

    // Нужно ConveyorLoopMover-у: пока посылку держат в руках, конвейер её не толкает.
    public bool IsHeld => isHeld;

    // Форма/размер по категории — чтобы игрок отличал посылки не только по цвету,
    // но и по силуэту: Fragile выглядит как плоское "письмо", Heavy — как крупная коробка.
    private static readonly Vector3 FragileScale = new Vector3(0.55f, 0.12f, 0.4f);
    private static readonly Vector3 NormalScale = new Vector3(0.8f, 0.8f, 0.8f);
    private static readonly Vector3 HeavyScale = new Vector3(1.15f, 1.15f, 1.15f);

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

    // Раскрашиваем куб и меняем его форму/размер в зависимости от категории — чтобы
    // игрок мог отличать посылки друг от друга на глаз, без чтения текста.
    private void ApplyCategoryVisual()
    {
        // GetComponent<Renderer>() ищет компонент, отвечающий за отображение объекта
        // (то, что рисует его на экране материалом/цветом). У куба он есть по умолчанию.
        Renderer rend = GetComponent<Renderer>();

        // switch по enum — аналог Python if/elif цепочки или match-case (3.10+),
        // только компилятор проверяет, что мы не забыли ни один вариант enum.
        switch (category)
        {
            case PackageCategory.Fragile:
                if (rend != null) rend.material.color = Color.yellow;
                transform.localScale = FragileScale;
                break;
            case PackageCategory.Heavy:
                if (rend != null) rend.material.color = Color.red;
                transform.localScale = HeavyScale;
                break;
            case PackageCategory.Normal:
            default:
                if (rend != null) rend.material.color = Color.white;
                transform.localScale = NormalScale;
                break;
        }
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
