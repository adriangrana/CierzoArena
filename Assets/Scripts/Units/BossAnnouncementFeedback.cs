using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class BossAnnouncementFeedback : MonoBehaviour
    {
        private static string message; private static float until;
        public static void Show(string value){message=value;until=Time.unscaledTime+5f;}
        private void OnGUI(){if(string.IsNullOrEmpty(message)||Time.unscaledTime>=until)return;GUIStyle style=new GUIStyle(GUI.skin.box){fontSize=24,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};GUI.Label(new Rect(Screen.width*.5f-260f,40f,520f,48f),message,style);}
    }
}
