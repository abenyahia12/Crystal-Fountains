using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

enum TweenEasing
{
    Linear,
    QuadIn,
    CubicOut,
    ElasticOut
}

[Serializable]
sealed class TweenRunner : CustomYieldInstruction
{
    [SerializeField] float m_Duration = 1.0f;
    [SerializeField] bool m_IgnoreTimeScale;
    [SerializeField] TweenEasing m_Easing;

    MonoBehaviour m_CoroutineContainer;
    TweenAction m_TweenAction;
    Coroutine m_TweenCoroutine;

    public float duration { get { return m_Duration; } }
    public TweenAction tweenAction { get { return m_TweenAction; } }

    public delegate void TweenAction(float t, float unscaledT, bool init, float duration, bool ignoreTimeScale);

    public override bool keepWaiting
    {
        get { return m_TweenCoroutine != null; }
    }

    static readonly WaitForEndOfFrame s_WaitForEndOfFrame = new WaitForEndOfFrame();

    public static IEnumerator TweenCoroutine(float duration, bool ignoreTimeScale, TweenEasing easing, TweenAction tweenAction)
    {
        yield return s_WaitForEndOfFrame;
        tweenAction(0f, 0f, true, duration, ignoreTimeScale);
        yield return null;
        float elapsedTime = 0.0f;
        while (true)
        {
            elapsedTime += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float unscaledT = duration > 0f ? elapsedTime / duration : 1.0f;
            if (unscaledT >= 1.0f)
            {
                tweenAction(1.0f, 1.0f, false, duration, ignoreTimeScale);
                yield break;
            }
            float t = unscaledT;
            switch (easing)
            {
                case TweenEasing.QuadIn:
                    {
                        t = t * t;
                        break;
                    }
                case TweenEasing.CubicOut:
                    {
                        t = t - 1f;
                        t = 1 + t * t * t;
                        break;
                    }
                case TweenEasing.ElasticOut:
                    {
                        float t1 = (t - 1) * (t - 1);
                        t = 1 - t1 * t1 * Mathf.Cos(t * Mathf.PI * 4.5f);
                        break;
                    }
            }
            tweenAction(t, unscaledT, false, duration, ignoreTimeScale);
            yield return null;
        }
    }

    public void Init(MonoBehaviour coroutineContainer, TweenAction tweenAction)
    {
#if MAP_DEBUG
        if (coroutineContainer == null)
        {
            throw new ArgumentNullException(nameof(coroutineContainer));
        }
        if (tweenAction == null)
        {
            throw new ArgumentNullException(nameof(tweenAction));
        }
#endif

        if (m_CoroutineContainer != null && m_TweenCoroutine != null)
        {
            m_CoroutineContainer.StopCoroutine(m_TweenCoroutine);
            m_TweenCoroutine = null;
        }

        m_CoroutineContainer = coroutineContainer;
        m_TweenAction = (t, unscaledT, init, duration, ignoreTimeScale) =>
        {
            tweenAction(t, unscaledT, init, duration, ignoreTimeScale);
            if (unscaledT == 1.0f)
            {
                m_TweenCoroutine = null;
            }
        };
    }

    public void InstantTweenAction()
    {
        m_TweenAction?.Invoke(1.0f, 1.0f, true, 0f, true);
    }

    public TweenRunner Start()
    {
        if (m_CoroutineContainer == null)
        {
            Debug.LogWarning("Coroutine container not configured... did you forget to call Init?");
            return this;
        }

        Stop();

        if (!m_CoroutineContainer.gameObject.activeInHierarchy)
        {
            m_TweenAction(1.0f, 1.0f, true, 0f, false);
            return this;
        }

        m_TweenCoroutine = m_CoroutineContainer.StartCoroutine(TweenCoroutine(m_Duration = Mathf.Max(m_Duration, 0f), m_IgnoreTimeScale, m_Easing, m_TweenAction));
        return this;
    }

    public void Stop()
    {
        if (m_CoroutineContainer == null)
        {
            Debug.LogWarning("Coroutine container not configured... did you forget to call Init?");
            return;
        }

        if (m_TweenCoroutine != null)
        {
            m_CoroutineContainer.StopCoroutine(m_TweenCoroutine);
            m_TweenCoroutine = null;
        }
    }
}