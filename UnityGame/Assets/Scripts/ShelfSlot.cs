using System.Collections;
using UnityEngine;

// Этот скрипт вешается на корень ShelfUnit — стеллаж вдоль стены, куда игрок кладёт
// (переносит) посылки. Логика полностью аналогична SortingZone.cs, только зона приёма —
// объём вокруг стеллажа, а не коврик на полу. У корня должен быть Collider с галочкой
// "Is Trigger" (WarehouseBuilder.SetupShelfSorting() следит, чтобы он был и был нужного размера) —
// иначе OnTriggerEnter никогда не вызовется и стеллаж не будет ни на что реагировать.
public class ShelfSlot : MonoBehaviour
{
    // Какую категорию посылок принимает именно этот стеллаж. Настраивается в инспекторе —
    // для каждого стеллажа на сцене выставишь своё значение (Fragile / Normal / Heavy).
    [SerializeField]
    private PackageCategory acceptedCategory;

    // Необязательный источник звука. Если объект не назначен в инспекторе — просто
    // не будем проигрывать звук (см. проверку ниже).
    [SerializeField]
    private AudioSource feedbackAudio;

    // Через сколько секунд убирать правильно отсортированную посылку со сцены —
    // чтобы игрок успел увидеть цветовую вспышку, а не увидел мгновенное исчезновение.
    [SerializeField]
    private float despawnDelay = 0.5f;

    // Цвет, в который на мгновение вспыхивает стеллаж при правильной сортировке.
    public Color correctFeedbackColor = Color.green;

    private Renderer zoneRenderer;
    private Color originalColor;

    private void Start()
    {
        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null)
        {
            // Запоминаем исходный цвет, чтобы потом вернуться к нему после вспышки.
            originalColor = zoneRenderer.material.color;
        }
    }

    // OnTriggerEnter вызывается автоматически, когда чей-то Collider заходит внутрь
    // нашего Collider-а с галочкой "Is Trigger" (в нашем случае — коллайдер посылки).
    private void OnTriggerEnter(Collider other)
    {
        PackagePickup pickup = other.GetComponent<PackagePickup>();
        if (pickup == null)
        {
            return;
        }

        if (pickup.Category == acceptedCategory)
        {
            HandleCorrectSort(pickup.gameObject);
        }
        // Категория не совпала — намеренно ничего не делаем: штрафов за ошибку нет,
        // игрок просто забирает посылку и несёт на другой стеллаж.
    }

    private void HandleCorrectSort(GameObject package)
    {
        if (feedbackAudio != null && feedbackAudio.clip != null)
        {
            feedbackAudio.Play();
        }

        StartCoroutine(FlashAndRemove(package));
    }

    private IEnumerator FlashAndRemove(GameObject package)
    {
        if (zoneRenderer != null)
        {
            zoneRenderer.material.color = correctFeedbackColor;
        }

        yield return new WaitForSeconds(despawnDelay);

        if (zoneRenderer != null)
        {
            zoneRenderer.material.color = originalColor;
        }

        Destroy(package);
    }
}
