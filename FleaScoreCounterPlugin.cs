using BepInEx;
using System.IO;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using GlobalEnums;
using HarmonyLib;
using BepInEx.Logging;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace FleaScoreCounter
{
    internal class FleaCounter
    {
        public string name;
        public Vector2 position;
        public Text textObject;
        
        public FleaCounter(string name, Vector2 position, GameObject parent)
        {
            this.name = name;
            this.position = position;

            GameObject GO= new GameObject() { name= this.name + " Counter"};
            this.textObject = GO.AddComponent<Text>();
                GO.AddComponent<CanvasRenderer>();
                textObject.fontSize = FleaScoreCounterPlugin.fontSize;
                textObject.font = Font.GetDefault();
                textObject.text = "0";
                textObject.alignment = TextAnchor.MiddleCenter;
            GO.GetComponent<RectTransform>().SetParent(parent.transform);
            GO.transform.localPosition = position;
        }
    }

    // TODO - adjust the plugin guid as needed
    [BepInAutoPlugin()]
    [Harmony]
    public partial class FleaScoreCounterPlugin : BaseUnityPlugin
    {
        private const string juggleName = "Flea Games Counter";
        private const string bounceName = "Flea Games Counter Bounce Variant";
        private const string dodgeName = "Flea Games Counter Dodge";

        public static FleaScoreCounterPlugin __instance;
        internal static int fontSize = 30;
        internal static ConfigEntry<float> scale;

        internal Image fleaCounterImage;

        internal Dictionary<string, FleaCounter> fleaCounterDict = new Dictionary<string, FleaCounter>();

        private void Awake()
        {
            __instance = this;

            scale = Config.Bind(
                        "UI",
                        "Scale",
                        1f,
                        new ConfigDescription("Flea counter's Scale", new AcceptableValueRange<float>(0, 5))
                    );

            scale.SettingChanged += (_, _) => fleaCounterImage.transform.localScale = new Vector2(fleaCounterImage.sprite.texture.width, fleaCounterImage.sprite.texture.height) / fleaCounterImage.sprite.texture.height * scale.Value;


            // the parent object to holde the UI elemts
            var canvasHolder = new GameObject() { name = "Flea Counter"};
                GameObject.DontDestroyOnLoad(canvasHolder);
                canvasHolder.layer = ((int)PhysLayers.UI);
                canvasHolder.AddComponent<Canvas>();
            
            var fleaCounter = new GameObject() { name = "Flea Overlay" };
                fleaCounter.transform.parent = canvasHolder.transform;
                fleaCounter.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                fleaCounter.AddComponent<CanvasRenderer>();

            fleaCounterImage = fleaCounter.AddComponent<Image>();
                fleaCounterImage.sprite = GetImageFromResources();
                fleaCounterImage.transform.localScale = new Vector2(fleaCounterImage.sprite.texture.width, fleaCounterImage.sprite.texture.height)/ fleaCounterImage.sprite.texture.height;
                fleaCounterImage.rectTransform.pivot = new Vector2(1, 1);
                fleaCounter.AddComponent<FleaCounterPostioner>();

            fleaCounterDict.Add(
                    juggleName, new FleaCounter(juggleName, new Vector2(-85, -98), fleaCounter)
                );

            fleaCounterDict.Add(
                    dodgeName, new FleaCounter(dodgeName, new Vector2(-49, -98), fleaCounter)
                );
            fleaCounterDict.Add(
                    bounceName, new FleaCounter(bounceName, new Vector2(-12, -98), fleaCounter)
                );

            fleaCounterImage.transform.localScale *=scale.Value;

            new Harmony(Id).PatchAll();
        }

        [HarmonyPatch(typeof(SimpleCounter),nameof(SimpleCounter.Increment))]
        [HarmonyPostfix]
        private static void FleaCounterUpdate(SimpleCounter __instance)
        {
            var counter = FleaScoreCounterPlugin.__instance.fleaCounterDict.GetValueOrDefault(__instance.gameObject.name);
            if (counter != null && __instance.count >= Int32.Parse(counter.textObject.text))
                counter.textObject.text = __instance.count.ToString();
        }

        // Keeps the couter in place when the resolution changes
        public class FleaCounterPostioner: MonoBehaviour
        {
            void FixedUpdate()
            {
                var image =this.gameObject.GetComponent<Image>();
                image.rectTransform.localPosition = this.gameObject.transform.parent.localPosition;
            }
        }

        public static Sprite GetImageFromResources()
        {
            var stream = Assembly.GetExecutingAssembly().
                GetManifestResourceStream(
                        Assembly.GetExecutingAssembly().GetName().Name + ".resources.flea_counter.png"
                    );
            
            byte[] array = new byte[stream.Length];
            stream.Read( array, 0, array.Length );

            Texture2D texture = new Texture2D(2, 1);
            texture.LoadImage(array);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(1,1));

            sprite.name = "flea counter sprite";
            return sprite;
        }
    }
}