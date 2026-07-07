using CityScape.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityScape.UI
{
    /// <summary>
    /// Listens for the Escape key to toggle the pause menu.
    /// Attach this to any persistent GameObject, like the Managers object.
    /// </summary>
    public class PauseInputHandler : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                GameManager.Instance?.TogglePause();
        }
    }
}
