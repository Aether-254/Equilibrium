using UnityEngine;

public class Conveyor : MonoBehaviour
{
    public float speed = 2f;
    public Vector2 direction = Vector2.right;

    void OnTriggerStay2D(Collider2D other)
    {
        other.transform.Translate(direction * speed * Time.deltaTime);
    }
}