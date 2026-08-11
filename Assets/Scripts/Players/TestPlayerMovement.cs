using UnityEngine;
public class TestPlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    private void Update()
    {
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move += Vector3.back;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        transform.position += move * speed * Time.deltaTime;
    }
}