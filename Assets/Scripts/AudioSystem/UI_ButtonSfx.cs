using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UI_ButtonSfx : MonoBehaviour
{
    private void Start()
    {
        AudioManager.Instance?.RegisterButton(GetComponent<Button>());
    }
}
