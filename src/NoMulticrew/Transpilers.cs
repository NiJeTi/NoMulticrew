using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace NoMulticrew;

internal static class Transpilers
{
    public static IEnumerable<CodeInstruction> ReplaceCall(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo target,
        MethodInfo replacement,
        string where
    )
    {
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(target))
            {
                replaced++;
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
                continue;
            }

            yield return instruction;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException($"{where}: expected one {target.Name} call, replaced {replaced}.");
        }
    }
}