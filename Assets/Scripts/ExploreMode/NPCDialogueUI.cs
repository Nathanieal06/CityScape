using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

namespace CityScape.ExploreMode
{
    public class NPCDialogueUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject interactionPromptGroup; // Contains "Press E to Talk"
        [SerializeField] private GameObject dialogueGroup;          // Contains the "Hi!" text and close button
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button closeButton;

        private NPCInteraction _interaction;

        public void Initialize(NPCInteraction interaction)
        {
            _interaction = interaction;
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
                closeButton.gameObject.SetActive(false); // Hide the button as we use Escape to close now
            }

            HideAll();
        }

        public void ShowInteractionPrompt()
        {
            if (interactionPromptGroup != null) interactionPromptGroup.SetActive(true);
            if (dialogueGroup != null) dialogueGroup.SetActive(false);
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptGroup != null) interactionPromptGroup.SetActive(false);
        }

        public void ShowDialogue(string text)
        {
            HideInteractionPrompt();
            if (dialogueGroup != null)
            {
                dialogueGroup.SetActive(true);
                if (dialogueText != null) dialogueText.text = text;
            }
        }

        public void HideAll()
        {
            if (interactionPromptGroup != null) interactionPromptGroup.SetActive(false);
            if (dialogueGroup != null) dialogueGroup.SetActive(false);
        }

        private void OnCloseClicked()
        {
            // When close is clicked, tell the interaction component to end dialogue
            _interaction.EndDialogue();
        }

        private void LateUpdate()
        {
            // Make the world space UI always face the main camera
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }
    }
}
