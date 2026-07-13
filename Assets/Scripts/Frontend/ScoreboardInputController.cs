using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>Single local input source for the non-interactive hold-Tab board.</summary>
    public sealed class ScoreboardInputController : MonoBehaviour
    {
        public static ScoreboardInputController Active { get; private set; }
        public bool IsHeld { get; private set; }
        private bool focused = true;
        private void OnEnable(){if(Active==null)Active=this;}
        private void OnDisable(){IsHeld=false;if(Active==this)Active=null;}
        private void OnApplicationFocus(bool hasFocus){focused=hasFocus;if(!focused)IsHeld=false;}
        private void Update()=>IsHeld=focused&&Input.GetKey(KeyCode.Tab);
    }
}
