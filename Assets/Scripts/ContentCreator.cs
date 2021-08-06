using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContentCreator : MonoBehaviour
{
    [SerializeField] float CanvasFadeTime;
    [SerializeField] OrbittingCamera m_OrbittingCamera;
    [SerializeField] CanvasGroup m_CanvasGroup;
    [SerializeField] List<AudioSource> m_FXAudioSources;
    [SerializeField] AudioSource m_MusicAudioSource;


    [SerializeField] ScriptableContent m_ScriptableContent;

    bool fadeState = true;
    public void StartSequence()
    {
        StartCoroutine(PlaySequence());
    }

    private IEnumerator  PlaySequence()
    {

        //Init and fade UI
        float audioLength = m_ScriptableContent.m_ContentParameters.audioClip.length;
        yield return StartCoroutine(FadeCanvas(fadeState));

        //Start the content 
        float counter = 0;
        PlayClip(m_ScriptableContent.m_ContentParameters.audioClip);
        for (int i = 0; i < m_ScriptableContent.m_ContentParameters.eventInformations.Count; i++)
        {

            while (counter < m_ScriptableContent.m_ContentParameters.eventInformations[i].eventTime)
            {
                counter += Time.deltaTime;
                yield return null;
            }
            m_OrbittingCamera.ChangeSetup(m_ScriptableContent.m_ContentParameters.eventInformations[i].SetupIndex);
        }
        while (counter < audioLength)
        {
            counter += Time.deltaTime;
            yield return null;
        }
        StopMusic();
        yield return StartCoroutine(FadeCanvas(fadeState));
        m_OrbittingCamera.ChangeSetup(0);
    }
    //this function is used to fade the canvas when showing the content
    private IEnumerator FadeCanvas(bool isFade)
    {
        float counter = 0;
        if (isFade)
        {
            counter = CanvasFadeTime;
            while (counter > 0)
            {
                counter -= Time.deltaTime;
                m_CanvasGroup.alpha = counter/ CanvasFadeTime;
                yield return null;
                m_CanvasGroup.interactable = false;
                m_CanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            while (counter < CanvasFadeTime)
            {
                counter += Time.deltaTime;
                m_CanvasGroup.alpha = counter / CanvasFadeTime;
                yield return null;
            }
            m_CanvasGroup.interactable = true;
            m_CanvasGroup.blocksRaycasts = true;
        }
        fadeState = !fadeState;
        yield return null;
    }
    private void PlayClip(AudioClip clip)
    {
        foreach (AudioSource item in m_FXAudioSources)
        {
            item.Stop();
        }
        m_MusicAudioSource.clip = clip;
        m_MusicAudioSource.Play();
    }
    private void StopMusic()
    {
        m_MusicAudioSource.Stop();
        foreach (AudioSource item in m_FXAudioSources)
        {
            item.Play();
        }
    }
}
