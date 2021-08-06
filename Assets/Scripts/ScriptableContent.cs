using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "Assets/Create/ScriptableContent")]
public class ScriptableContent : ScriptableObject
{
    [System.Serializable]
    public struct EventInformation
    {
        public float eventTime; //When this Change will happen
        public int SetupIndex; //Which Setup we use from the orbitting Camera
    }
    [System.Serializable]
    public struct ContentParameters
    {
        public AudioClip audioClip;
        public List<EventInformation> eventInformations;
    }
    public ContentParameters m_ContentParameters;
}
