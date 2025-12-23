using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using UnityEngine;

namespace Assets.PixelFantasy.Common.Scripts.CharacterScripts
{
    public abstract class  CharacterBuilderBase : MonoBehaviour
    {
        public SpriteCollection SpriteCollection;
        public string Body = "Human";
        public string Head = "Human";
        public string Ears = "Human";
        public string Eyes = "Human";
        public string Mouth;
        public string Hair;
        public string Armor;
        public string Helmet;
        public string Weapon;
        public string Firearm;
        public string Shield;
        public string Cape;
        public string Back;
        public string Mask;
        public string Horns;

        public Texture2D Texture { get; protected set; }
        
        public void Awake()
        {
            Rebuild();
        }

        public abstract void Rebuild(bool forceMerge = false);

        public virtual void Reset()
        {
            Head = Ears = Eyes = Body = Hair = Armor = Helmet = Weapon = Firearm = Shield = Cape = Back = Mask = Horns = "";
            Head = "Human";
            Ears = "Human";
            Eyes = "Human";
            Body = "Human";

            Rebuild();
        }

        public void Randomize(bool helmet = true, bool armor = true, bool weapon = true, bool shield = true)
        {
            if (helmet) Helmet = Randomize("Helmet", 20);
            if (armor) Armor = Randomize("Armor", 20);
            if (weapon) Weapon = Randomize("Weapon");

            var bow = Weapon.Contains("Bow"); // TODO:
            var gun = Weapon.Contains("Gun"); // TODO:

            if (bow || gun)
            {
                Shield = "";
            }
            else
            {
                if (shield) Shield = Randomize("Shield", 50);
            }

            Back = bow ? "LeatherQuiver" : "";

            Rebuild();
        }

        private string Randomize(string part, int emptyChance = 0)
        {
            var options = SpriteCollection.Layers.Single(i => i.Name == part).Textures;

            if (Random.Range(0, 100) < emptyChance && Random.Range(0, options.Count + 1) == 0) return "";

            return options[Random.Range(0, options.Count)].name;
        }
    }
}