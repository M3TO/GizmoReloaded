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
    [BepInPlugin("m3to.mods.GizmoReloaded", "Gizmo Reloaded", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        public const string PluginId = "m3to.mods.GizmoReloaded";
        public const string DisplayName = "Gizmo Reloaded";
        public const string Version = "1.0.0";

        int xRot;
        int yRot;
        int zRot;

        Transform gizmoRoot;

        Transform xGizmo;
        Transform yGizmo;
        Transform zGizmo;

        Transform xGizmoRoot;
        Transform yGizmoRoot;
        Transform zGizmoRoot;

        private ConfigEntry<string> snapDivisions;
        private ConfigEntry<string> xKey;
        private ConfigEntry<string> zKey;
        private ConfigEntry<string> resetKey;
        private ConfigEntry<string> cyclePrevSnapKey;
        private ConfigEntry<string> cycleNextSnapKey;
        private ConfigEntry<bool> displayVisuals;
        private ConfigEntry<string> toggleVisuals;

        public int snapIndex;
        public int snapDivision;
        public float snapAngle;
        public List<int> snapDivs;
        public int snapCount;

        GameObject gizmoPrefab;

        private void Awake()
        {
            instance = this;
            snapDivisions = Config.Bind("General", "SnapDivisions", "8, 16, 32, 64", "The different Snap Divisions you can cycle through");
            cyclePrevSnapKey = Config.Bind("General", "CyclePrevSnapKey", "KeypadMinus", "Press this key to go to the previous Snap Division");
            cycleNextSnapKey = Config.Bind("General", "CycleNextSnapKey", "KeypadPlus", "Press this key to go to the next Snap Division");            
            xKey = Config.Bind("General", "xKey", "LeftControl", "Hold this key to rotate in the x plane (red circle)");
            zKey = Config.Bind("General", "zKey", "LeftAlt", "Hold this key to rotate in the z plane (blue circle)");
            resetKey = Config.Bind("General", "ResetKey", "V", "Press this key to reset all axes to zero rotation");
            displayVisuals = Config.Bind("General", "DisplayVisuals", true, "Whether ot not the display of Axis Indicators is displayed by default");
            toggleVisuals = Config.Bind("General", "ToggleVisuals", "KeypadEnter", "Press this key to toggle the display of the Axis Indicators");

            var bundle = AssetBundle.LoadFromMemory(ResourceUtils.GetResource(Assembly.GetExecutingAssembly(), "GizmoReloaded.Resources.gizmos"));
            gizmoPrefab = bundle.LoadAsset<GameObject>("GizmoRoot");
            bundle.Unload(false);

            Harmony.CreateAndPatchAll(typeof(UpdatePlacementGhost_Patch));
            Harmony.CreateAndPatchAll(typeof(UpdatePlacement_Patch));

            
            snapDivs = snapDivisions.Value.Split(',').Select(Int32.Parse).ToList();
            snapCount = snapDivs.Count();
            snapIndex = 0;
            snapDivision = snapDivs[snapIndex];
            snapAngle = 180f / snapDivs[snapIndex];

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

                xGizmo.GetComponent<MeshRenderer>().material.color = new Color(0f, 1.0f, 0f, 0.5f);
                yGizmo.GetComponent<MeshRenderer>().material.color = new Color(1.0f, 0f, 0f, 0.5f);
                zGizmo.GetComponent<MeshRenderer>().material.color = new Color(0f, 0f, 1.0f, 0.5f);


                xGizmoRoot = gizmoRoot.Find("YRoot/ZRoot/XRoot");
                yGizmoRoot = gizmoRoot.Find("YRoot");
                zGizmoRoot = gizmoRoot.Find("YRoot/ZRoot");
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

            var buildMode = Player.m_localPlayer.GetRightItem().m_shared.m_name == "$item_hammer";

            ToggleVisuals(displayVisuals.Value);

            if (Enum.TryParse<KeyCode>(toggleVisuals.Value, out var toggleVisualsKeyCode) && Input.GetKeyUp(toggleVisualsKeyCode) && buildMode)
            {
                if (displayVisuals.Value)
                {
                    displayVisuals.Value = false;
                } else
                {
                    displayVisuals.Value = true;
                }


                ToggleVisuals(displayVisuals.Value);
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
                HandleAxisInput(scrollWheelInput, ref xRot, xGizmo);
            }
            else if (Enum.TryParse<KeyCode>(zKey.Value, out var zKeyCode) && Input.GetKey(zKeyCode) && buildMode)
            {
                HandleAxisInput(scrollWheelInput, ref zRot, zGizmo);
            }
            else if (buildMode)
            {
                HandleAxisInput(scrollWheelInput, ref yRot, yGizmo);
            }

            if (Enum.TryParse<KeyCode>(resetKey.Value, out var resetKeyCode) && Input.GetKeyUp(resetKeyCode) && buildMode)
            {
                ResetRotation();
            }

            UpdateRotation();
        }

        private void HandleAxisInput(int scrollWheelInput, ref int rot, Transform gizmo)
        {
            gizmo.localScale = displayVisuals.Value ? Vector3.one * 1f : new Vector3(0f, 0f, 0f);
            rot = (rot + scrollWheelInput) % (snapDivision * 2);
        }

        private void SetSnaps(int snapIndex)
        {
            snapDivision = snapDivs[snapIndex];
            snapAngle = 180f / snapDivision;

            notifyUser("Current snap divisions: " + snapDivision);
        }

        private void ToggleVisuals(bool show)
        {
            if (show)
            {
                xGizmo.localScale = Vector3.one * 0.8f;
                yGizmo.localScale = Vector3.one * 0.8f;
                zGizmo.localScale = Vector3.one * 0.8f;
            }
            else
            {
                xGizmo.localScale = new Vector3(0f, 0f, 0f);
                yGizmo.localScale = new Vector3(0f, 0f, 0f);
                zGizmo.localScale = new Vector3(0f, 0f, 0f);
            }
        }

        private void UpdateRotation()
        {
            Quaternion xRotation = Quaternion.AngleAxis(xRot * snapAngle, Vector3.right);
            Quaternion yRotation = Quaternion.AngleAxis(yRot * snapAngle, Vector3.up);
            Quaternion zRotation = Quaternion.AngleAxis(zRot * snapAngle, Vector3.forward);
            gizmoRoot.localRotation = yRotation * xRotation * zRotation;
        }

        private void ResetRotation()
        {
            xRot = 0;
            yRot = 0;
            zRot = 0;
        }

        private static Quaternion GetPlacementAngle(float x, float y, float z)
        {
            return instance.xGizmoRoot.rotation;
        }

        private static void notifyUser(string Message, MessageHud.MessageType position = MessageHud.MessageType.TopLeft)
        {
            MessageHud.instance.ShowMessage(position, "GizmoReloaded: " + Message);
        }
    }
}