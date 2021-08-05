using System;
using UnityEngine;
using UnityEngine.Serialization;


[RequireComponent(typeof(Camera))]
class OrbittingCamera : MonoBehaviour
{
    [Serializable]
    struct Setup
    {
#if UNITY_EDITOR
        public string debugName;
#endif
        public float lookAtHeight;
        public float height;
        public float radius;
        public Vector3 offset;
        [Header("Orbit Controls")]
        public float sensitivity;
        public float defaultOrbitRate;
        public bool canChangeOrbitDirection;
        public float maxOrbitRate;
        public float inertia;
        public float snapSmoothTime;
        public bool snapOnRelease;
        [FormerlySerializedAs("snapToFirstTargetOnEnter")]
        public bool snapToTargetOnEnter;
        public int snapTargetIndex;
        [Header("Projection")]
        public bool overrideProjection;
        public float projectionFieldOfView;
        public float projectionSize;

    }


    [SerializeField] float m_DefaultFieldOfView;
    [SerializeField] float m_DefaultSize;
    [SerializeField] float m_DefaultOrbitAngle;
    [SerializeField] int m_DefaultSetupIndex;
    [SerializeField] float m_NearClipPlane;
    [SerializeField] float m_FarClipPlane;
    [SerializeField] Transform m_OrbitAround;
    [SerializeField] Setup[] m_Setups = new Setup[0];
    [SerializeField] float[] m_SnapTargets = new float[0];
    [SerializeField] TweenRunner m_ChangeSetupAnimation;

    [SerializeField] Camera m_GameCamera;

    public bool IsAnimating { get; private set; }

    Transform m_Transform;
    Camera m_Camera;



    bool m_Init;

    int m_OldSetupIndex;
    int m_ActiveSetupIndex;
    float m_OrbitAngle;
    float m_OrbitAngleAnimStart;
    float m_ManualOrbitDelta;
    float m_OrbitRate;
    float m_ProjectionSize;

    int m_State;

    const int kAuto = 0;
    const int kManualOrbit = 1;
    const int kInertia = 2;
    const int kInitialSnap = 3;
    const int kSnapToTarget = 4;

    const float kMinFOV = 0.05f;

    int m_NearestSnapTargetIndex;
    float m_DistanceToNearestSnapTarget;

    public int nearestSnapTargetIndex
    {
        get { return m_NearestSnapTargetIndex; }
    }

    public float distanceToNearestSnapTarget
    {
        get { return m_DistanceToNearestSnapTarget; }
    }

    float DefaultSnapTarget()
    {
        return m_SnapTargets.Length > 0 ? m_SnapTargets[0] : 0f;
    }

    Vector3 OrbitAroundPosition()
    {
        if (m_OrbitAround)
        {
            return m_OrbitAround.position;
        }
        Debug.LogWarning("m_OrbitAround is unassigned", this);
        return Vector3.zero;
    }

    [ContextMenu("Cycle Setup")]
    public void CycleSetup()
    {
        ChangeSetup((m_ActiveSetupIndex + 1) % m_Setups.Length);
    }
 

    public void SnapToTarget(int targetIndex)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (m_Setups.Length == 0)
        {
            return;
        }

        if (targetIndex < 0 || targetIndex >= m_SnapTargets.Length)
        {
            Debug.LogError("Can't snap to target : out of range", this);
        }

        if (m_State == kInitialSnap && targetIndex == 0)
        {
            return;
        }

        if (m_ChangeSetupAnimation.keepWaiting)
        {
            m_ChangeSetupAnimation.InstantTweenAction();
            m_ChangeSetupAnimation.Stop();
        }

        m_State = kSnapToTarget;
        m_NearestSnapTargetIndex = targetIndex;
    }

    public void ChangeSetup(int setupIndex, bool instant = false)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (setupIndex < 0 || setupIndex >= m_Setups.Length)
        {
            Debug.LogError("Can't change setup : out of range", this);
            return;
        }

        if (setupIndex == m_ActiveSetupIndex && !instant)
        {
            return;
        }

        m_OldSetupIndex = m_ActiveSetupIndex;
        m_ActiveSetupIndex = setupIndex;
        if (m_Setups[m_ActiveSetupIndex].snapToTargetOnEnter)
        {
            m_State = kInitialSnap;
        }
        else
        {
            m_State = kAuto;
        }

        if (instant)
        {
            m_ChangeSetupAnimation.InstantTweenAction();
        }
        else
        {
            m_ChangeSetupAnimation.Start();
        }
    }
    public void ChangeSetupFromEditor(int setupIndex)
    {
        ChangeSetup(setupIndex, false);
    }
    public void ManualPanOrbit(float delta)
    {
        if (!Application.isPlaying)
        {
            return;
        }
        m_ManualOrbitDelta = delta;
        m_State = kManualOrbit;
    }

    void SetCameraProjection(float fov, float size)
    {
        fov = Mathf.Max(fov, kMinFOV);
        //if (fov > 0.1f)
        {
            m_Camera.orthographic = false;
            m_Camera.fieldOfView = fov;
            m_ProjectionSize = size;
        }


        float distance = !m_Camera.orthographic ? (float)(size / Math.Tan(fov * 0.5 * Math.PI / 180.0)) : 0;
        m_Transform.position += m_Transform.rotation * new Vector3(0, 0, -distance);
        m_Camera.nearClipPlane = Mathf.Max(m_NearClipPlane, m_NearClipPlane + distance - 100);
        m_Camera.farClipPlane = m_NearClipPlane + distance + (m_FarClipPlane - m_NearClipPlane);
    }

    void Awake()
    {
        m_Transform = GetComponent<Transform>();
        m_Camera = GetComponent<Camera>();
        m_ProjectionSize = m_DefaultSize;
        m_OrbitAngle = m_DefaultOrbitAngle;

        m_ChangeSetupAnimation.Init(this, (t, unscaledT, init, duration, ignoreTimeScale) =>
        {
            IsAnimating = t < 1f;

            if (init)
            {
                    if (m_GameCamera)
                    {
                        m_GameCamera.enabled = false;
                    }

                    m_Camera.enabled = true;
            }

            Vector3 position1, position2, forward1, forward2;
            CalculatePositionAndRotation(m_OldSetupIndex, out position1, out forward1, m_OrbitAngle);
            CalculatePositionAndRotation(m_ActiveSetupIndex, out position2, out forward2, m_OrbitAngle);
            m_Transform.position = Vector3.LerpUnclamped(position1, position2, t);
            m_Transform.forward = Vector3.LerpUnclamped(forward1, forward2, t).normalized;


            // Animate projection
            float fieldOfView0, size0, fieldOfView1, size1;
            if (m_Setups[m_OldSetupIndex].overrideProjection)
            {
                fieldOfView0 = m_Setups[m_OldSetupIndex].projectionFieldOfView;
                size0 = m_Setups[m_OldSetupIndex].projectionSize;
            }
            else
            {
                fieldOfView0 = m_DefaultFieldOfView;
                size0 = m_DefaultSize;
            }

            if (m_Setups[m_ActiveSetupIndex].overrideProjection)
            {
                fieldOfView1 = m_Setups[m_ActiveSetupIndex].projectionFieldOfView;
                size1 = m_Setups[m_ActiveSetupIndex].projectionSize;
            }
            else
            {
                fieldOfView1 = m_DefaultFieldOfView;
                size1 = m_DefaultSize;
            }

            if (!Mathf.Approximately(fieldOfView1, fieldOfView0) || !Mathf.Approximately(fieldOfView1, fieldOfView0))
            {
                float fov = Mathf.LerpUnclamped(Mathf.Max(fieldOfView0, kMinFOV), Mathf.Max(fieldOfView1, kMinFOV), t);
                float size = Mathf.LerpUnclamped(size0, size1, t);
                SetCameraProjection(fov, size);
            }
        });

        if (m_Setups.Length > 0)
        {
            ChangeSetup(m_DefaultSetupIndex, true);
        }
    }

    void Update()
    {
        if (m_Setups.Length == 0)
        {
            return;
        }

        Setup activeSetup = m_Setups[m_ActiveSetupIndex];
        if (m_State == kManualOrbit)
        {
            m_OrbitRate = Mathf.Lerp(m_OrbitRate, m_ManualOrbitDelta * activeSetup.sensitivity / Time.deltaTime, 0.5f); // Input smoothing
            m_OrbitRate = Mathf.Min(Mathf.Abs(m_OrbitRate), activeSetup.maxOrbitRate) * Mathf.Sign(m_OrbitRate);
            m_State = kInertia;
        }
        else if (m_State == kInertia)
        {
            if (activeSetup.inertia > 0f)
            {
                m_OrbitRate *= Mathf.Pow(activeSetup.inertia, Time.deltaTime);
                if (Mathf.Abs(m_OrbitRate) <= Mathf.Abs(activeSetup.defaultOrbitRate))
                {
                    m_State = kAuto;
                }
            }
            else
            {
                m_State = kAuto;
            }
        }

        if (m_State == kAuto && !activeSetup.snapOnRelease)
        {
            m_OrbitRate = Mathf.Lerp(m_OrbitRate, activeSetup.canChangeOrbitDirection ? Mathf.Sign(m_OrbitRate) * Mathf.Abs(activeSetup.defaultOrbitRate) : activeSetup.defaultOrbitRate, 0.5f);
        }

        float angle = AngleRelativeToOrbitCenter(m_Transform);
        float leastDifference = float.MaxValue;
        float nearestSnapCornerAngle = 0f;
        for (int i = 0; i < m_SnapTargets.Length; i++)
        {
            float angle1 = m_SnapTargets[i];
            float unsignedDelta = UnsignedDeltaAngle(angle, angle1);
            if ((m_State != kSnapToTarget && m_State != kInitialSnap && unsignedDelta < leastDifference) || (m_State == kSnapToTarget && i == m_NearestSnapTargetIndex) || (m_State == kInitialSnap && i == 0))
            {
                leastDifference = unsignedDelta;
                nearestSnapCornerAngle = angle1;
                m_NearestSnapTargetIndex = i;
                m_DistanceToNearestSnapTarget = unsignedDelta;
            }
        }

        if (m_State == kInitialSnap || m_State == kSnapToTarget)
        {
            int snapTargetIndex = m_State == kInitialSnap ? m_Setups[m_ActiveSetupIndex].snapTargetIndex : m_NearestSnapTargetIndex;
            float angle1 = snapTargetIndex < m_SnapTargets.Length && snapTargetIndex >= 0 ? m_SnapTargets[snapTargetIndex] : DefaultSnapTarget();
            float lastOrbitRate = m_OrbitRate;
            float newAngle = Mathf.SmoothDampAngle(angle, angle1, ref m_OrbitRate, activeSetup.snapSmoothTime, Mathf.Infinity, Time.deltaTime);
            if (newAngle < 0f)
            {
                newAngle += 360f;
            }
            float delta = ClampAngle(newAngle - angle);
            m_OrbitAngle += delta;
            bool movingTowardTarget = (Mathf.DeltaAngle(angle, angle1) >= 0f) == (Mathf.Sign(lastOrbitRate) >= 0f) || Mathf.Approximately(angle, angle1);
            bool stopping = UnsignedDeltaAngle(m_OrbitAngle, angle1) >= UnsignedDeltaAngle(angle, angle1) && movingTowardTarget;
            if (!activeSetup.snapOnRelease && activeSetup.canChangeOrbitDirection && (movingTowardTarget || stopping))
            {
                m_OrbitRate = Mathf.Sign(lastOrbitRate) * Mathf.Max(Mathf.Abs(m_OrbitRate), activeSetup.defaultOrbitRate);
            }
            if (stopping)
            {
                if (activeSetup.snapOnRelease)
                {
                    m_OrbitAngle = angle1;
                }
                m_State = kAuto;
            }
        }
        else if (m_State == kAuto && activeSetup.snapOnRelease)
        {
            float newAngle = Mathf.SmoothDampAngle(angle, nearestSnapCornerAngle, ref m_OrbitRate, activeSetup.snapSmoothTime, Mathf.Infinity, Time.deltaTime);
            if (newAngle < 0f)
            {
                newAngle += 360f;
            }
            float delta = ClampAngle(newAngle - angle);
            m_OrbitAngle += delta;
        }
        else
        {
            m_OrbitAngle += m_OrbitRate * Time.deltaTime;
        }

        if (m_OrbitAngle > 360f)
        {
            m_OrbitAngle -= 360f;
        }
        if (!m_ChangeSetupAnimation.keepWaiting)
        {
            Vector3 position, forward;
            CalculatePositionAndRotation(m_ActiveSetupIndex, out position, out forward, m_OrbitAngle);
            m_Transform.position = position;
            m_Transform.forward = forward;

            float distance = !m_Camera.orthographic
                ? m_ProjectionSize / Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                : 0;
            m_Transform.position += m_Transform.rotation * new Vector3(0, 0, -distance);
            m_Camera.nearClipPlane = Mathf.Max(m_NearClipPlane, m_NearClipPlane + distance - 100);
            m_Camera.farClipPlane = m_NearClipPlane + distance + (m_FarClipPlane - m_NearClipPlane);
        }
    }

    void CalculatePositionAndRotation(int setupIndex, out Vector3 position, out Vector3 forward, float orbitAngle)
    {
        position = OrbitAroundPosition() + Quaternion.AngleAxis(orbitAngle, Vector3.up) * Vector3.forward * m_Setups[setupIndex].radius;
        position.y = m_Setups[setupIndex].height;
        Vector3 lookAtPosition = m_Setups[setupIndex].lookAtHeight * Vector3.up + OrbitAroundPosition();
        forward = (lookAtPosition - position).normalized;
        position += m_Setups[setupIndex].offset;
    }

    float AngleRelativeToOrbitCenter(Transform target)
    {
        Vector3 delta = target.position - OrbitAroundPosition();
        delta.y = 0f;
        float angle = Vector3.SignedAngle(Vector3.forward, delta.normalized, Vector3.up);
        if (angle < 0f)
        {
            angle += 360f;
        }
        return angle;
    }

    static float ClampAngle(float angle)
    {
        return (angle + 540f) % 360f - 180f;
    }

    static float UnsignedDeltaAngle(float angle1, float angle2)
    {
        return Mathf.Abs(Mathf.DeltaAngle(angle1, angle2));
    }



#if UNITY_EDITOR
    void OnValidate()
    {
        m_Transform = GetComponent<Transform>();
        m_Camera = GetComponent<Camera>();

        m_DefaultSetupIndex = Mathf.Clamp(m_DefaultSetupIndex, 0, m_Setups.Length - 1);

        if (Application.isPlaying)
        {
            m_NearestSnapTargetIndex = Mathf.Clamp(m_NearestSnapTargetIndex, 0, m_SnapTargets.Length - 1);
            m_OldSetupIndex = Mathf.Clamp(m_OldSetupIndex, 0, m_Setups.Length - 1);
            m_ActiveSetupIndex = Mathf.Clamp(m_OldSetupIndex, 0, m_Setups.Length - 1);

            if (m_Setups.Length == 0 && m_ChangeSetupAnimation.keepWaiting)
            {
                m_ChangeSetupAnimation.Stop();
            }
        }

        if (m_Setups.Length == 0)
        {
            return;
        }

        if (!Application.isPlaying || !m_ChangeSetupAnimation.keepWaiting)
        {
            int setupIndex = Application.isPlaying ? m_ActiveSetupIndex : m_DefaultSetupIndex;
            float fov;
            float size;
            if (m_Setups[setupIndex].overrideProjection)
            {
                fov = m_Setups[setupIndex].projectionFieldOfView;
                size = m_Setups[setupIndex].projectionSize;
            }
            else
            {
                fov = m_DefaultFieldOfView;
                size = m_DefaultSize;
            }

            if (!Application.isPlaying)
            {
                Vector3 position, forward;
                float angle;
                if (m_Setups[setupIndex].snapToTargetOnEnter && m_Setups[setupIndex].snapTargetIndex >= 0 && m_Setups[setupIndex].snapTargetIndex < m_SnapTargets.Length)
                {
                    angle = m_SnapTargets[m_Setups[setupIndex].snapTargetIndex];
                }
                else
                {
                    angle = m_DefaultOrbitAngle;
                }
                CalculatePositionAndRotation(m_DefaultSetupIndex, out position, out forward, angle);
                m_Transform.position = position;
                m_Transform.forward = forward;
            }

            SetCameraProjection(fov, size);
        }

    }
#endif
}
