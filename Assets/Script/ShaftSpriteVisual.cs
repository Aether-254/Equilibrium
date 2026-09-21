using UnityEngine;

public class ShaftSpriteVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;

    [SerializeField] private float rpm = 60f;

    private float angle;

    public float RPM
    {
        get => rpm;
        set => rpm = value;
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (spriteRenderer == null)
            return;

        if (frames == null || frames.Length == 0)
            return;

        angle = Mathf.Repeat(
            angle + rpm * 6f * Time.deltaTime,
            360f
        );

        int index = Mathf.FloorToInt(
            angle / 360f * frames.Length
        );

        if (index >= frames.Length)
            index = frames.Length - 1;

        spriteRenderer.sprite = frames[index];
    }
}