using UnityEngine;

// Категория посылки. В Python ты бы, наверное, сделал это строкой ("fragile", "normal", "heavy")
// или классом-константой. В C# для фиксированного набора вариантов принято использовать enum —
// это как Python-класс со значениями, унаследованный от Enum, только встроенный в язык.
// Плюс enum: опечатку в названии категории компилятор поймает сразу, а не в рантайме.
public enum PackageCategory
{
    Fragile,
    Normal,
    Heavy
}

// Класс наследуется от MonoBehaviour — это базовый класс Unity для любого скрипта,
// который должен быть "живым" компонентом на GameObject-е (аналог: миксин, который
// даёт объекту методы Start/Update и доступ к transform, gameObject и т.д.).
// Только классы-наследники MonoBehaviour можно перетащить на объект в Unity Editor.
public class PackageSpawner : MonoBehaviour
{
    // [SerializeField] — атрибут, который говорит Unity: "покажи это приватное поле
    // в инспекторе (панель настроек объекта), чтобы его можно было менять мышкой,
    // без правки кода". Аналог — как если бы у тебя был dataclass с полями,
    // и отдельная GUI-форма сама подхватывала бы поля для редактирования.
    // Приватное (private) поле обычно не видно снаружи — здесь просто по умолчанию,
    // ключевое слово private можно было бы дописать явно.
    [SerializeField]
    private GameObject packagePrefab; // "Чертёж" посылки, которую будем создавать (Prefab).

    [SerializeField]
    private Transform spawnPoint; // Точка в 3D-пространстве, где появляются посылки.

    [SerializeField]
    private float spawnInterval = 3f; // Пауза между спавнами в секундах. "= 3f" — значение по умолчанию.

    // Настройки конвейера-карусели, по которому едет только что заспавненная посылка.
    // Синхронизируются с реальным маршрутом через WarehouseBuilder.AddConveyorLoop
    // (Warehouse Tools/Add Conveyor Loop). conveyorWaypointsRoot — родитель точек
    // замкнутого маршрута (см. ConveyorLoopMover.cs), по которым посылка едет по кругу.
    [SerializeField]
    private Transform conveyorWaypointsRoot;

    [SerializeField]
    private float conveyorSpeed = 0.6f;

    // Внутренний таймер-накопитель. В Python это была бы обычная переменная
    // self.timer = 0.0 в __init__. Здесь нет отдельного конструктора — вместо него
    // Unity вызывает специальные методы (Awake, Start), а поля инициализируются сразу при объявлении.
    private float timer;

    // Update() — метод, который Unity сам вызывает один раз каждый кадр (frame),
    // примерно как game loop в pygame: while running: ... update() ... draw().
    // Тебе не нужно писать этот цикл самому — Unity делает это за тебя.
    private void Update()
    {
        // Time.deltaTime — сколько секунд реально прошло с прошлого кадра.
        // Если бы мы просто прибавляли "1" каждый кадр, скорость таймера зависела бы
        // от FPS (на слабом ПК секунда игры "тянулась" бы дольше). deltaTime решает эту проблему —
        // это аналог dt в физическом симуляторе на Python (time.time() - last_time).
        timer += Time.deltaTime;

        // Как только накопили нужный интервал — спавним посылку и сбрасываем таймер.
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnPackage();
        }
    }

    // Обычный приватный метод, вызывается только изнутри этого класса.
    private void SpawnPackage()
    {
        // Защитная проверка: если забыли назначить префаб в инспекторе — не падаем,
        // а пишем предупреждение в консоль и выходим. Аналог: if package_prefab is None: return.
        if (packagePrefab == null)
        {
            Debug.LogWarning("PackageSpawner: packagePrefab не назначен в инспекторе.");
            return;
        }

        // Определяем позицию спавна: если точка не задана — спавним в позиции самого спавнера.
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;

        // Instantiate — функция Unity, которая клонирует префаб и создаёт из него
        // новый живой объект на сцене. Это как copy.deepcopy() шаблонного объекта
        // плюс автоматическая регистрация его в "мире" игры.
        // Параметры: что клонировать, позиция, поворот (Quaternion.identity — "без поворота").
        GameObject package = Instantiate(packagePrefab, position, Quaternion.identity);

        // Random.Range(0, 3) для целых чисел возвращает число из {0, 1, 2} — верхняя граница
        // не включается, как в Python range(0, 3). System.Enum.GetValues возвращает массив
        // всех значений enum PackageCategory — аналог list(PackageCategory) в Python (если бы
        // PackageCategory был классом Enum из модуля enum).
        PackageCategory[] categories = (PackageCategory[])System.Enum.GetValues(typeof(PackageCategory));
        PackageCategory randomCategory = categories[Random.Range(0, categories.Length)];

        // GetComponent<T>() ищет на объекте компонент нужного типа — аналог getattr, но по типу,
        // а не по имени. Если на префабе уже есть PackagePickup — используем его,
        // иначе на всякий случай добавляем через AddComponent<T>() (аналог setattr/динамического
        // добавления поведения, но именно как компонент Unity).
        PackagePickup pickup = package.GetComponent<PackagePickup>();
        if (pickup == null)
        {
            pickup = package.AddComponent<PackagePickup>();
        }

        // Присваиваем сгенерированную категорию посылке — метод объявлен в PackagePickup.cs.
        pickup.SetCategory(randomCategory);

        // Ставим посылку на конвейер-карусель: она едет по кругу вейпоинтов бесконечно,
        // пока игрок её не заберёт (см. ConveyorLoopMover.cs). Не забрал вовремя —
        // посылка не исчезает, а просто продолжает ехать дальше по кругу.
        ConveyorLoopMover mover = package.GetComponent<ConveyorLoopMover>();
        if (mover == null)
        {
            mover = package.AddComponent<ConveyorLoopMover>();
        }

        mover.waypointsRoot = conveyorWaypointsRoot;
        mover.speed = conveyorSpeed;
    }
}
