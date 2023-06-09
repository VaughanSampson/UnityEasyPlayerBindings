using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities; 

namespace EPBindings
{
    public class EasyPlayerBindings
    {
         
        private PlayerInput playerInput;
        private InputActionMap actionMap;
         
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private bool doingInteractiveRebind = false; 

        /// <summary>
        /// This constructor initializes an <c>EasyPlayerBindings</c> object
        /// with a reference to a <c>PlayerInput</c> class.
        /// </summary>
        /// <param name="playerInput">A <c>PlayerInput</c> class.</param>
        public EasyPlayerBindings(PlayerInput playerInput)
        {
            this.playerInput = playerInput;
        }

        /// <summary>
        /// This selects an <c>ActionMap</c> from the <c>PlayerInput</c> to make this action map the subject of rebindings.
        /// </summary>
        /// <param name="actionMapName">The name of the selected action map.</param>
        public void SelectActionMap(string actionMapName)
        {
            actionMap = playerInput.actions.FindActionMap(actionMapName); 
        }
         
        /// <summary>
        /// Returns an integer index to directly reference a binding which is in a composite action.
        /// </summary>
        /// <param name="selectedAction">The composite action.</param>
        /// <param name="compositeName">The name of the set of composite bindings within the action.</param>
        /// <param name="compositeBindingName">The name of the composite binding within the composite binding set.</param>
        /// <returns>An integer index referring to a specific binding within the action.</returns>
        /// <exception cref="ArgumentException"></exception>
        private int FindCompositeBindingPartIndex(InputActionReference selectedAction, string compositeName, string compositeBindingName)
        {
            int index = -1;
            bool workingWithinDefinedComposite = false;
            for (int i = 0; i < selectedAction.action.bindings.Count; i++)
            {
                if (selectedAction.action.bindings[i].isComposite)
                    workingWithinDefinedComposite = selectedAction.action.bindings[i].name.ToLower() == compositeName.ToLower();
                else
                if (workingWithinDefinedComposite)
                {
                    if (selectedAction.action.bindings[i].name.ToLower() == compositeBindingName.ToLower())
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == -1)
                throw new ArgumentException("Passed compositeName and compositeBindingName do not reference any composite binding in the actionMap " + actionMap.name);

            return index;
        }

        /// <summary>
        /// This predicate is checked to protect against interactive rebinding errors.
        /// </summary>
        /// <param name="selectedAction">The selected action for rebinding.</param>
        /// <returns></returns>
        ///  
        private bool CanDoInteractiveRebind(InputActionReference selectedAction)
        {
            if (actionMap == null)
            {
                Debug.LogWarning("Interactive rebind request failed. " +
                    "There is no selected Action Map. " +
                    "Select an Action Map using EasyPlayerBindings.SelectActionMap(string actionMapName).");
                return false;
            }
            else
            if (selectedAction == null || selectedAction.ToInputAction() == null)
            {
                Debug.LogWarning("Interactive rebind request failed. " +
                    "The passed action to rebind was not found in the selected Action Map.");
                return false;
            }
            else
            if (doingInteractiveRebind)
            {
                Debug.LogWarning("Interactive rebind request failed. " +
                    "An InteractiveRebind is already active.");
                return false;
            } 

            return true;

        }

        /// <summary>
        /// This begins an interactive rebinding for a binding at a specific index within an action specified by string.
        /// </summary>
        /// <param name="actionName">The name of the action to be effected.</param>
        /// <param name="actionBindingIndex">The index of the specific binding within the action.</param>
        /// <param name="controlsExcluding">The string representation of the controls that should be ignored and not mapped on press.</param>
        public void InteractiveRebind(string actionName, int actionBindingIndex = 0, string controlsExcluding = "")
        { 
            InputActionReference selectedAction = ScriptableObject.CreateInstance<InputActionReference>();
            selectedAction.Set(actionMap.FindAction(actionName));
            InteractiveRebind(selectedAction, actionBindingIndex, controlsExcluding);

        }
         
        /// <summary>
        /// This begins an interactive rebinding for a composite binding of a specific name within an action specified by string.
        /// </summary>
        /// <param name="actionName">The name of the action to be effected.</param>
        /// <param name="compositeName">The name of the composite binding set to be effected.</param>
        /// <param name="compositeBindingName">The name of the composite binding within the set to be effect.</param>
        /// <param name="controlsExcluding">The string representation of the controls that should be ignored and not mapped on press.</param>
        public void InteractiveCompositeRebind(string actionName, string compositeName, string compositeBindingName, string controlsExcluding = "")
        {
            InputActionReference selectedAction = ScriptableObject.CreateInstance<InputActionReference>();
            selectedAction.Set(actionMap.FindAction(actionName));

            int bindingIndex = FindCompositeBindingPartIndex(selectedAction, compositeName, compositeBindingName);

            InteractiveRebind(selectedAction, bindingIndex, controlsExcluding);

        }

        private bool actionMapOfInteractiveRebindIsCurrentActionMap;

        /// <summary>
        /// This method does an interactive rebinding to a given action reference, at a given id.
        /// </summary>
        /// <param name="action">The action to rebind to.</param>
        /// <param name="actionBindingIndex">The index of the specific binding within the action.</param>
        /// <param name="controlsExcluding">The string representation of the controls that should be ignored and not mapped on press.</param>
        public void InteractiveRebind(InputActionReference action, int actionBindingIndex = 0, string controlsExcluding = "")
        { 
            if (!CanDoInteractiveRebind(action))
                return;

            doingInteractiveRebind = true;

            actionMapOfInteractiveRebindIsCurrentActionMap = playerInput.currentActionMap == actionMap;
            if(actionMapOfInteractiveRebindIsCurrentActionMap) actionMap.Disable();

            rebindOperation = action.action.PerformInteractiveRebinding();

            if (controlsExcluding != "")
                rebindOperation.WithControlsExcluding(controlsExcluding);

            rebindOperation.WithTargetBinding(actionBindingIndex);

            rebindOperation.OnMatchWaitForAnother(0.1f);
            rebindOperation.OnComplete(operation => InteractiveRebindClosed());
            rebindOperation.OnCancel(operation => InteractiveRebindClosed());

            rebindOperation.Start();

        }

        /// <summary>
        /// This method will cancel the active interactive rebinding.
        /// </summary>
        public void CancelInteractiveRebind()
        {
            rebindOperation.Cancel();
            InteractiveRebindClosed(); 
         }

        private event Action<InputAction> OnInteractiveRebindClosed;

        /// <summary>
        /// This is called to cleanup after an interactive rebinding.
        /// </summary>
        private void InteractiveRebindClosed()
        { 
            OnInteractiveRebindClosed?.Invoke(rebindOperation.action);
            
            rebindOperation.Dispose();
            doingInteractiveRebind = false;
            if (actionMapOfInteractiveRebindIsCurrentActionMap) actionMap.Enable();
            actionMapOfInteractiveRebindIsCurrentActionMap = false;
        }
         
        /// <summary>
        /// This predicate is used to check if an interactive rebinding is already occurring.
        /// </summary>
        /// <returns>A boolean describing if an interactive rebinding is active.</returns>
        public bool IsDoingInteractiveRebind()
        {
            return doingInteractiveRebind;
        }


        /// <summary>
        /// This will rebind to a specific actiongiven a bind path of the new binding.
        /// </summary>
        /// <param name="actionName">The string name of the action to rebind to.</param>
        /// <param name="newBindingPath">The bind path for the new binding.</param>
        public void RebindTo(string actionName, string newBindingPath) => RebindTo(actionName, 0, newBindingPath);

        /// <summary>
        /// This will rebind to a specific action, at a specific index, given a bind path of the new binding.
        /// </summary>
        /// <param name="actionName">The string name of the action to rebind to.</param>
        /// <param name="bindingIndex">The integer index to rebind to within the action.</param>
        /// <param name="newBindingPath">The string bind path for the new binding.</param>
        public void RebindTo(string actionName, int bindingIndex, string newBindingPath)
        {
            InputActionReference selectedAction = ScriptableObject.CreateInstance<InputActionReference>();
            selectedAction.Set(actionMap.FindAction(actionName)); 

            RebindTo(selectedAction, bindingIndex, newBindingPath);
        }

        /// <summary>
        /// This will rebind a specific binding in a composite action.
        /// </summary>
        /// <param name="actionName">The action to rebind to.</param>
        /// <param name="compositeName">The name of the composite group of bindings.</param>
        /// <param name="compositeBindingName">The name of a binding within the composite set.</param>
        /// <param name="newBindingPath">The bind path for the new binding.</param>
        public void RebindToComposite(string actionName, string compositeName, string compositeBindingName, string newBindingPath)
        {
            InputActionReference selectedAction = ScriptableObject.CreateInstance<InputActionReference>();
            selectedAction.Set(actionMap.FindAction(actionName));

            RebindTo(selectedAction, FindCompositeBindingPartIndex(selectedAction, compositeName, compositeBindingName), newBindingPath);
        }

        /// <summary>
        /// This will rebind to a specific action, at a specific index, given a bind path of the new binding.
        /// </summary>
        /// <param name="action">An InputActionReference pointing at the action ot be rebinded.</param>
        /// <param name="bindingIndex">The index to bind to.</param>
        /// <param name="newBindingPath">The bind path for the new binding.</param>
        public void RebindTo(InputActionReference action, int bindingIndex, string newBindingPath)
        {
            InputBinding binding = action.action.bindings[bindingIndex];
            binding.overridePath = newBindingPath;
            action.action.ApplyBindingOverride(bindingIndex, binding); 
        } 


        /// <summary>
        /// This "SaveRebinds" saves rebind information to player prefs.
        /// </summary>
        /// <param name="saveTitle">The title used to save rebinds more uniquely.</param>
        public void SaveRebinds(string saveTitle = "playerActionBindings")
        {
            string rebinds = playerInput.actions.SaveBindingOverridesAsJson(); 
            PlayerPrefs.SetString(saveTitle + "_rebinds", rebinds);
        }

        /// <summary>
        /// This "SaveRebinds" loads rebind information from player prefs.
        /// </summary>
        /// <param name="saveTitle">The title used to save rebinds more uniquely.</param>
        public void LoadRebinds(string saveTitle = "playerActionBindings")
        {
            string rebinds = PlayerPrefs.GetString(saveTitle + "_rebinds", string.Empty);
            if (string.IsNullOrEmpty(rebinds)) { return; }
            playerInput.actions.LoadBindingOverridesFromJson(rebinds);
        }

        // To get the binding path of the next user input, the  following code:
        // creates a temporary action map (not an asset),
        // adds an action to this map (not an asset),
        // performs an interactive rebind to that action,
        // reads the new binding path of said action.

        // Needless to say this is ineffecient but given that Unity currently provides no easy alternative solutions to this problem
        // and that recalling a rebinding input in most use-cases will occur only rarely (most games don't have to handle excessive continuous run-time rebindings),
        // this system is functionally effective.

        // There are more effecient solutions on forums which concentrate on rebinding to a specific physical interface, for example,
        // solely for mouse or for controller. This code alternatively will return any input which can be bound to a Unity InputAction.

        private Action<string> callbackNextInput; 
        private bool doingGetNextInput; 
        private InputActionRebindingExtensions.RebindingOperation getNextInputPathRebindOperation;

        /// <summary>
        /// This will get the path of the next input and send it to the given function.
        /// </summary>
        /// <param name="controlsExcluding"></param>
        public void GetNextInput(Action<string> callback, string controlsExcluding = "")
        {
            if (doingGetNextInput)
                return;
            doingGetNextInput = true;

            callbackNextInput = callback;

            InputActionMap getInputActionMap = new InputActionMap();
            getInputActionMap.AddAction("GetKey");
            getInputActionMap.actions[0].AddBinding("");
            getNextInputPathRebindOperation = getInputActionMap.actions[0].PerformInteractiveRebinding();

            if (controlsExcluding != "")
                getNextInputPathRebindOperation.WithControlsExcluding(controlsExcluding); 

            getNextInputPathRebindOperation.WithTargetBinding(0);

            getNextInputPathRebindOperation.OnMatchWaitForAnother(0.1f);
            getNextInputPathRebindOperation.OnComplete(operation => GetNextInputClosed()); 
            getNextInputPathRebindOperation.Start();
        }
  
        private void GetNextInputClosed()
        {
            callbackNextInput(getNextInputPathRebindOperation.action.bindings[0].overridePath); 
            getNextInputPathRebindOperation.Dispose();
            doingGetNextInput = false; 
        }
         

    }



}