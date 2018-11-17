// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
//
// Copyright (c) 2018 Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Creator Agreement, located
// here: https://id.magicleap.com/creator-terms
//
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;
using System;

namespace MagicLeap
{
    /// <summary>
    /// This class handles the functionality of updating the bounding box
    /// for the planes query params through input. This class also updates
    /// the UI text containing the latest useful info on the planes queries.
    /// </summary>
    [RequireComponent(typeof(Planes))]
    public class PlanesExample : MonoBehaviour
    {
        #region Private Variables
        [SerializeField, Tooltip("Flag specifying if plane extents are bounded.")]
        private bool _bounded = false;

        [Space, SerializeField, Tooltip("Text to display number of planes.")]
        private Text _numberOfPlanesText;

        [SerializeField, Tooltip("Text to display if planes extents are bounded or boundless.")]
        private Text _boundedExtentsText;

        [Space, SerializeField, Tooltip("ControllerConnectionHandler reference.")]
        private ControllerConnectionHandler _controllerConnectionHandler;

        [SerializeField, Tooltip("The keyboard input text.")]
        private Text _keyboardText;

        private Planes _planesComponent;

        private static readonly Vector3 _boundedExtentsSize = new Vector3(5.0f, 5.0f, 5.0f);
        // Distance close to sensor's maximum recognition distance.
        private static readonly Vector3 _boundlessExtentsSize = new Vector3(10.0f, 10.0f, 10.0f);

        private Camera _camera;
        #endregion

        #region Unity Methods
        /// <summary>
        /// Check editor set variables for null references.
        /// </summary>
        void Awake()
        {
            if (_numberOfPlanesText == null)
            {
                Debug.LogError("Error: PlanesExample._numberOfPlanesText is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_boundedExtentsText == null)
            {
                Debug.LogError("Error: PlanesExample._boundedExtentsText is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_controllerConnectionHandler == null)
            {
                Debug.LogError("Error: PlanesExample._controllerConnectionHandler is not set, disabling script.");
                enabled = false;
                return;
            }

            MLInput.OnControllerButtonDown += OnButtonDown;

            _planesComponent = GetComponent<Planes>();
            _camera = Camera.main;
        }

        /// <summary>
        /// Start bounds based on _bounded state.
        /// </summary>
        void Start()
        {
            UpdateBounds();
        }

        /// <summary>
        /// Update position of the planes component to camera position.
        /// Planes query center is based on this position.
        /// </summary>
        void Update()
        {
            _planesComponent.gameObject.transform.position = _camera.transform.position;
        }

        /// <summary>
        /// Cleans up the component.
        /// </summary>
        void OnDestroy()
        {
            MLInput.OnControllerButtonDown -= OnButtonDown;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Update plane query bounds extents based on if the current _bounded status is true(bounded)
        /// or false(boundless).
        /// </summary>
        private void UpdateBounds()
        {
            _planesComponent.transform.localScale = _bounded ? _boundedExtentsSize : _boundlessExtentsSize;

            _boundedExtentsText.text = string.Format("Bounded Extents: ({0},{1},{2})",
                _planesComponent.transform.localScale.x,
                _planesComponent.transform.localScale.y,
                _planesComponent.transform.localScale.z);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Callback handler, changes text when new planes are received.
        /// </summary>
        /// <param name="planes"> Array of new planes. </param>
        public void OnPlanesUpdate(MLWorldPlane[] planes)
        {
            _numberOfPlanesText.text = string.Format("Number of Planes: {0}/{1}", planes.Length, _planesComponent.MaxPlaneCount);
        }

          /// <summary>
        /// Keyboard events are propagated via Unity's event system. OnGUI is the preferred way
        /// to catch these events.
        /// </summary>
        private void OnGUI()
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Backspace)
                {
                    if (_keyboardText.text.Length > 0)
                    {
                        _keyboardText.text = _keyboardText.text.Substring(0, _keyboardText.text.Length - 1);
                    }
                }
                else if (e.keyCode == KeyCode.Return)
                {
                    _keyboardText.text += "\n";
                }
                else if (!Char.IsControl(e.character))
                {
                    _keyboardText.text += e.character;
                }

                Debug.Log(e.keyCode);
            }
        }

        /// <summary>
        /// Handles the event for button down. Changes from bounded to boundless and viceversa
        /// when pressing home button
        /// </summary>
        /// <param name="controllerId">The id of the controller.</param>
        /// <param name="button">The button that is being released.</param>
        private void OnButtonDown(byte controllerId, MLInputControllerButton button)
        {
            if(button == MLInputControllerButton.HomeTap)
            {
                _keyboardText.text = "";
            }
        }
        #endregion
    }
}
