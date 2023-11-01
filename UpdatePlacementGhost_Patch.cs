using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace GizmoReloaded
{
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    public static class UpdatePlacementGhost_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var placementAnglePatched = false;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if(!placementAnglePatched)
                if (codes[i].opcode == OpCodes.Stfld &&
                    codes[i + 1].opcode == OpCodes.Ldc_R4 &&
                    codes[i + 2].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 3].opcode == OpCodes.Ldfld &&
                    codes[i + 4].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 5].opcode == OpCodes.Ldfld &&
                    codes[i + 6].opcode == OpCodes.Conv_R4 &&
                    codes[i + 7].opcode == OpCodes.Mul &&
                    codes[i + 8].opcode == OpCodes.Ldc_R4 &&
                    codes[i + 9].opcode == OpCodes.Call
                    )

                {
                    codes[i + 9] = CodeInstruction.Call(typeof(Plugin), "GetPlacementAngle");
                        placementAnglePatched = true;
                }


            }
            return codes.AsEnumerable();
        }
    }
}
