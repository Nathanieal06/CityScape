using UnityEngine;
using UnityEngine.InputSystem;

namespace CityScape.ExploreMode
{
    [RequireComponent(typeof(NPCController))]
    public class NPCInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private string simpleDialogueLine = "Hi!";

        [Header("References")]
        [SerializeField] private NPCDialogueUI dialogueUI;
        
        private NPCController _controller;
        private Transform _playerTransform;
        
        private bool _isPlayerInRange = false;
        private bool _isDialogueActive = false;

        private void Awake()
        {
            _controller = GetComponent<NPCController>();
            if (dialogueUI != null)
            {
                dialogueUI.Initialize(this);
            }
        }

        private void Start()
        {
            // Try to find the player. It might be disabled initially if in Build Mode.
            FindPlayer();
        }

        private void FindPlayer()
        {
            // PlayerController is only active in ExploreMode usually, but let's find it.
            PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            if (!_isDialogueActive)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                bool inRange = (dist <= interactionDistance);

                if (inRange && !_isPlayerInRange)
                {
                    _isPlayerInRange = true;
                    if (dialogueUI != null) dialogueUI.ShowInteractionPrompt();
                }
                else if (!inRange && _isPlayerInRange)
                {
                    _isPlayerInRange = false;
                    if (dialogueUI != null) dialogueUI.HideInteractionPrompt();
                }

                // Handle Input
                if (_isPlayerInRange && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
                {
                    StartDialogue();
                }
            }
            else
            {
                // Handle closing dialogue with Escape
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    EndDialogue();
                }
            }
        }

        private void StartDialogue()
        {
            _isDialogueActive = true;
            _controller.SetPaused(true);
            
            if (dialogueUI != null)
            {
                dialogueUI.ShowDialogue(simpleDialogueLine);
            }
        }

        public void EndDialogue()
        {
            _isDialogueActive = false;
            _controller.SetPaused(false);
            
            if (dialogueUI != null)
            {
                dialogueUI.HideAll();
                
                // Re-evaluate if player is still in range to show prompt again immediately
                if (_playerTransform != null)
                {
                    float dist = Vector3.Distance(transform.position, _playerTransform.position);
                    _isPlayerInRange = (dist <= interactionDistance);
                    if (_isPlayerInRange) dialogueUI.ShowInteractionPrompt();
                }
            }
        }
    }
}
