using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoloPogo.Utilities;
using RoloPogo.Utils;
using UnityEngine;

namespace GizmoReloaded
{
    [BepInPlugin(PluginId, DisplayName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        public const string PluginId = "m3to.mods.GizmoReloaded";
        public const string DisplayName = "Gizmo Reloaded";
        public const string Version = "1.1.2";

        Transform gizmoRoot;

        Transform xGizmo;
        Transform yGizmo;
        Transform zGizmo;

        private ConfigEntry<string> snapDivisions;
        private ConfigEntry<string> xKey;
        private ConfigEntry<string> zKey;
        private ConfigEntry<string> resetKey;
        private ConfigEntry<string> cyclePrevSnapKey;
        private ConfigEntry<string> cycleNextSnapKey;
        private ConfigEntry<string> displayMode;
        private ConfigEntry<string> cycleDisplayModeKey;
        private ConfigEntry<string> allowedTools;

        public int snapIndex;
        public int snapDivision;
        public float snapAngle;
        public List<int> snapDivs;
        public int snapCount;
        public List<string> whitelistTools;

        GameObject gizmoPrefab;

        private void Awake()
        {
            instance = this;
            snapDivisions = Config.Bind("General", "SnapDivisions", "8, 16, 32, 64", "The different Snap Divisions per 360° you can cycle through (Vanilla is 16)");
            cyclePrevSnapKey = Config.Bind("General", "CyclePrevSnapKey", "KeypadMinus", "Press this key to go to the previous Snap Division");
            cycleNextSnapKey = Config.Bind("General", "CycleNextSnapKey", "KeypadPlus", "Press this key to go to the next Snap Division");
            xKey = Config.Bind("General", "xKey", "LeftControl", "Hold this key to rotate in the x plane (red circle)");
            zKey = Config.Bind("General", "zKey", "LeftAlt", "Hold this key to rotate in the z plane (blue circle)");
            resetKey = Config.Bind("General", "ResetKey", "V", "Press this key to reset all axes to zero rotation");
            displayMode = Config.Bind("General", "DisplayMode", "ShowAll", "The default visuals mode - Options are ShowAll, OnlySelected, None");
            cycleDisplayModeKey = Config.Bind("General", "CycleDisplayModeKey", "KeypadEnter", "Press this key to cycle through the different axis Display Modes");
            allowedTools = Config.Bind("General", "AllowedTools", "item_hammer, item_cultivator, item_blueprintrune, PlumgaPlantItShovel", "The different Tools that can use Gizmos");

            var bundle = AssetBundle.LoadFromMemory(ResourceUtils.GetResource(Assembly.GetExecutingAssembly(), "GizmoReloaded.Resources.gizmos"));
            gizmoPrefab = bundle.LoadAsset<GameObject>("GizmoRoot");
            bundle.Unload(false);

            Harmony.CreateAndPatchAll(typeof(UpdatePlacementGhost_Patch));
            Harmony.CreateAndPatchAll(typeof(UpdatePlacement_Patch));

            snapDivs = snapDivisions.Value.Split(',').Select(Int32.Parse).ToList();
            snapCount = snapDivs.Count();
            snapIndex = 1;
            snapDivision = snapDivs[snapIndex];
            snapAngle = 360f / snapDivs[snapIndex];

            whitelistTools = allowedTools.Value.Split(',').Select(t => t.Trim().Insert(0, "$")).ToList();
        }

        public void UpdatePlacement(Player player, GameObject placementGhost, bool takeInput)
        {
            if (player != Player.m_localPlayer) return;

            if (!gizmoRoot)
            {
                gizmoRoot = Instantiate(gizmoPrefab).transform;
                xGizmo = gizmoRoot.Find("YRoot/ZRoot/XRoot/X");
                yGizmo = gizmoRoot.Find("YRoot/Y");
                zGizmo = gizmoRoot.Find("YRoot/ZRoot/Z");

                xGizmo.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
                yGizmo.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
                zGizmo.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));

                xGizmo.GetComponent<MeshRenderer>().material.color = new Color(1.0f, 0f, 0f, 1f);
                yGizmo.GetComponent<MeshRenderer>().material.color = new Color(0f, 1.0f, 0f, 1f);
                zGizmo.GetComponent<MeshRenderer>().material.color = new Color(0f, 0f, 1.0f, 1f);
            }

            var marker = player.GetPrivateField<GameObject>("m_placementMarkerInstance");
            if (marker)
            {
                gizmoRoot.gameObject.SetActive(marker.activeSelf);
                gizmoRoot.position = marker.transform.position + Vector3.up * .5f;
            }

            if (!player.InPlaceMode())
                return;

            if (!takeInput)
                return;

            bool IsPieceSlectionVisible()
            {
                return !(Hud.instance == null) && Hud.instance.m_buildHud.activeSelf && Hud.instance.m_pieceSelectionWindow.activeSelf;
            };

            bool buildMode = whitelistTools.Contains(Player.m_localPlayer.GetRightItem().m_shared.m_name) && !IsPieceSlectionVisible();

            switch (displayMode.Value)
            {
                case "ShowAll":
                    xGizmo.localScale = Vector3.one * 0.9f;
                    yGizmo.localScale = Vector3.one * 0.9f;
                    zGizmo.localScale = Vector3.one * 0.9f;
                    break;
                case "OnlySelected":
                case "None":
                    xGizmo.localScale = new Vector3(0f, 0f, 0f);
                    yGizmo.localScale = new Vector3(0f, 0f, 0f);
                    zGizmo.localScale = new Vector3(0f, 0f, 0f);
                    break;
            }

            if (Enum.TryParse<KeyCode>(cycleDisplayModeKey.Value, out var toggleDisplayModeKeyCode) 
                && Input.GetKeyUp(toggleDisplayModeKeyCode) 
                && buildMode)
            {
                switch (displayMode.Value)
                {
                    case "ShowAll":
                        xGizmo.localScale = new Vector3(0f, 0f, 0f);
                        yGizmo.localScale = new Vector3(0f, 0f, 0f);
                        zGizmo.localScale = new Vector3(0f, 0f, 0f);
                        displayMode.Value = "OnlySelected";
                        notifyUser("Display Mode is \"OnlySelected\"");
                        break;
                    case "OnlySelected":
                        xGizmo.localScale = new Vector3(0f, 0f, 0f);
                        yGizmo.localScale = new Vector3(0f, 0f, 0f);
                        zGizmo.localScale = new Vector3(0f, 0f, 0f);
                        displayMode.Value = "None";
                        notifyUser("Display Mode is \"None\"");
                        break;
                    case "None":
                        xGizmo.localScale = Vector3.one * 0.9f;
                        yGizmo.localScale = Vector3.one * 0.9f;
                        zGizmo.localScale = Vector3.one * 0.9f;
                        displayMode.Value = "ShowAll";
                        notifyUser("Display Mode is \"ShowAll\"");
                        break;
                }
            }

            if (Enum.TryParse<KeyCode>(cyclePrevSnapKey.Value, out var prevSnapKeyCode) && Input.GetKeyUp(prevSnapKeyCode) && buildMode)
            {
                snapIndex = snapIndex - 1 < 0 ? snapCount - 1  : snapIndex - 1;
                SetSnaps(snapIndex);
            }

            if (Enum.TryParse<KeyCode>(cycleNextSnapKey.Value, out var nextSnapKeyCode) && Input.GetKeyUp(nextSnapKeyCode) && buildMode)
            {
                snapIndex = snapIndex + 1 == snapCount ? 0 : snapIndex + 1;
                SetSnaps(snapIndex);
            }

            var scrollWheelInput = Math.Sign(Input.GetAxis("Mouse ScrollWheel"));

            if (Enum.TryParse<KeyCode>(xKey.Value, out var xKeyCode) && Input.GetKey(xKeyCode) && buildMode)
            {
                HandleAxisInput(scrollWheelInput, xGizmo, "X");
            }
            else if (Enum.TryParse<KeyCode>(zKey.Value, out var zKeyCode) && Input.GetKey(zKeyCode) && buildMode)
            {
                HandleAxisInput(scrollWheelInput, zGizmo, "Z");
            }
            else if (buildMode)
            {
                HandleAxisInput(scrollWheelInput, yGizmo, "Y");
            }

            if (Enum.TryParse<KeyCode>(resetKey.Value, out var resetKeyCode) && Input.GetKeyUp(resetKeyCode) && buildMode)
            {
                ResetRotation();
            }            
        }

        private void HandleAxisInput(int scrollWheelInput, Transform gizmo, string axis)
        {
            if (displayMode.Value == "ShowAll")
            {
                gizmo.localScale = Vector3.one * 1.1f;
            } else if (displayMode.Value == "OnlySelected")
            {
                gizmo.localScale = Vector3.one * 1.1f;
                if (axis == "X")
                {
                    yGizmo.localScale = new Vector3(0f, 0f, 0f);
                    zGizmo.localScale = new Vector3(0f, 0f, 0f);
                } else if (axis == "Y")
                {
                    xGizmo.localScale = new Vector3(0f, 0f, 0f);
                    zGizmo.localScale = new Vector3(0f, 0f, 0f);
                }  else if (axis == "Z")
                {
                    xGizmo.localScale = new Vector3(0f, 0f, 0f);
                    yGizmo.localScale = new Vector3(0f, 0f, 0f);
                }
            }

            UpdateRotation(scrollWheelInput, axis);
        }

        private void SetSnaps(int snapIndex)
        {
            snapDivision = snapDivs[snapIndex];
            snapAngle = 360f / snapDivision;
            ResetRotation();

            notifyUser("Current snap divisions: " + snapDivision);
        }

        private void UpdateRotation(int scrollWheelinput, string axis = "X")
        {
            if (axis == "X")
            {
                gizmoRoot.rotation *= Quaternion.AngleAxis(scrollWheelinput * snapAngle, Vector3.right);
            }
            else if (axis == "Y")
            {
                gizmoRoot.rotation *= Quaternion.AngleAxis(scrollWheelinput * snapAngle, Vector3.up);
            }
            else if (axis == "Z")
            {
                gizmoRoot.rotation *= Quaternion.AngleAxis(scrollWheelinput * snapAngle, Vector3.forward);
            }

        }

        private void ResetRotation()
        {
            gizmoRoot.rotation = Quaternion.AngleAxis(0f, Vector3.up);
            gizmoRoot.rotation = Quaternion.AngleAxis(0f, Vector3.right);
            gizmoRoot.rotation = Quaternion.AngleAxis(0f, Vector3.forward);
        }

        private static Quaternion GetPlacementAngle(float x, float y, float z)
        {
            return instance.gizmoRoot.rotation;
        }

        private static void notifyUser(string Message, MessageHud.MessageType position = MessageHud.MessageType.TopLeft)
        {
            MessageHud.instance.ShowMessage(position, "GizmoReloaded: " + Message);
        }
    }
}