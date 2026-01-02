using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using Silksong.DataManager;
using Silksong.ModMenu;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace FleaScoreCounter
{

    internal static class FleaSimpleCounterNames
    {
        internal static string juggleName = "Flea Games Counter";
        internal static string bounceName = "Flea Games Counter Bounce Variant";
        internal static string dodgeName = "Flea Games Counter Dodge";
        internal static string[] names = [juggleName, bounceName, dodgeName];
    }
    public class SaveData
    {
        private static SaveData? _instance;

        [AllowNull]
        public static SaveData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        public Dictionary<string, int> highScores { get; set; }  = new()
        {
            { FleaSimpleCounterNames.dodgeName, 0},
            { FleaSimpleCounterNames.juggleName, 0},
            { FleaSimpleCounterNames.bounceName, 0},
        };
    }
    internal class FleaCounter
    {
        
        public string name;
        public Vector2 position;
        public Text textObject;
        
        public FleaCounter(string name, Vector2 position, GameObject parent, int count)
        {
            this.name = name;
            this.position = position;

            GameObject GO= new GameObject() { name= this.name + " Counter"};
            this.textObject = GO.AddComponent<Text>();
                textObject.fontSize = FleaScoreCounterPlugin.fontSize;
                textObject.font = Font.GetDefault();
                textObject.text = count.ToString();
                textObject.alignment = TextAnchor.MiddleCenter;
            GO.GetComponent<RectTransform>().SetParent(parent.transform);
            GO.transform.localPosition = position;
        }
    }

    // TODO - adjust the plugin guid as needed
    [BepInDependency("org.silksong-modding.datamanager")]
    [BepInDependency("org.silksong-modding.modmenu")]
    [BepInAutoPlugin(id:"io.github.randomscorp.fleascorecounter",version:"1.1.0")]
    [Harmony]
    public partial class FleaScoreCounterPlugin : BaseUnityPlugin, ISaveDataMod<SaveData>, IModMenuCustomMenu
    {

        public static FleaScoreCounterPlugin __instance;
        internal static int fontSize = 30;
        internal static ConfigEntry<float> scale;

        internal Image fleaCounterImage;

        internal Dictionary<string, FleaCounter> fleaCounterDict = new Dictionary<string, FleaCounter>();

        SaveData? ISaveDataMod<SaveData>.SaveData
        {
            get => SaveData.Instance;
            set => SaveData.Instance = value;
        }

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
                    FleaSimpleCounterNames.juggleName, new FleaCounter(FleaSimpleCounterNames.juggleName, new Vector2(-85, -98), fleaCounter, 0)
                );

            fleaCounterDict.Add(
                    FleaSimpleCounterNames.dodgeName, new FleaCounter(FleaSimpleCounterNames.dodgeName, new Vector2(-49, -98), fleaCounter, 0)
                );
            fleaCounterDict.Add(
                    FleaSimpleCounterNames.bounceName, new FleaCounter(FleaSimpleCounterNames.bounceName, new Vector2(-12, -98), fleaCounter, 0)
                );

            fleaCounterImage.transform.localScale *=scale.Value;

            new Harmony(Id).PatchAll();
        }

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.Awake))]
        [HarmonyPrefix]
        private static void FillCounter()
        {
            foreach(string name in FleaSimpleCounterNames.names)
            {
                FleaScoreCounterPlugin.__instance.fleaCounterDict[name].textObject.text = SaveData.Instance.highScores[name].ToString();
            }
        }

        [HarmonyPatch(typeof(SimpleCounter),nameof(SimpleCounter.Increment))]
        [HarmonyPostfix]
        private static void FleaCounterUpdate(SimpleCounter __instance)
        {
            var counter = FleaScoreCounterPlugin.__instance.fleaCounterDict.GetValueOrDefault(__instance.gameObject.name);
            if (counter != null && __instance.count >= SaveData.Instance.highScores[__instance.gameObject.name])
            {
                SaveData.Instance.highScores[__instance.gameObject.name] = __instance.count;
                counter.textObject.text = __instance.count.ToString();
            }
        }

        // Keeps the couter in place when the resolution changes
        // TODO: change it to events
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

        public AbstractMenuScreen BuildCustomMenu()
        {
            var menu =  new SimpleMenuScreen("Flea Score Counter");

            menu.Add(
                    new TextButton("Reset Juggle")
                    {
                        OnSubmit = () => {
                            SaveData.Instance.highScores[FleaSimpleCounterNames.juggleName] = 0;
                            FleaScoreCounterPlugin.__instance.fleaCounterDict[FleaSimpleCounterNames.juggleName].textObject.text = 0.ToString();
                        }
                    }
                );

            menu.Add(
                    new TextButton("Reset Dodge")
                    {
                        OnSubmit = () => {
                            SaveData.Instance.highScores[FleaSimpleCounterNames.dodgeName] = 0;
                            FleaScoreCounterPlugin.__instance.fleaCounterDict[FleaSimpleCounterNames.dodgeName].textObject.text = 0.ToString();
                        }
                    }
                );

            menu.Add(
                    new TextButton("Reset Bounce")
                    {
                        OnSubmit = () => {
                            SaveData.Instance.highScores[FleaSimpleCounterNames.bounceName] = 0;
                            FleaScoreCounterPlugin.__instance.fleaCounterDict[FleaSimpleCounterNames.bounceName].textObject.text = 0.ToString();
                        }
                    }
                );

            return menu;
        }

        public string ModMenuName() => "Flea Score Counter";
    }
}