// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;
using System;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ThisHoloObject : MonoBehaviour,
                             IFocusable,
                             IInputHandler,
                             ISourceStateHandler,
                             ISpeechHandler
{
    /// <summary>
    /// Event triggered when dragging starts.
    /// </summary>
    public event Action StartedDragging;

    /// <summary>
    /// Event triggered when dragging starts.
    /// </summary>
    public enum AcceleratedScales {zero, one, two, three};
    public AcceleratedScales scaleAccelerationLevel = AcceleratedScales.zero;

    /// <summary>
    /// Event triggered when dragging stops.
    /// </summary>
    public event Action StoppedDragging;

    [Tooltip("Transform that will be dragged. Defaults to the object of the component.")]
    public Transform HostTransform;

    [Tooltip("Scale by which hand movement in z is multipled to move the dragged object.")]
    public float DistanceScale = 2f;

    public enum RotationModeEnum
    {
        Default,
        LockObjectRotation,
        OrientTowardUser,
        OrientTowardUserAndKeepUpright
    }

    public RotationModeEnum RotationMode = RotationModeEnum.Default;

    [Tooltip("Controls the speed at which the object will interpolate toward the desired position")]
    [Range(0.01f, 1.0f)]
    public float PositionLerpSpeed = 0.2f;

    [Tooltip("Controls the speed at which the object will interpolate toward the desired rotation")]
    [Range(0.01f, 1.0f)]
    public float RotationLerpSpeed = 0.2f;

    public bool IsDraggingEnabled = true;

    private Camera mainCamera;
    private bool isDragging;
    private bool isGazed;
    private Vector3 objRefForward;
    private Vector3 objRefUp;
    private float objRefDistance;
    private Quaternion gazeAngularOffset;
    private float handRefDistance;
    private Vector3 objRefGrabPoint;

    private Vector3 draggingPosition;
    private Quaternion draggingRotation;

    private IInputSource currentInputSource = null;
    private uint currentInputSourceId;

    private float rotateAngle = 8.0f;
    private float scaleByAmount = 0.9F;
    private float handMovementUpper = 5.0F;
    private float handMovementLower = -5.0F;
    private float triggerInterval = 0.0005F;
    private float rotateAngleControl = 50f;

    private Vector3 acceleratedScale0 = new Vector3(0.1f, 0.1f, 0.1f);
    private Vector3 acceleratedScale1 = new Vector3(1f, 1f, 1f);
    private Vector3 acceleratedScale2 = new Vector3(5f, 5f, 5f);
    private Vector3 acceleratedScale3 = new Vector3(15f, 15f, 15f);
    

    private Collider collidedObject;

    private void Start()
    {
        if (HostTransform == null)
        {
            HostTransform = transform;
        }

        mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (isDragging)
        {
            StopDragging();
        }

        if (isGazed)
        {
            OnFocusExit();
        }
    }

    private void Update()
    {
        if (IsDraggingEnabled && isDragging)
        {
            UpdateDragging();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableUpMode()
    {
        Manager.opMode = Manager.OperationModes.Up;
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableSideMode()
    {
        Manager.opMode = Manager.OperationModes.Side;
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableAssembleMode()
    {
        Manager.opMode = Manager.OperationModes.Assemble;
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableScaleMode()
    {
        Manager.opMode = Manager.OperationModes.Scale;
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableResetMode()
    {
        Manager.resetScene();
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Basic");
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableBreakMode()
    {
        Manager.opMode = Manager.OperationModes.Break;
    }

    /// <summary>
    /// 
    /// </summary>
    public void enableRotateMode()
    {
        Manager.opMode = Manager.OperationModes.Rotate;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventData"></param>
    public void OnSpeechKeywordRecognized(SpeechKeywordRecognizedEventData eventData)
    {
        Manager.Instance.OnSpeechKeywordRecognized(eventData);
    }

    /// <summary>
    /// Starts dragging the object.
    /// </summary>
    public void StartDragging()
    {
        if (!IsDraggingEnabled)
        {
            return;
        }

        if (isDragging)
        {
            return;
        }

        // Add self as a modal input handler, to get all inputs during the manipulation
        InputManager.Instance.PushModalInputHandler(gameObject);

        isDragging = true;
        //GazeCursor.Instance.SetState(GazeCursor.State.Move);
        //GazeCursor.Instance.SetTargetObject(HostTransform);

        Vector3 gazeHitPosition = GazeManager.Instance.HitInfo.point;
        Vector3 handPosition;
        currentInputSource.TryGetPosition(currentInputSourceId, out handPosition);

        Vector3 pivotPosition = GetHandPivotPosition();
        handRefDistance = Vector3.Magnitude(handPosition - pivotPosition);
        objRefDistance = Vector3.Magnitude(gazeHitPosition - pivotPosition);

        Vector3 objForward = HostTransform.forward;
        Vector3 objUp = HostTransform.up;

        // Store where the object was grabbed from
        objRefGrabPoint = mainCamera.transform.InverseTransformDirection(HostTransform.position - gazeHitPosition);

        Vector3 objDirection = Vector3.Normalize(gazeHitPosition - pivotPosition);
        Vector3 handDirection = Vector3.Normalize(handPosition - pivotPosition);

        objForward = mainCamera.transform.InverseTransformDirection(objForward);       // in camera space
        objUp = mainCamera.transform.InverseTransformDirection(objUp);                 // in camera space
        objDirection = mainCamera.transform.InverseTransformDirection(objDirection);   // in camera space
        handDirection = mainCamera.transform.InverseTransformDirection(handDirection); // in camera space

        objRefForward = objForward;
        objRefUp = objUp;

        // Store the initial offset between the hand and the object, so that we can consider it when dragging
        gazeAngularOffset = Quaternion.FromToRotation(handDirection, objDirection);
        draggingPosition = gazeHitPosition;

        StartedDragging.RaiseEvent();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    void OnTriggerEnter(Collider other)
    {
        Invoke("assemble", triggerInterval);
        collidedObject = other;
    }

    /// <summary>
    /// 
    /// </summary>
    private void breakJoints()
    {
        int noOfJoints = 0;
        FixedJoint[] joints = this.gameObject.GetComponents<FixedJoint>();

        if (joints != null)
        {
            noOfJoints = joints.Length;
        }
        // see if joints are defined on the selected objects
        if (noOfJoints != 0)
        {
            breakjoints(noOfJoints, joints);
        }
        // see if joints to this objects are available on connected/collided object
        // this loop also ensures that in break mode no joints are created.
        else if (noOfJoints == 0)
        {
            if (this.collidedObject != null)
            {
                joints = this.collidedObject.GetComponents<FixedJoint>();
            }

            if (joints != null)
            {
                breakjoints(joints.Length, joints);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="noOfJoints"></param>
    /// <param name="joints"></param>
    private void breakjoints(int noOfJoints, FixedJoint[] joints)
    {
        for (int i = 0; i < noOfJoints; i++)
        {
            Destroy(joints[i]);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void assemble()
    {
        bool alreadyConnected = false;
        FixedJoint[] jointsOfThisObj = gameObject.GetComponents<FixedJoint>();
        FixedJoint[] jointsOfCollidedObj = collidedObject.gameObject.GetComponents<FixedJoint>();
        Rigidbody rBody = collidedObject.gameObject.GetComponent(typeof(Rigidbody)) as Rigidbody;

        // see if a joint is already established in the collided object?
        alreadyConnected = checkPreExistingJoints(gameObject.name, jointsOfCollidedObj);
        // do the same check for this object?
        if (!alreadyConnected)
        {
            alreadyConnected = checkPreExistingJoints(collidedObject.gameObject.name, jointsOfThisObj);
        }
        // go ahead if everything looks good!
        if (rBody != null && !alreadyConnected)
        {
            FixedJoint fJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;
            fJoint.connectedBody = rBody;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="nameOfObj"></param>
    /// <param name="joints"></param>
    /// <returns></returns>
    private bool checkPreExistingJoints(string nameOfObj, FixedJoint[] joints)
    {
        bool connected = false;
        for (int i = 0; i < joints.Length; i++)
        {
            if (connected)
            {
                break;
            }
            if (nameOfObj == joints[i].connectedBody.name)
            {
                connected = true;
            }
        }
        return connected;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vector"></param>
    private void debugPrintVector3(Vector3 vector)
    {
        Debug.Log(vector.x + " " + vector.y + " " + vector.z);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    private float getRelativeAngle(Vector3 newHandPosition, Vector3 axis)
    {
        //Vector3 targetDir = Camera.main.transform.forward - getObjCenter();
        return Vector3.SignedAngle(getObjCenter(), newHandPosition, axis);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private Vector3 getObjCenter()
    {
        Collider thisCollder = this.GetComponent(typeof(Collider)) as Collider;
        return thisCollder.bounds.center;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    private float getAngle(float number)
    {
        return number * Time.deltaTime;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    /// <param name="axis"></param>
    /// <param name="sign"></param>
    private void rotateAround(Vector3 newHandPosition, Vector3 axis, int sign)
    {
        Vector3 center = getObjCenter();
        float angle = getRelativeAngle(newHandPosition, axis);

        if (angle > handMovementUpper)
        {
            transform.RotateAround(center, sign * axis, getAngle(rotateAngle));
        }

        if (angle < handMovementLower)
        {
            transform.RotateAround(center, -sign * axis, getAngle(rotateAngle));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    private void rotate(Vector3 newHandPosition)
    {
        Vector3 center = getObjCenter();
        // rotate along X and Y axes.
        transform.RotateAround(center, -mainCamera.transform.right, getRelativeAngle(newHandPosition, Vector3.right) / rotateAngleControl);
        transform.RotateAround(center, -mainCamera.transform.up, getRelativeAngle(newHandPosition, Vector3.up) / rotateAngleControl);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    private void up(Vector3 newHandPosition)
    {
        Vector3 center = getObjCenter();
        float diffy = newHandPosition.y - GazeManager.Instance.GazeNormal.y;

        if (diffy < 0)
        {
            transform.RotateAround(center, -mainCamera.transform.right, getAngle(rotateAngle));
        }

        if (diffy > 0)
        {
            transform.RotateAround(center, mainCamera.transform.right, getAngle(rotateAngle));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    private void side(Vector3 newHandPosition)
    {
        Vector3 center = getObjCenter();
        float diffx = newHandPosition.x - GazeManager.Instance.GazeNormal.x;

        if (diffx < 0)
        {
            transform.RotateAround(center, mainCamera.transform.up, getAngle(rotateAngle));
        }

        if (diffx > 0)
        {
            transform.RotateAround(center, -mainCamera.transform.up, getAngle(rotateAngle));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private Vector3 getScaleQ()
    {     
        if (scaleAccelerationLevel == AcceleratedScales.one)
            return acceleratedScale1;
        else if (scaleAccelerationLevel == AcceleratedScales.two)
            return acceleratedScale2;
        else if (scaleAccelerationLevel == AcceleratedScales.three)
            return acceleratedScale3;
        else
            return acceleratedScale0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newHandPosition"></param>
    private void scale(Vector3 newHandPosition)
    {
        float diffy = newHandPosition.y - GazeManager.Instance.GazeNormal.y;
        Vector3 scaleQ = getScaleQ();

        if (diffy < 0)
        {
            transform.localScale = Vector3.Lerp(transform.localScale - scaleQ, transform.localScale, scaleByAmount);
        }

        if (diffy > 0)
        {
            transform.localScale = Vector3.Lerp(transform.localScale + scaleQ, transform.localScale, scaleByAmount);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parent"></param>
    /// <returns></returns>
    private List<GameObject> getConnectedObjects (GameObject parent)
    {
        List<GameObject> returnList = new List<GameObject>();
        FixedJoint[] joints = parent.GetComponents<FixedJoint>();
        for(int i = 0; i < joints.Length; i++)
        {
            GameObject child = joints[i].connectedBody.gameObject;
            returnList.Add(child);
            returnList.AddRange(getConnectedObjects(child));
        }
        return returnList;
    }

    /// <summary>
    /// Gets the pivot position for the hand, which is approximated to the base of the neck.
    /// </summary>
    /// <returns>Pivot position for the hand.</returns>
    private Vector3 GetHandPivotPosition()
    {
        Vector3 pivot = Camera.main.transform.position + new Vector3(0, -0.2f, 0) - Camera.main.transform.forward * 0.2f; // a bit lower and behind
        return pivot;
    }

    /// <summary>
    /// Enables or disables dragging.
    /// </summary>
    /// <param name="isEnabled">Indicates whether dragging shoudl be enabled or disabled.</param>
    public void SetDragging(bool isEnabled)
    {
        if (IsDraggingEnabled == isEnabled)
        {
            return;
        }

        IsDraggingEnabled = isEnabled;

        if (isDragging)
        {
            StopDragging();
        }
    }

    /// <summary>
    /// Update the position of the object being dragged.
    /// </summary>
    private void UpdateDragging()
    {
        if (Manager.OperationModes.Break == Manager.opMode)
        {
            breakJoints();
        }

        Vector3 newHandPosition;
        currentInputSource.TryGetPosition(currentInputSourceId, out newHandPosition);

        Vector3 pivotPosition = GetHandPivotPosition();

        Vector3 newHandDirection = Vector3.Normalize(newHandPosition - pivotPosition);

        newHandDirection = mainCamera.transform.InverseTransformDirection(newHandDirection); // in camera space
        Vector3 targetDirection = Vector3.Normalize(gazeAngularOffset * newHandDirection);
        targetDirection = mainCamera.transform.TransformDirection(targetDirection); // back to world space

        float currenthandDistance = Vector3.Magnitude(newHandPosition - pivotPosition);

        float distanceRatio = currenthandDistance / handRefDistance;
        float distanceOffset = distanceRatio > 0 ? (distanceRatio - 1f) * DistanceScale : 0;
        float targetDistance = objRefDistance + distanceOffset;

        draggingPosition = pivotPosition + (targetDirection * targetDistance);

        if (Manager.OperationModes.Up == Manager.opMode)
        {
            up(newHandPosition);
        }

        else if (Manager.OperationModes.Side == Manager.opMode)
        {
            side(newHandPosition);
        }

        else if (Manager.OperationModes.Rotate == Manager.opMode)
        {
            rotate(newHandPosition);
        }

        else if (Manager.OperationModes.Scale == Manager.opMode)
        {
            scale(newHandPosition);
        }

        else
        {
            if (RotationMode == RotationModeEnum.OrientTowardUser || RotationMode == RotationModeEnum.OrientTowardUserAndKeepUpright)
            {
                draggingRotation = Quaternion.LookRotation(HostTransform.position - pivotPosition);
            }
            else if (RotationMode == RotationModeEnum.LockObjectRotation)
            {
                draggingRotation = HostTransform.rotation;
            }
            else // RotationModeEnum.Default
            {
                Vector3 objForward = mainCamera.transform.TransformDirection(objRefForward); // in world space
                Vector3 objUp = mainCamera.transform.TransformDirection(objRefUp);   // in world space
                draggingRotation = Quaternion.LookRotation(objForward, objUp);
            }

            // Apply Final Position
            HostTransform.position = Vector3.Lerp(HostTransform.position, draggingPosition + mainCamera.transform.TransformDirection(objRefGrabPoint), PositionLerpSpeed);
            // Apply Final Rotation
            //HostTransform.rotation = Quaternion.Lerp(HostTransform.rotation, draggingRotation, RotationLerpSpeed);

            if (RotationMode == RotationModeEnum.OrientTowardUserAndKeepUpright)
            {
                Quaternion upRotation = Quaternion.FromToRotation(HostTransform.up, Vector3.up);
                HostTransform.rotation = upRotation * HostTransform.rotation;
            }
        }
    }

    /// <summary>
    /// Stops dragging the object.
    /// </summary>
    public void StopDragging()
    {
        if (!isDragging)
        {
            return;
        }

        // Remove self as a modal input handler
        InputManager.Instance.PopModalInputHandler();

        isDragging = false;
        currentInputSource = null;
        StoppedDragging.RaiseEvent();
    }

    public void OnFocusEnter()
    {
        if (!IsDraggingEnabled)
        {
            return;
        }

        if (isGazed)
        {
            return;
        }

        isGazed = true;
    }

    public void OnFocusExit()
    {
        if (!IsDraggingEnabled)
        {
            return;
        }

        if (!isGazed)
        {
            return;
        }

        isGazed = false;
    }

    public void OnInputUp(InputEventData eventData)
    {
        if (currentInputSource != null &&
            eventData.SourceId == currentInputSourceId)
        {
            StopDragging();
        }
    }

    public void OnInputDown(InputEventData eventData)
    {
        if (isDragging)
        {
            // We're already handling drag input, so we can't start a new drag operation.
            return;
        }

        if (!eventData.InputSource.SupportsInputInfo(eventData.SourceId, SupportedInputInfo.Position))
        {
            // The input source must provide positional data for this script to be usable
            return;
        }

        currentInputSource = eventData.InputSource;
        currentInputSourceId = eventData.SourceId;
        StartDragging();
    }

    public void OnSourceDetected(SourceStateEventData eventData)
    {
        // Nothing to do
    }

    public void OnSourceLost(SourceStateEventData eventData)
    {
        if (currentInputSource != null && eventData.SourceId == currentInputSourceId)
        {
            StopDragging();
        }
    }
}

