using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Procedural greybox accents make the first roster recognisable without
    /// embedding team colours or borrowing external character art.</summary>
    public sealed class HeroSilhouettePresentation : MonoBehaviour
    {
        private const string RootName = "Hero Archetype Accent";

        /// <summary>Lets a hero-specific visual suppress only this procedural fallback
        /// accent while preserving all gameplay-owned children and indicators.</summary>
        public void SetPresentationVisible(bool visible)
        {
            Transform root = transform.Find(RootName);
            if (root != null) root.gameObject.SetActive(visible);
        }

        public void Apply(HeroDefinition definition)
        {
            if(definition==null)return;Transform root=transform.Find(RootName);
            if(root==null){root=new GameObject(RootName).transform;root.SetParent(transform,false);}
            root.gameObject.SetActive(true);
            for(int i=root.childCount-1;i>=0;i--)DestroyObject(root.GetChild(i).gameObject);
            switch(definition.PrimaryRole)
            {
                case HeroRole.Vanguard: Add(root,PrimitiveType.Cube,new Vector3(0,1.1f,-.22f),new Vector3(1.25f,.65f,.3f),definition.ThemeColor);break;
                case HeroRole.Duelist: Add(root,PrimitiveType.Cylinder,new Vector3(.48f,.75f,.18f),new Vector3(.09f,.75f,.09f),definition.ThemeColor);break;
                case HeroRole.Carry: Add(root,PrimitiveType.Cylinder,new Vector3(.55f,1.0f,0),new Vector3(.06f,1.15f,.06f),definition.ThemeColor);break;
                case HeroRole.Mage: Add(root,PrimitiveType.Sphere,new Vector3(0,1.45f,0),Vector3.one*.36f,definition.ThemeColor);break;
                case HeroRole.Support: Add(root,PrimitiveType.Sphere,new Vector3(0,1.15f,-.35f),Vector3.one*.45f,definition.ThemeColor);break;
                case HeroRole.Controller: Add(root,PrimitiveType.Cube,new Vector3(0,1.35f,0),new Vector3(.22f,.9f,.22f),definition.ThemeColor);break;
            }
        }
        private static void Add(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            GameObject accent=GameObject.CreatePrimitive(type);accent.name="Roster Accent";accent.transform.SetParent(parent,false);accent.transform.localPosition=position;accent.transform.localScale=scale;
            Collider collider=accent.GetComponent<Collider>();if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
            Renderer renderer=accent.GetComponent<Renderer>();Shader shader=Shader.Find("Standard");if(shader!=null){Material material=new Material(shader);material.color=color;renderer.sharedMaterial=material;}
        }
        private static void DestroyObject(GameObject value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
